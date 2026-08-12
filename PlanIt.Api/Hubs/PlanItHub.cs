using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Hubs;

// Per-project groups, joined explicitly by the client after connecting (not in
// OnConnectedAsync) — one connection can belong to multiple project groups (e.g. a project-list
// page and a board page) without reconnecting. The client must call JoinProject again after every
// reconnect, since ConnectionId (and therefore group membership) resets — that's what satisfies
// "re-verify/re-join on every connect" (planit-api-contracts-backend.md §5).
[Authorize]
public class PlanItHub(IProjectMemberRepository projectMembers) : Hub
{
    public async Task JoinProject(Guid projectId)
    {
        var userId = GetUserId();
        if (!await projectMembers.IsMemberAsync(projectId, userId))
        {
            throw new HubException("Not authorized for this project.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId));
    }

    public static string GroupName(Guid projectId) => $"project-{projectId}";

    private Guid GetUserId()
    {
        var subClaim = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? throw new HubException("No authenticated user on this connection.");
        return Guid.Parse(subClaim);
    }
}
