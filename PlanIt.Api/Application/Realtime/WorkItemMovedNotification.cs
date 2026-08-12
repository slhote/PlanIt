using MediatR;
using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Application.Realtime;

// Deviates from the subplan's original OldParentId/NewParentId payload sketch: reparenting isn't
// actually supported by UpdateWorkItemRequest (ParentId is deliberately not patchable — see that
// DTO's own comment), so there's no "old parent -> new parent" transition to report. What this
// API actually supports moving is Order (drag-and-drop repositioning within/across status
// columns), so the payload carries that instead — full enough for a client to reposition the card
// without a refetch.
public record WorkItemMovedNotification(
    Guid ProjectId,
    Guid WorkItemId,
    Guid? ParentId,
    WorkItemStatus Status,
    double Order,
    string? OriginConnectionId) : INotification;
