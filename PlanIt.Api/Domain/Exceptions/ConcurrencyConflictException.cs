namespace PlanIt.Api.Domain.Exceptions;

// Maps to 409 Conflict (planit-system-design-architecture.md §6). Thrown by repository
// implementations when a DbUpdateConcurrencyException indicates a stale xmin write, so the
// service layer never needs to know EF Core exists (planit-persistence-wiring.md §4).
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
