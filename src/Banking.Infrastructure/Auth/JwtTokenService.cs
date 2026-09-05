using Banking.Application;
using Banking.Application.Abstractions;
using Banking.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Banking.Infrastructure.Auth;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public AuthResult Create(User user)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset expires = now.AddMinutes(_options.LifetimeMinutes);
        System.Security.Claims.Claim[] claims =
        [
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new System.Security.Claims.Claim("role", user.Role.ToString()),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        ];
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now.UtcDateTime,
            expires.UtcDateTime,
            credentials);
        string accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthResult(accessToken, expires, user.Id, user.Username, user.Role);
    }
}
