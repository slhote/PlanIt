using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Application;
using PlanIt.Api.Contracts.Auth;

namespace PlanIt.Api.Controllers;

// Thin pass-through to AuthService — never touches IUserRepository/IRefreshTokenRepository
// directly (strict layering, planit-persistence-wiring.md).
[ApiController]
[Route("auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var response = await authService.RegisterAsync(request);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request) =>
        Ok(await authService.LoginAsync(request));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request) =>
        Ok(await authService.RefreshAsync(request));

    // Revokes the specific refresh token in the request body, not "all sessions" derived from
    // the Bearer token's claims — [Authorize] here just requires a valid access token to call
    // logout at all, it isn't used to pick which session gets revoked.
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        await authService.LogoutAsync(request);
        return NoContent();
    }
}
