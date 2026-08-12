using Microsoft.Extensions.Options;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Startup.Validation;

public class CorsOptionsValidator : IValidateOptions<CorsOptions>
{
    public ValidateOptionsResult Validate(string? name, CorsOptions options)
    {
        if (options.AllowedOrigins.Count == 0)
        {
            return ValidateOptionsResult.Fail(
                $"{CorsOptions.SectionName}:{nameof(CorsOptions.AllowedOrigins)} must contain at least one origin.");
        }

        return ValidateOptionsResult.Success;
    }
}
