using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace PlanIt.Api.Application.Auth;

// ASP.NET Core's authorization middleware returns 403 on a failed policy by default. The
// project's "404, not 403" rule for unauthorized project access (planit-system-design-architecture.md
// §4, avoid leaking project existence to non-members) requires overriding that specifically for
// ProjectMemberRequirement failures — every other authorization failure (e.g. a missing/invalid
// Bearer token) falls through to the default 401/403 behavior, unchanged.
public class ProjectMember404ResultHandler(IProblemDetailsService problemDetailsService) : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var failedOnProjectMembership = authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure!.FailedRequirements.OfType<ProjectMemberRequirement>().Any();

        if (!failedOnProjectMembership)
        {
            await DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
            },
        });
    }
}
