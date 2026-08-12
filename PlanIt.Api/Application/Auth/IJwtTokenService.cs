using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Application.Auth;

public interface IJwtTokenService
{
    (string AccessToken, int ExpiresInSeconds) CreateAccessToken(User user);
}
