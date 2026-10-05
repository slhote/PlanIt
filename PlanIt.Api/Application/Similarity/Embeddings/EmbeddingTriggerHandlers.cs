using MediatR;
using PlanIt.Api.Application.Realtime;
using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Application.Similarity.Embeddings;

// Event-driven trigger: rides the existing WorkItem MediatR notifications rather than adding a new publish call at the mutation sites.
// Auto-discovered by AddMediatR's assembly scan, same as the SignalR handlers -- no manual registration needed.
public class EmbeddingWorkItemCreatedTriggerHandler(EmbeddingWorkQueue queue) : INotificationHandler<WorkItemCreatedNotification>
{
    public Task Handle(WorkItemCreatedNotification notification, CancellationToken cancellationToken)
    {
        queue.Enqueue(notification.Item.Id);
        return Task.CompletedTask;
    }
}

// Text-only changes are covered by EmbeddingWorkItemUpdatedTriggerHandler + the periodic sweep's SourceText comparison.
public class EmbeddingWorkItemUpdatedTriggerHandler(EmbeddingWorkQueue queue) : INotificationHandler<WorkItemUpdatedNotification>
{
    public Task Handle(WorkItemUpdatedNotification notification, CancellationToken cancellationToken)
    {
        queue.Enqueue(notification.WorkItemId);
        return Task.CompletedTask;
    }
}

// This covers the edge case: an item embedded, then completed, then reopened with no text change.
// A fresh embedding is still needed since candidates exclude Completed items regardless,
// but a reopened item can again be the *reference* item.
public class EmbeddingWorkItemStatusChangedTriggerHandler(EmbeddingWorkQueue queue) : INotificationHandler<WorkItemStatusChangedNotification>
{
    public Task Handle(WorkItemStatusChangedNotification notification, CancellationToken cancellationToken)
    {
        if (notification.OldStatus == WorkItemStatus.Completed && notification.NewStatus != WorkItemStatus.Completed)
        {
            queue.Enqueue(notification.WorkItemId);
        }

        return Task.CompletedTask;
    }
}