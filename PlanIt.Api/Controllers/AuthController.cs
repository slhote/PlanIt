using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Application;
using PlanIt.Api.Contracts.Auth;

namespace PlanIt.Api.Controllers;

// Thin pass-through to AuthService — never touches IUserRepository/IRefreshTokenRepository
// directly (strict layering, planit-persistence-wiring.md). /auth/refresh and /auth/logout land
// in step 4 alongside the rotation/reuse-detection logic they depend on.
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
}
