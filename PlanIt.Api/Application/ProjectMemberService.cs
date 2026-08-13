using MediatR;
using PlanIt.Api.Application.Realtime;
using PlanIt.Api.Contracts.ProjectMembers;
using PlanIt.Api.Contracts.Users;
using PlanIt.Api.Data;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Exceptions;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Application;

public class ProjectMemberService(
    IProjectMemberRepository projectMemberRepository,
    IUserRepository userRepository,
    PlanItDbContext db,
    IMediator mediator)
{
    public async Task<IReadOnlyList<ProjectMemberDto>> GetForProjectAsync(Guid projectId)
    {
        var members = await projectMemberRepository.GetForProjectAsync(projectId);
        return members.Select(ToDto).ToList();
    }

    public async Task<ProjectMemberDto> AddAsync(Guid projectId, AddProjectMemberRequest request, string? originConnectionId = null)
    {
        var user = await userRepository.GetByIdAsync(request.UserId)
            ?? throw new TaskNotFoundException($"User {request.UserId} not found.");

        if (await projectMemberRepository.GetAsync(projectId, request.UserId) is not null)
        {
            throw new ValidationException($"User {request.UserId} is already a member of this project.");
        }

        var member = new ProjectMember
        {
            ProjectId = projectId,
            UserId = request.UserId,
            Role = request.Role,
            JoinedAt = DateTimeOffset.UtcNow,
        };
        projectMemberRepository.Add(member);
        await db.SaveChangesAsync();

        await mediator.Publish(new ProjectMemberAddedNotification(projectId, request.UserId, originConnectionId));

        return new ProjectMemberDto(projectId, request.UserId, request.Role, member.JoinedAt, ToUserDto(user));
    }

    // At least one Owner must always remain — otherwise a project could end up with no one able
    // to manage membership.
    public async Task RemoveAsync(Guid projectId, Guid userId, string? originConnectionId = null)
    {
        var member = await projectMemberRepository.GetAsync(projectId, userId)
            ?? throw new TaskNotFoundException($"User {userId} is not a member of this project.");

        if (member.Role == ProjectMemberRole.Owner)
        {
            var allMembers = await projectMemberRepository.GetForProjectAsync(projectId);
            if (allMembers.Count(m => m.Role == ProjectMemberRole.Owner) <= 1)
            {
                throw new ValidationException("Cannot remove the last Owner of a project.");
            }
        }

        projectMemberRepository.Remove(member);
        await db.SaveChangesAsync();

        await mediator.Publish(new ProjectMemberRemovedNotification(projectId, userId, originConnectionId));
    }

    private static ProjectMemberDto ToDto(ProjectMember member) =>
        new(member.ProjectId, member.UserId, member.Role, member.JoinedAt, ToUserDto(member.User));

    private static UserSummaryDto ToUserDto(User user) => new(user.Id, user.Username, user.Email, user.CreatedAt);
}
