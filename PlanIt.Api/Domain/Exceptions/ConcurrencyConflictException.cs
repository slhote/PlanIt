namespace PlanIt.Api.Domain.Exceptions;

// Maps to 409 Conflict (planit-system-design-architecture.md §6). Thrown by the service layer
// wherever it calls SaveChangesAsync, translating a DbUpdateConcurrencyException (stale xmin
// write) — repositories don't call SaveChangesAsync themselves (planit-persistence-wiring.md §4:
// DbContext's scoped lifetime substitutes for a Unit-of-Work wrapper, one SaveChangesAsync per
// service call), so this is where the translation actually has to happen.
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
