using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Domain.Repositories;

public interface IRefreshTokenRepository
{
    // The hot lookup path on every refresh request (RefreshToken(TokenHash) is UNIQUE-indexed).
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);

    // For reuse-detection revocation: revoke every active token for a user at once.
    Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(Guid userId);

    void Add(RefreshToken token);
}
