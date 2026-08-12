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
}
