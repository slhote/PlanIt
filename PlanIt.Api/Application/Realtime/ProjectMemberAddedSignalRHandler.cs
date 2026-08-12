using MediatR;
using Microsoft.AspNetCore.SignalR;
using PlanIt.Api.Hubs;

namespace PlanIt.Api.Application.Realtime;

public class ProjectMemberAddedSignalRHandler(IHubContext<PlanItHub> hub) : INotificationHandler<ProjectMemberAddedNotification>
{
    public Task Handle(ProjectMemberAddedNotification notification, CancellationToken cancellationToken) =>
        RealtimeGroups.Clients(hub, notification.ProjectId, notification.OriginConnectionId)
            .SendAsync("ProjectMemberAdded", new { notification.UserId }, cancellationToken);
}
