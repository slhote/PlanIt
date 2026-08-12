using PlanIt.Api.Contracts.Users;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Exceptions;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Application;

public class UserService(IUserRepository userRepository)
{
    public async Task<IReadOnlyList<UserSummaryDto>> SearchAsync(string term, int take = 20)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        var users = await userRepository.SearchAsync(term, take);
        return users.Select(ToDto).ToList();
    }

    public async Task<UserSummaryDto> GetByIdAsync(Guid id)
    {
        var user = await userRepository.GetByIdAsync(id)
            ?? throw new TaskNotFoundException($"User {id} not found.");
        return ToDto(user);
    }

    private static UserSummaryDto ToDto(User user) => new(user.Id, user.Username, user.Email, user.CreatedAt);
}
