using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Application;
using PlanIt.Api.Application.Auth;
using PlanIt.Api.Contracts.Projects;

namespace PlanIt.Api.Controllers;

[ApiController]
[Route("projects")]
public class ProjectsController(ProjectService projectService, ICurrentUserAccessor currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetMyProjects() =>
        Ok(await projectService.GetForUserAsync(currentUser.UserId));

    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ProjectBoardDto>> GetBoard(Guid projectId) =>
        Ok(await projectService.GetBoardAsync(projectId));
}
