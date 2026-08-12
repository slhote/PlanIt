using PlanIt.Api.Contracts.Projects;
using PlanIt.Api.Data;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Exceptions;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Application;

public class ProjectService(IProjectRepository projectRepository, IWorkItemRepository workItemRepository, PlanItDbContext db)
{
    public async Task<IReadOnlyList<ProjectDto>> GetForUserAsync(Guid userId)
    {
        var projects = await projectRepository.GetForUserAsync(userId);
        return projects.Select(ToDto).ToList();
    }

    // Membership is not checked here yet — that's the [Authorize(Policy = "ProjectMember")]
    // gate landing in step 3 (planit-api-contracts-backend.md §7/§8). For now this returns any
    // project's board to any caller, which is fine while there's no auth to gate behind.
    public async Task<ProjectBoardDto> GetBoardAsync(Guid projectId)
    {
        var project = await projectRepository.GetByIdAsync(projectId)
            ?? throw new TaskNotFoundException($"Project {projectId} not found.");

        var workItems = await workItemRepository.GetForProjectAsync(projectId);
        var workItemDtos = workItems.Select(w => WorkItemMapper.ToDto(w, db)).ToList();

        return new ProjectBoardDto(ToDto(project), workItemDtos);
    }

    private static ProjectDto ToDto(Project project) =>
        new(project.Id, project.Name, project.Description, project.CreatedByUserId, project.CreatedAt);
}
