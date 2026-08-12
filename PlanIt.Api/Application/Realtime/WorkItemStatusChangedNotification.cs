using MediatR;
using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Application.Realtime;

public record WorkItemStatusChangedNotification(
    Guid ProjectId,
    Guid WorkItemId,
    WorkItemStatus OldStatus,
    WorkItemStatus NewStatus,
    string? OriginConnectionId) : INotification;
