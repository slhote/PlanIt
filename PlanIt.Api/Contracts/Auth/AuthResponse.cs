using PlanIt.Api.Contracts.Users;

namespace PlanIt.Api.Contracts.Auth;

public record AuthResponse(UserSummaryDto User, string AccessToken, int ExpiresInSeconds, string RefreshToken);
