using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Domain.Exceptions;

namespace PlanIt.Api.ExceptionHandling;

// Maps to 404 Not Found (planit-system-design-architecture.md §6).
public class TaskNotFoundExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<TaskNotFoundExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not TaskNotFoundException notFoundException)
        {
            return false;
        }

        logger.LogInformation(notFoundException, "Requested resource was not found.");

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = notFoundException.Message,
            },
        });
    }
}
