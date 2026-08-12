using MediatR;

namespace PlanIt.Api.Application.Realtime;

// DeletedIds includes the requested work item plus every cascaded child (a Feature delete takes
// its Tasks with it) — full payload, so clients can remove every affected card immediately.
public record WorkItemDeletedNotification(Guid ProjectId, IReadOnlyList<Guid> DeletedIds, string? OriginConnectionId) : INotification;
