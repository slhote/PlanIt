using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Application;
using PlanIt.Api.Contracts.Users;

namespace PlanIt.Api.Controllers;

[ApiController]
[Route("users")]
public class UsersController(UserService userService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> Search([FromQuery] string q, [FromQuery] int take = 20) =>
        Ok(await userService.SearchAsync(q, take));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserSummaryDto>> GetById(Guid id) =>
        Ok(await userService.GetByIdAsync(id));
}
