using MediatR;
using Microsoft.AspNetCore.SignalR;
using PlanIt.Api.Hubs;

namespace PlanIt.Api.Application.Realtime;

public class WorkItemDeletedSignalRHandler(IHubContext<PlanItHub> hub) : INotificationHandler<WorkItemDeletedNotification>
{
    public Task Handle(WorkItemDeletedNotification notification, CancellationToken cancellationToken) =>
        RealtimeGroups.Clients(hub, notification.ProjectId, notification.OriginConnectionId)
            .SendAsync("WorkItemDeleted", new { notification.DeletedIds }, cancellationToken);
}
