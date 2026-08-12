using MediatR;
using PlanIt.Api.Contracts.WorkItems;

namespace PlanIt.Api.Application.Realtime;

public record WorkItemCreatedNotification(WorkItemDto Item, string? OriginConnectionId) : INotification;
