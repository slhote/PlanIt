using MediatR;

namespace PlanIt.Api.Application.Realtime;

// Content-only, lightweight: fired for edits that don't change a card's position/column (title,
// description, tags, assignee) — clients just invalidate their cache for this item rather than
// getting a full payload to render immediately.
public record WorkItemUpdatedNotification(Guid ProjectId, Guid WorkItemId, string? OriginConnectionId) : INotification;
