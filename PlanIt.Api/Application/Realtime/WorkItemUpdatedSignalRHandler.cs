using MediatR;
using Microsoft.AspNetCore.SignalR;
using PlanIt.Api.Hubs;

namespace PlanIt.Api.Application.Realtime;

public class WorkItemUpdatedSignalRHandler(IHubContext<PlanItHub> hub) : INotificationHandler<WorkItemUpdatedNotification>
{
    public Task Handle(WorkItemUpdatedNotification notification, CancellationToken cancellationToken) =>
        RealtimeGroups.Clients(hub, notification.ProjectId, notification.OriginConnectionId)
            .SendAsync("WorkItemUpdated", new { notification.WorkItemId }, cancellationToken);
}
