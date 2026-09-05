namespace Banking.Domain;

public sealed class User
{
    public User(string username, UserRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        Id = Guid.NewGuid();
        Username = username.Trim();
        Role = role;
        PasswordHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; }

    public string PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    private User()
    {
        Username = null!;
        PasswordHash = null!;
    }
}
