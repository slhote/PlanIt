using MediatR;
using Microsoft.AspNetCore.SignalR;
using PlanIt.Api.Hubs;

namespace PlanIt.Api.Application.Realtime;

public class ProjectMemberRemovedSignalRHandler(IHubContext<PlanItHub> hub) : INotificationHandler<ProjectMemberRemovedNotification>
{
    public Task Handle(ProjectMemberRemovedNotification notification, CancellationToken cancellationToken) =>
        RealtimeGroups.Clients(hub, notification.ProjectId, notification.OriginConnectionId)
            .SendAsync("ProjectMemberRemoved", new { notification.UserId }, cancellationToken);
}
