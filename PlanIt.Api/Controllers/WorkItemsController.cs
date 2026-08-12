using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Application;
using PlanIt.Api.Contracts.WorkItems;

namespace PlanIt.Api.Controllers;

// X-SignalR-Connection-Id: the frontend's current SignalR connection, so its own SignalR
// broadcast can exclude the client that made the REST call (planit-api-contracts-backend.md §5).
// Optional — absent when the caller has no live SignalR connection yet.
[ApiController]
[Route("projects/{projectId:guid}/workitems")]
[Authorize(Policy = "ProjectMember")]
public class WorkItemsController(WorkItemService workItemService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkItemDto>> GetById(Guid projectId, Guid id) =>
        Ok(await workItemService.GetByIdAsync(id));

    [HttpGet("{id:guid}/children")]
    public async Task<ActionResult<FeatureDetailDto>> GetChildren(Guid projectId, Guid id) =>
        Ok(await workItemService.GetFeatureDetailAsync(id));

    [HttpPost]
    public async Task<ActionResult<WorkItemDto>> Create(
        Guid projectId,
        CreateWorkItemRequest request,
        [FromHeader(Name = "X-SignalR-Connection-Id")] string? originConnectionId = null)
    {
        var workItem = await workItemService.CreateAsync(projectId, request, originConnectionId);
        return StatusCode(StatusCodes.Status201Created, workItem);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<WorkItemDto>> Update(
        Guid projectId,
        Guid id,
        UpdateWorkItemRequest request,
        [FromHeader(Name = "X-SignalR-Connection-Id")] string? originConnectionId = null) =>
        Ok(await workItemService.UpdateAsync(id, request, originConnectionId));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<DeleteWorkItemResponse>> Delete(
        Guid projectId,
        Guid id,
        [FromHeader(Name = "X-SignalR-Connection-Id")] string? originConnectionId = null) =>
        Ok(await workItemService.DeleteAsync(id, originConnectionId));

    // Route locked now so the URL contract is stable; implementation is subplan 8 (Similar Tasks
    // Suggestions), sequenced after Tags/Labels and seed data exist (planit-api-contracts-backend.md §8).
    [HttpGet("{id:guid}/similar-tasks")]
    public IActionResult GetSimilarTasks(Guid projectId, Guid id) =>
        StatusCode(StatusCodes.Status501NotImplemented);
}
