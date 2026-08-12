using Microsoft.Extensions.Options;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Startup.Validation;

public class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            errors.Add($"{JwtOptions.SectionName}:{nameof(JwtOptions.Issuer)} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            errors.Add($"{JwtOptions.SectionName}:{nameof(JwtOptions.Audience)} is required.");
        }

        if (options.ExpirationMinutes <= 0)
        {
            errors.Add($"{JwtOptions.SectionName}:{nameof(JwtOptions.ExpirationMinutes)} must be greater than zero.");
        }

        if (options.RefreshTokenExpirationDays <= 0)
        {
            errors.Add($"{JwtOptions.SectionName}:{nameof(JwtOptions.RefreshTokenExpirationDays)} must be greater than zero.");
        }

        // HS256 keys should be at least 256 bits (32 bytes) — a short key defeats the algorithm.
        if (string.IsNullOrWhiteSpace(options.SigningKey) || options.SigningKey.Length < 32)
        {
            errors.Add(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)} is required and must be at least 32 characters. " +
                "Set it via `dotnet user-secrets set \"Jwt:SigningKey\" \"...\"` locally, or the Jwt__SigningKey env var when deployed.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
