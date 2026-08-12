using Microsoft.EntityFrameworkCore;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly PlanItDbContext _db;

    public UserRepository(PlanItDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public Task<User?> GetByUsernameAsync(string username) =>
        _db.Users.FirstOrDefaultAsync(u => u.Username == username);

    public Task<User?> GetByEmailAsync(string email) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<IReadOnlyList<User>> SearchAsync(string term, int take = 20) =>
        await _db.Users
            .Where(u => EF.Functions.ILike(u.Username, $"%{term}%") || EF.Functions.ILike(u.Email, $"%{term}%"))
            .OrderBy(u => u.Username)
            .Take(take)
            .ToListAsync();

    public void Add(User user) => _db.Users.Add(user);
}
