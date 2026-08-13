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

    // Rotation with reuse detection (planit-api-contracts-backend.md §4):
    // 1. Unknown token hash -> reject.
    // 2. Already-revoked token presented again -> treat as a replay, revoke every active token
    //    for that user (forces re-login everywhere), then reject.
    // 3. Expired -> reject.
    // 4. Otherwise: mint a new access+refresh token pair, revoke the presented one pointing at
    //    its replacement, and return the new pair.
    public async Task<AuthResponse> RefreshAsync(RefreshRequest request)
    {
        var tokenHash = RefreshTokenGenerator.Hash(request.RefreshToken);
        var token = await refreshTokenRepository.GetByTokenHashAsync(tokenHash);

        if (token is null)
        {
            throw new InvalidRefreshTokenException("Refresh token not recognized.");
        }

        if (token.RevokedAt is not null)
        {
            var activeTokens = await refreshTokenRepository.GetActiveForUserAsync(token.UserId);
            foreach (var activeToken in activeTokens)
            {
                activeToken.RevokedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync();

            throw new InvalidRefreshTokenException("Refresh token already used; all sessions for this user have been revoked.");
        }

        if (token.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new InvalidRefreshTokenException("Refresh token expired.");
        }

        var user = await userRepository.GetByIdAsync(token.UserId)
            ?? throw new InvalidRefreshTokenException("Refresh token's user no longer exists.");

        var (accessToken, expiresInSeconds) = jwtTokenService.CreateAccessToken(user);

        var rawRefreshToken = RefreshTokenGenerator.CreateRawToken();
        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = RefreshTokenGenerator.Hash(rawRefreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenExpirationDays),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        refreshTokenRepository.Add(newToken);

        token.RevokedAt = DateTimeOffset.UtcNow;
        token.ReplacedByTokenId = newToken.Id;

        await db.SaveChangesAsync();

        var userDto = new UserSummaryDto(user.Id, user.Username, user.Email, user.CreatedAt);
        return new AuthResponse(userDto, accessToken, expiresInSeconds, rawRefreshToken);
    }

    // Revokes only the specific presented refresh token, not every session for the user — an
    // already-revoked/unknown token is treated as an idempotent no-op, not an error, since the
    // end state ("this token no longer works") is already true.
    public async Task LogoutAsync(RefreshRequest request)
    {
        var tokenHash = RefreshTokenGenerator.Hash(request.RefreshToken);
        var token = await refreshTokenRepository.GetByTokenHashAsync(tokenHash);

        if (token is null || token.RevokedAt is not null)
        {
            return;
        }

        token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
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
