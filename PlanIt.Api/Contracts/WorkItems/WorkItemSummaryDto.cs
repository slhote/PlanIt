using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Contracts.WorkItems;

public record WorkItemSummaryDto(Guid Id, WorkItemType WorkItemType, string Title, WorkItemStatus Status);
