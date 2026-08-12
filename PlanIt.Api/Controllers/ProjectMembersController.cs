using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Application;
using PlanIt.Api.Contracts.ProjectMembers;

namespace PlanIt.Api.Controllers;

[ApiController]
[Route("projects/{projectId:guid}/members")]
[Authorize(Policy = "ProjectMember")]
public class ProjectMembersController(ProjectMemberService projectMemberService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberDto>>> GetForProject(Guid projectId) =>
        Ok(await projectMemberService.GetForProjectAsync(projectId));

    [HttpPost]
    public async Task<ActionResult<ProjectMemberDto>> Add(
        Guid projectId,
        AddProjectMemberRequest request,
        [FromHeader(Name = "X-SignalR-Connection-Id")] string? originConnectionId = null)
    {
        var member = await projectMemberService.AddAsync(projectId, request, originConnectionId);
        return StatusCode(StatusCodes.Status201Created, member);
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(
        Guid projectId,
        Guid userId,
        [FromHeader(Name = "X-SignalR-Connection-Id")] string? originConnectionId = null)
    {
        await projectMemberService.RemoveAsync(projectId, userId, originConnectionId);
        return NoContent();
    }
}
