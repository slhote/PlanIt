using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PlanIt.Api.Application;
using PlanIt.Api.Application.Auth;
using PlanIt.Api.Data;
using PlanIt.Api.Data.Repositories;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Repositories;
using PlanIt.Api.ExceptionHandling;
using PlanIt.Api.HealthChecks;
using PlanIt.Api.Startup.Options;
using PlanIt.Api.Startup.Validation;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "Frontend";

// Add services to the container.

builder.Services.AddControllers()
    // Enums must serialize as their member names ("Feature", "ToDo", ...), not numbers, to match
    // the frontend's TS string-union types (planit-api-contracts-backend.md §2).
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<PlanItDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
builder.Services.AddScoped<IWorkItemRepository, WorkItemRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddScoped<AuthService>();

// TEMPORARY (planit-api-contracts-backend.md §8 step 1) — swap for a claims-based accessor once
// [Authorize] is turned on for real (step 3).
builder.Services.AddScoped<ICurrentUserAccessor, TemporaryCurrentUserAccessor>();

// PasswordHasher<T> (Microsoft.AspNetCore.Identity, part of the ASP.NET Core shared framework —
// no extra package needed): PBKDF2 with adaptive iteration counts, versioned hash format. Chosen
// over BCrypt.Net-Next since it adds no new dependency (planit-api-contracts-backend.md §4).
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<CorsOptions>, CorsOptionsValidator>();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // HS256 shared secret (planit-system-design-architecture.md §7) — a single service
        // both mints and verifies tokens, so no asymmetric RS256 split is needed.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.Zero,
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
            ?? new CorsOptions();

        // Credentials required for the SignalR handshake (access token in the negotiate/connect
        // request). GET/POST/PATCH/DELETE only — no PUT, PATCH is the idempotent mutation verb.
        policy.WithOrigins(corsOptions.AllowedOrigins.ToArray())
            .WithMethods("GET", "POST", "PATCH", "DELETE")
            .WithHeaders("Content-Type", "Authorization")
            .AllowCredentials();
    });
});

// Global exception handling via IExceptionHandler classes (planit-system-design-architecture.md
// §6). Typed handlers run in registration order; each returns false if the exception isn't its
// type, falling through to the next. Anything none of them catch gets a generic 500
// ProblemDetails from AddProblemDetails() below.
builder.Services.AddExceptionHandler<TaskNotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<ConcurrencyConflictExceptionHandler>();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<InvalidCredentialsExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        // Stack traces hidden in production, shown in development (System Design §6).
        if (builder.Environment.IsDevelopment() && context.Exception is not null)
        {
            context.ProblemDetails.Extensions["exception"] = context.Exception.ToString();
        }
    };
});

// Single GET /health endpoint confirming the API is up and the DB is reachable, for Azure
// deployment health probes (planit-system-design-architecture.md §8).
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

// Exception handling first, so it can catch anything thrown by later middleware.
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Exposed for WebApplicationFactory<Program> in PlanIt.Api.Tests.
public partial class Program
{
}
