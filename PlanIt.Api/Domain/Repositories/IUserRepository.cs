using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);

    // Paginated, filter-as-you-type per the master plan's user search/lookup endpoint scope.
    Task<IReadOnlyList<User>> SearchAsync(string term, int take = 20);

    void Add(User user);
}
