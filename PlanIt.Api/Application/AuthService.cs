using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PlanIt.Api.Application.Auth;
using PlanIt.Api.Contracts.Auth;
using PlanIt.Api.Contracts.Users;
using PlanIt.Api.Data;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Exceptions;
using PlanIt.Api.Domain.Repositories;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Application;

// RefreshAsync/LogoutAsync (rotation + reuse-detection state machine, planit-api-contracts-backend.md
// §4) land in step 4. This step only issues an initial refresh token on register/login — the
// RefreshToken repository already exists, so there's no reason to withhold that row until the
// endpoint that later rotates it is built.
public class AuthService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    PlanItDbContext db,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions)
{
    private const int MinimumPasswordLength = 8;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ValidationException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ValidationException("Email is required.");
        }

        if (request.Password.Length < MinimumPasswordLength)
        {
            throw new ValidationException($"Password must be at least {MinimumPasswordLength} characters.");
        }

        if (await userRepository.GetByUsernameAsync(request.Username) is not null)
        {
            throw new ValidationException($"Username '{request.Username}' is already taken.");
        }

        if (await userRepository.GetByEmailAsync(request.Email) is not null)
        {
            throw new ValidationException($"Email '{request.Email}' is already registered.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        userRepository.Add(user);
        await db.SaveChangesAsync();

        return await IssueSessionAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await userRepository.GetByUsernameAsync(request.UsernameOrEmail)
            ?? await userRepository.GetByEmailAsync(request.UsernameOrEmail);

        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw new InvalidCredentialsException("Invalid username/email or password.");
        }

        return await IssueSessionAsync(user);
    }

    private async Task<AuthResponse> IssueSessionAsync(User user)
    {
        var (accessToken, expiresInSeconds) = jwtTokenService.CreateAccessToken(user);

        var rawRefreshToken = RefreshTokenGenerator.CreateRawToken();
        refreshTokenRepository.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = RefreshTokenGenerator.Hash(rawRefreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenExpirationDays),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var userDto = new UserSummaryDto(user.Id, user.Username, user.Email, user.CreatedAt);
        return new AuthResponse(userDto, accessToken, expiresInSeconds, rawRefreshToken);
    }
}
