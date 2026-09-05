namespace Banking.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string username, string password, CancellationToken cancellationToken);

    Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken);
}
