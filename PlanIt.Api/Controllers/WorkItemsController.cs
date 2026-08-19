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
public class WorkItemsController(WorkItemService workItemService, SimilarWorkItemsService similarWorkItemsService) : ControllerBase
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

    // Similar Tasks Suggestions (planit-similar-tasks-lexical-metadata.md): lexical + metadata
    // signals, scored on-demand, same-project candidates only.
    [HttpGet("{id:guid}/similar-tasks")]
    public async Task<ActionResult<IReadOnlyList<SimilarWorkItemDto>>> GetSimilarTasks(Guid projectId, Guid id) =>
        Ok(await similarWorkItemsService.GetSimilarAsync(projectId, id));

    // On-demand bulk recompute of semantic embeddings for every work item in the project
    // (planit-similar-tasks-semantic-embeddings.md) -- project-scoped, not item-scoped, hence the
    // absolute route override rather than nesting under /workitems/{id}. Gated by the same
    // ProjectMember policy as everything else in this controller -- no Owner-vs-Member
    // distinction, since no such role-based authorization exists anywhere else in this codebase
    // to reuse; see the PR description for this deviation from the design doc's "owner-only" line.
    [HttpPost("~/projects/{projectId:guid}/similar-tasks/recompute")]
    public async Task<ActionResult<RecomputeSimilarTasksResponse>> RecomputeAllSimilarTasks(Guid projectId) =>
        Ok(new RecomputeSimilarTasksResponse(await similarWorkItemsService.RecomputeAllAsync(projectId)));
}
