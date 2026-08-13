using MediatR;
using Microsoft.AspNetCore.SignalR;
using PlanIt.Api.Hubs;

namespace PlanIt.Api.Application.Realtime;

// The only class (per notification) that touches Microsoft.AspNetCore.SignalR — the service layer
// depends on MediatR's IMediator, never on IHubContext directly.
public class WorkItemCreatedSignalRHandler(IHubContext<PlanItHub> hub) : INotificationHandler<WorkItemCreatedNotification>
{
    public Task Handle(WorkItemCreatedNotification notification, CancellationToken cancellationToken) =>
        RealtimeGroups.Clients(hub, notification.Item.ProjectId, notification.OriginConnectionId)
            .SendAsync("WorkItemCreated", notification.Item, cancellationToken);
}
