using Microsoft.AspNetCore.SignalR;
using PlanIt.Api.Hubs;

namespace PlanIt.Api.Application.Realtime;

// Shared origin-exclusion logic for every handler below: the frontend sends its current SignalR
// ConnectionId on mutating REST calls via the X-SignalR-Connection-Id header (plumbed through the
// controller -> service -> notification); GroupExcept when present so the client that made the
// change doesn't get its own echo, Group when absent (e.g. no live connection yet).
internal static class RealtimeGroups
{
    public static IClientProxy Clients(IHubContext<PlanItHub> hub, Guid projectId, string? originConnectionId)
    {
        var group = PlanItHub.GroupName(projectId);
        return originConnectionId is { } id
            ? hub.Clients.GroupExcept(group, [id])
            : hub.Clients.Group(group);
    }
}
