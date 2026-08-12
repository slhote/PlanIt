namespace PlanIt.Api.Domain.Entities;

public class User
{
    private string _username = string.Empty;
    private string _email = string.Empty;

    public Guid Id { get; set; }

    public string Username
    {
        get => _username;
        set => _username = value.ToLowerInvariant();
    }

    public string Email
    {
        get => _email;
        set => _email = value.ToLowerInvariant();
    }

    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
