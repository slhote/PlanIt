namespace PlanIt.Api.Application.Auth;

// TEMPORARY (planit-api-contracts-backend.md §8 step 1): no real auth exists yet, so there's no
// JWT to read a "sub" claim from. Returns a fixed test user id so the read-only endpoints built
// in this step can be exercised end-to-end. Replace with a claims-based implementation reading
// the authenticated user's id once step 2/3 (real login + [Authorize]) lands — every consumer
// depends on the ICurrentUserAccessor interface, not this class, so that swap touches only DI
// registration in Program.cs.
public class TemporaryCurrentUserAccessor : ICurrentUserAccessor
{
    public static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid UserId => TestUserId;
}
