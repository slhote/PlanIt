namespace PlanIt.Api.Startup.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; }
    public int RefreshTokenExpirationDays { get; set; }

    // HS256 shared secret. Never set in appsettings.json — User Secrets locally, the
    // Jwt__SigningKey env var when deployed (planit-system-design-architecture.md §7).
    public string SigningKey { get; set; } = string.Empty;
}
