namespace PlanIt.Api.Domain.Exceptions;

// Maps to 400 Bad Request (planit-system-design-architecture.md §6). For business-rule
// violations caught in the service layer — not model-binding errors, which [ApiController]
// already handles automatically.
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
}
