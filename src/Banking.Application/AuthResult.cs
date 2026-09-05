using Banking.Domain;

namespace Banking.Application;

public sealed record AuthResult(string AccessToken, DateTimeOffset ExpiresAt, Guid UserId, string Username, UserRole Role);
