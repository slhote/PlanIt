using Microsoft.Extensions.Options;
using Pgvector;
using PlanIt.Api.Data;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Repositories;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Application.Similarity.Embeddings;

// Single worker covering all three trigger paths:
// - Event-driven: drains EmbeddingWorkQueue, fed by the MediatR trigger handlers.
// - Periodic sweep: on a timer, finds stale/missing rows and enqueues them (also the backfill --
//   a fresh deploy has zero embeddings, so the first sweep run enqueues everything).
// - On-demand recompute-all: just another producer onto the same queue (see the recompute
//   endpoint added in a later step).
//
// Every dequeued item is computed via BOTH generators and written to BOTH tables, regardless of
// which one SimilarWorkItems:EmbeddingSource currently reads from -- that's what keeps the two
// sources comparable side by side. A generator failure is caught/logged and skipped per-source;
// one bad item (or the Python service being down locally) never blocks the queue.
public class EmbeddingBackgroundService(
    EmbeddingWorkQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<EmbeddingWorkerOptions> options,
    ILogger<EmbeddingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sweepLoop = RunSweepLoopAsync(stoppingToken);

        await foreach (var workItemId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(workItemId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // An unhandled exception here would propagate out of ExecuteAsync and take the
                // whole BackgroundService (and, by default host behavior, the app) down with it --
                // one bad item must never do that.
                logger.LogError(ex, "Unhandled failure processing embedding for work item {WorkItemId}.", workItemId);
            }
        }

        await sweepLoop;
    }

    private async Task RunSweepLoopAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.Value.SweepIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var embeddingRepository = scope.ServiceProvider.GetRequiredService<IWorkItemEmbeddingRepository>();
                var staleIds = await embeddingRepository.GetStaleOrMissingWorkItemIdsAsync();

                foreach (var id in staleIds)
                {
                    queue.Enqueue(id);
                }

                if (staleIds.Count > 0)
                {
                    logger.LogInformation("Embedding sweep enqueued {Count} stale/missing work items.", staleIds.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Embedding sweep failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(Guid workItemId, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var workItemRepository = sp.GetRequiredService<IWorkItemRepository>();
        var workItem = await workItemRepository.GetByIdAsync(workItemId);
        if (workItem is null)
        {
            // Deleted between being enqueued and being processed -- nothing to do.
            return;
        }

        var sourceText = $"{workItem.Title} {workItem.Description}".Trim();
        var computedAt = DateTimeOffset.UtcNow;
        var embeddingRepository = sp.GetRequiredService<IWorkItemEmbeddingRepository>();

        var onnxGenerator = sp.GetRequiredService<OnnxEmbeddingGenerator>();
        try
        {
            var vector = await onnxGenerator.EmbedAsync(sourceText, stoppingToken);
            await embeddingRepository.UpsertAsync(EmbeddingSource.Onnx, workItemId, new Vector(vector), sourceText, computedAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ONNX embedding failed for work item {WorkItemId}.", workItemId);
        }

        var pythonGenerator = sp.GetRequiredService<PythonEmbeddingGenerator>();
        try
        {
            var vector = await pythonGenerator.EmbedAsync(sourceText, stoppingToken);
            await embeddingRepository.UpsertAsync(EmbeddingSource.Python, workItemId, new Vector(vector), sourceText, computedAt);
        }
        catch (Exception ex)
        {
            // Expected/frequent in local dev when the Python service isn't running -- the ONNX
            // side still succeeds independently, so this doesn't escalate above a warning.
            logger.LogWarning(ex, "Python embedding failed for work item {WorkItemId}.", workItemId);
        }

        var db = sp.GetRequiredService<PlanItDbContext>();
        await db.SaveChangesAsync(stoppingToken);
    }
}