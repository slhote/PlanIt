using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Application.Auth;

// Explicit, acknowledged departure from "repository access is service-layer-only"
// (planit-persistence-wiring.md) — the authorization layer touches IProjectMemberRepository
// directly so ASP.NET Core's authorization middleware can gate the request before it ever
// reaches a controller action. See planit-api-contracts-backend.md §7 for the full rationale.
public class ProjectMemberAuthorizationHandler(IProjectMemberRepository projectMembers, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<ProjectMemberRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ProjectMemberRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("ProjectMemberAuthorizationHandler requires an active HttpContext.");

        if (!httpContext.GetRouteData().Values.TryGetValue("projectId", out var routeProjectId)
            || !Guid.TryParse(routeProjectId?.ToString(), out var projectId))
        {
            // No projectId route value to check against — not this handler's concern, leave the
            // requirement unsatisfied rather than guessing.
            return;
        }

        var subClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
        {
            return;
        }

        if (await projectMembers.IsMemberAsync(projectId, userId))
        {
            context.Succeed(requirement);
        }
    }
}
