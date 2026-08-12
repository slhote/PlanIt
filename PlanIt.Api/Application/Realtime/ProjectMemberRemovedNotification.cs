using MediatR;

namespace PlanIt.Api.Application.Realtime;

public record ProjectMemberRemovedNotification(Guid ProjectId, Guid UserId, string? OriginConnectionId) : INotification;
