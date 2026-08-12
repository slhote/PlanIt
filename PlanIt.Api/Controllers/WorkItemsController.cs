using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Application;
using PlanIt.Api.Contracts.WorkItems;

namespace PlanIt.Api.Controllers;

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
    public async Task<ActionResult<WorkItemDto>> Create(Guid projectId, CreateWorkItemRequest request)
    {
        var workItem = await workItemService.CreateAsync(projectId, request);
        return StatusCode(StatusCodes.Status201Created, workItem);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<WorkItemDto>> Update(Guid projectId, Guid id, UpdateWorkItemRequest request) =>
        Ok(await workItemService.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<DeleteWorkItemResponse>> Delete(Guid projectId, Guid id) =>
        Ok(await workItemService.DeleteAsync(id));
}
