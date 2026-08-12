namespace PlanIt.Api.Domain.Exceptions;

// Maps to 401 Unauthorized (planit-api-contracts-backend.md §4).
public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message) : base(message)
    {
    }
}
