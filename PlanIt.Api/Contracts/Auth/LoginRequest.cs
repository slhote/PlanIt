namespace PlanIt.Api.Contracts.Auth;

public record LoginRequest(string UsernameOrEmail, string Password);
