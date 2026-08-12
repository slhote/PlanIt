using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Application;
using PlanIt.Api.Application.Auth;
using PlanIt.Api.Contracts.Projects;

namespace PlanIt.Api.Controllers;

[ApiController]
[Route("projects")]
[Authorize]
public class ProjectsController(ProjectService projectService, ICurrentUserAccessor currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetMyProjects() =>
        Ok(await projectService.GetForUserAsync(currentUser.UserId));

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request)
    {
        var project = await projectService.CreateAsync(request, currentUser.UserId);
        return StatusCode(StatusCodes.Status201Created, project);
    }

    // ProjectMember policy gates this to members only, 404 (not 403) for everyone else
    // (planit-api-contracts-backend.md §7).
    [HttpGet("{projectId:guid}")]
    [Authorize(Policy = "ProjectMember")]
    public async Task<ActionResult<ProjectBoardDto>> GetBoard(Guid projectId) =>
        Ok(await projectService.GetBoardAsync(projectId));
}
