using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Contracts.WorkItems;

// PATCH semantics: a field left at its default (null) means "don't change it." This means
// Description can't be explicitly cleared and AssigneeId can't be explicitly unassigned via this
// endpoint yet — plain C# record deserialization can't distinguish "field omitted" from "field
// explicitly null" without extra machinery (a custom JsonConverter/Optional<T> wrapper), which
// isn't built yet. Known limitation, not an oversight — worth revisiting if/when the frontend
// needs to clear either field.
public record UpdateWorkItemRequest(
    string? Title = null,
    string? Description = null,
    WorkItemStatus? Status = null,
    Guid? AssigneeId = null,
    IReadOnlyList<string>? Tags = null,
    double? Order = null,
    uint? RowVersion = null);
