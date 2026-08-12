using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Domain.Exceptions;

namespace PlanIt.Api.ExceptionHandling;

// Maps to 409 Conflict (planit-system-design-architecture.md §6) — a stale xmin write.
public class ConcurrencyConflictExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ConcurrencyConflictExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ConcurrencyConflictException concurrencyException)
        {
            return false;
        }

        logger.LogWarning(concurrencyException, "Concurrency conflict on a stale write.");

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = concurrencyException.Message,
            },
        });
    }
}
