using Microsoft.EntityFrameworkCore;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Data.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly PlanItDbContext _db;

    public RefreshTokenRepository(PlanItDbContext db) => _db = db;

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash) =>
        _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(Guid userId) =>
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();

    public void Add(RefreshToken token) => _db.RefreshTokens.Add(token);
}
