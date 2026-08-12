using PlanIt.Api.Contracts.Users;
using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Contracts.ProjectMembers;

public record ProjectMemberDto(Guid ProjectId, Guid UserId, ProjectMemberRole Role, DateTimeOffset JoinedAt, UserSummaryDto User);
