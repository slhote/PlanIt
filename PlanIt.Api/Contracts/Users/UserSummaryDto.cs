namespace PlanIt.Api.Contracts.Users;

public record UserSummaryDto(Guid Id, string Username, string Email, DateTimeOffset CreatedAt);
