using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
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
using PlanIt.Api.Hubs;
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
builder.Services.AddScoped<ProjectMemberService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, ClaimsCurrentUserAccessor>();

// PasswordHasher<T> (Microsoft.AspNetCore.Identity, part of the ASP.NET Core shared framework —
// no extra package needed): PBKDF2 with adaptive iteration counts, versioned hash format. Chosen
// over BCrypt.Net-Next since it adds no new dependency (planit-api-contracts-backend.md §4).
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddSignalR()
    // SignalR's hub protocol has its own JsonSerializerOptions, separate from the one
    // AddControllers().AddJsonOptions() configures for REST responses above — without this,
    // enums broadcast over the hub come through as numbers (0, 1, ...) while the REST API sends
    // them as strings ("ToDo", "InProgress", ...), breaking the assumption that both share one
    // wire format.
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Chosen deliberately over direct IRealtimeNotifier injection, despite today's 1-consumer-
// per-event reality: a second consumer (audit log, cache invalidation) becomes a new handler
// class with zero changes to the publishing service, and IPipelineBehavior is a documented seam
// for later cross-cutting concerns (planit-api-contracts-backend.md §5). MediatR moved to a
// commercial license at v13 for commercial use above a revenue threshold — fine for this
// portfolio/non-commercial project, worth revisiting if that ever changes.
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

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
        // Without this, ASP.NET Core remaps short JWT claim names ("sub", ...) to long legacy
        // ClaimTypes URIs on the way in, so a lookup for JwtRegisteredClaimNames.Sub against
        // context.User silently finds nothing even though the token validated fine. Keep the
        // claims exactly as JwtTokenService minted them.
        options.MapInboundClaims = false;

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

        // SignalR's WebSocket upgrade can't set an Authorization header, so the token travels as
        // an "access_token" query-string param instead (planit-api-contracts-backend.md §5) — the
        // standard pattern for this, scoped to only the hub path so it doesn't weaken normal
        // Bearer-header validation for REST calls.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });

// ProjectMember policy: gates project-scoped routes to members only, 404 (not 403) for everyone
// else via ProjectMember404ResultHandler below (planit-api-contracts-backend.md §7). This is a
// deliberate, acknowledged exception to "repository access is service-layer-only" — see
// ProjectMemberAuthorizationHandler's own comment for the rationale.
builder.Services.AddAuthorization(options =>
    options.AddPolicy("ProjectMember", policy => policy.Requirements.Add(new ProjectMemberRequirement())));
// Scoped, not Singleton — it depends on IProjectMemberRepository, which is scoped.
builder.Services.AddScoped<IAuthorizationHandler, ProjectMemberAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProjectMember404ResultHandler>();

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
builder.Services.AddExceptionHandler<InvalidRefreshTokenExceptionHandler>();
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
app.MapHub<PlanItHub>("/hub");

app.Run();

// Exposed for WebApplicationFactory<Program> in PlanIt.Api.Tests.
public partial class Program
{
}
