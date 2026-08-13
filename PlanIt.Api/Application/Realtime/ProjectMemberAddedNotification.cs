using MediatR;

namespace PlanIt.Api.Application.Realtime;

public record ProjectMemberAddedNotification(Guid ProjectId, Guid UserId, string? OriginConnectionId) : INotification;
