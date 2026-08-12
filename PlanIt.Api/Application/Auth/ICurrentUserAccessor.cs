namespace PlanIt.Api.Application.Auth;

public interface ICurrentUserAccessor
{
    Guid UserId { get; }
}
