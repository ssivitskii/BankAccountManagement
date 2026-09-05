using Banking.Application;
using Banking.Domain;

namespace Banking.Api.Contracts;

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Username,
    UserRole Role)
{
    public static AuthResponse FromApplication(AuthResult result)
    {
        return new AuthResponse(
            result.AccessToken,
            result.ExpiresAt,
            result.UserId,
            result.Username,
            result.Role);
    }
}
