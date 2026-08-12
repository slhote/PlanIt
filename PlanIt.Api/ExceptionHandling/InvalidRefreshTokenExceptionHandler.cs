using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Domain.Exceptions;

namespace PlanIt.Api.ExceptionHandling;

// Maps to 401 Unauthorized (planit-api-contracts-backend.md §4).
public class InvalidRefreshTokenExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<InvalidRefreshTokenExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not InvalidRefreshTokenException invalidRefreshTokenException)
        {
            return false;
        }

        logger.LogWarning(invalidRefreshTokenException, "Refresh token rejected.");

        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = invalidRefreshTokenException.Message,
            },
        });
    }
}
