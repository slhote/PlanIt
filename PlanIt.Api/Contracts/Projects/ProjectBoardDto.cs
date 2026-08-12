using PlanIt.Api.Contracts.WorkItems;

namespace PlanIt.Api.Contracts.Projects;

public record ProjectBoardDto(ProjectDto Project, IReadOnlyList<WorkItemDto> WorkItems);
