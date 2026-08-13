using PlanIt.Api.Contracts.Projects;
using PlanIt.Api.Data;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Exceptions;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Application;

public class ProjectService(
    IProjectRepository projectRepository,
    IProjectMemberRepository projectMemberRepository,
    IWorkItemRepository workItemRepository,
    PlanItDbContext db)
{
    public async Task<IReadOnlyList<ProjectDto>> GetForUserAsync(Guid userId)
    {
        var projects = await projectRepository.GetForUserAsync(userId);
        return projects.Select(ToDto).ToList();
    }

    // Membership is enforced by the [Authorize(Policy = "ProjectMember")] gate on the controller
    // action, not here (planit-api-contracts-backend.md §7) — a non-member request never reaches
    // this method.
    public async Task<ProjectBoardDto> GetBoardAsync(Guid projectId)
    {
        var project = await projectRepository.GetByIdAsync(projectId)
            ?? throw new TaskNotFoundException($"Project {projectId} not found.");

        var workItems = await workItemRepository.GetForProjectAsync(projectId);
        var workItemDtos = workItems.Select(w => WorkItemMapper.ToDto(w, db)).ToList();

        return new ProjectBoardDto(ToDto(project), workItemDtos);
    }

    // Inserts the Project row plus the creator's Owner ProjectMember row in the same
    // SaveChangesAsync — ProjectMember is the sole source of truth for access control, so a
    // project can never exist without its creator already being a member (planit-api-contracts-backend.md §3).
    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Project name is required.");
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        projectRepository.Add(project);

        projectMemberRepository.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = createdByUserId,
            Role = ProjectMemberRole.Owner,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();

        return ToDto(project);
    }

    private static ProjectDto ToDto(Project project) =>
        new(project.Id, project.Name, project.Description, project.CreatedByUserId, project.CreatedAt);
}
