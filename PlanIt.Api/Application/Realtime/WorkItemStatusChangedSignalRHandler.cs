using MediatR;
using Microsoft.AspNetCore.SignalR;
using PlanIt.Api.Hubs;

namespace PlanIt.Api.Application.Realtime;

public class WorkItemStatusChangedSignalRHandler(IHubContext<PlanItHub> hub) : INotificationHandler<WorkItemStatusChangedNotification>
{
    public Task Handle(WorkItemStatusChangedNotification notification, CancellationToken cancellationToken) =>
        RealtimeGroups.Clients(hub, notification.ProjectId, notification.OriginConnectionId)
            .SendAsync(
                "WorkItemStatusChanged",
                new { notification.WorkItemId, notification.OldStatus, notification.NewStatus },
                cancellationToken);
}
