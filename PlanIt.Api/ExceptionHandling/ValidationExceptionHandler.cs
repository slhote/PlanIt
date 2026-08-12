using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlanIt.Api.Domain.Exceptions;

namespace PlanIt.Api.ExceptionHandling;

// Maps to 400 Bad Request (planit-system-design-architecture.md §6) — a business-rule
// violation caught in the service layer, not a model-binding error ([ApiController] already
// handles those automatically as 400 ValidationProblemDetails).
public class ValidationExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ValidationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        logger.LogInformation(validationException, "Business-rule validation failed.");

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = validationException.Message,
            },
        });
    }
}
