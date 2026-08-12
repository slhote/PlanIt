namespace PlanIt.Api.Domain.Entities;

// Rotation with reuse detection: a presented token whose RevokedAt is already set means the
// token was already rotated away — treat as a replay and revoke every active token for UserId.
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // The raw token is never stored, only its hash.
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public RefreshToken? ReplacedByToken { get; set; }
}
