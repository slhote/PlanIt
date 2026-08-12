namespace PlanIt.Api.Contracts.Projects;

public record ProjectDto(Guid Id, string Name, string? Description, Guid CreatedByUserId, DateTimeOffset CreatedAt);
