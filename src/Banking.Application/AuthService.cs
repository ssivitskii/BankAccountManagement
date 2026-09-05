using Banking.Application.Abstractions;
using Banking.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Banking.Application;

public sealed class AuthService : IAuthService
{
    private static readonly User DummyUser = new("timing-dummy", UserRole.Customer);
    private static readonly string DummyPasswordHash = new PasswordHasher<User>()
        .HashPassword(DummyUser, "not-a-real-password");

    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher,
        ITokenService tokens,
        ILogger<AuthService> logger)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        ValidatePassword(password);
        if (await _users.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false) is not null)
            throw new ConflictException("The username is already registered.");
        var user = new User(username, UserRole.Customer);
        user.SetPasswordHash(_passwordHasher.HashPassword(user, password));
        _users.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Customer {UserId} registered", user.Id);
        return _tokens.Create(user);
    }

    public async Task<AuthResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        User? user = await _users.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            _passwordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash, password);
            _logger.LogWarning("Login failed for an unknown account");
            throw new AuthenticationFailedException();
        }

        PasswordVerificationResult result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login failed for user {UserId}", user.Id);
            throw new AuthenticationFailedException();
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(_passwordHasher.HashPassword(user, password));
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Login succeeded for user {UserId}", user.Id);
        return _tokens.Create(user);
    }

    internal static void ValidatePassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (password.Length < 8)
            throw new ArgumentException("Password must contain at least eight characters.", nameof(password));
    }
}
