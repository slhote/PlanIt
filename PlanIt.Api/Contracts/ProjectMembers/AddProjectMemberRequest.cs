using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Contracts.ProjectMembers;

public record AddProjectMemberRequest(Guid UserId, ProjectMemberRole Role = ProjectMemberRole.Member);
