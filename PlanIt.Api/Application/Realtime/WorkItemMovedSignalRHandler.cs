using MediatR;
using Microsoft.AspNetCore.SignalR;
using PlanIt.Api.Hubs;

namespace PlanIt.Api.Application.Realtime;

public class WorkItemMovedSignalRHandler(IHubContext<PlanItHub> hub) : INotificationHandler<WorkItemMovedNotification>
{
    public Task Handle(WorkItemMovedNotification notification, CancellationToken cancellationToken) =>
        RealtimeGroups.Clients(hub, notification.ProjectId, notification.OriginConnectionId)
            .SendAsync(
                "WorkItemMoved",
                new { notification.WorkItemId, notification.ParentId, notification.Status, notification.Order },
                cancellationToken);
}
