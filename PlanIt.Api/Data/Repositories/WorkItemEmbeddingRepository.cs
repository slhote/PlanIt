using Microsoft.EntityFrameworkCore;
using Pgvector;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Data.Repositories;

public class WorkItemEmbeddingRepository : IWorkItemEmbeddingRepository
{
    private readonly PlanItDbContext _db;

    public WorkItemEmbeddingRepository(PlanItDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, Vector>> GetVectorsAsync(EmbeddingSource source, IReadOnlyList<Guid> workItemIds)
    {
        return source switch
        {
            EmbeddingSource.Onnx => await _db.WorkItemEmbeddingsOnnx
                .Where(e => workItemIds.Contains(e.WorkItemId))
                .ToDictionaryAsync(e => e.WorkItemId, e => e.Vector),
            EmbeddingSource.Python => await _db.WorkItemEmbeddingsPython
                .Where(e => workItemIds.Contains(e.WorkItemId))
                .ToDictionaryAsync(e => e.WorkItemId, e => e.Vector),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };
    }

    // Tracks the insert/update only — SaveChangesAsync is the caller's responsibility, matching
    // the rest of the repository layer (planit-persistence-wiring.md).
    public async Task UpsertAsync(EmbeddingSource source, Guid workItemId, Vector vector, string sourceText, DateTimeOffset computedAt)
    {
        switch (source)
        {
            case EmbeddingSource.Onnx:
                var onnxRow = await _db.WorkItemEmbeddingsOnnx.FindAsync(workItemId);
                if (onnxRow is null)
                {
                    _db.WorkItemEmbeddingsOnnx.Add(new WorkItemEmbeddingOnnx
                    {
                        WorkItemId = workItemId,
                        Vector = vector,
                        SourceText = sourceText,
                        ComputedAt = computedAt,
                    });
                }
                else
                {
                    onnxRow.Vector = vector;
                    onnxRow.SourceText = sourceText;
                    onnxRow.ComputedAt = computedAt;
                }
                break;

            case EmbeddingSource.Python:
                var pythonRow = await _db.WorkItemEmbeddingsPython.FindAsync(workItemId);
                if (pythonRow is null)
                {
                    _db.WorkItemEmbeddingsPython.Add(new WorkItemEmbeddingPython
                    {
                        WorkItemId = workItemId,
                        Vector = vector,
                        SourceText = sourceText,
                        ComputedAt = computedAt,
                    });
                }
                else
                {
                    pythonRow.Vector = vector;
                    pythonRow.SourceText = sourceText;
                    pythonRow.ComputedAt = computedAt;
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, null);
        }
    }
}
