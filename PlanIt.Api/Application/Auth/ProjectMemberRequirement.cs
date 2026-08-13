using Microsoft.AspNetCore.Authorization;

namespace PlanIt.Api.Application.Auth;

// Marker requirement — all the logic lives in ProjectMemberAuthorizationHandler.
public class ProjectMemberRequirement : IAuthorizationRequirement
{
}
