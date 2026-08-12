namespace PlanIt.Api.Domain.Exceptions;

// Maps to 401 Unauthorized (planit-api-contracts-backend.md §4) — unknown, already-revoked
// (reuse), or expired refresh token.
public class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException(string message) : base(message)
    {
    }
}
