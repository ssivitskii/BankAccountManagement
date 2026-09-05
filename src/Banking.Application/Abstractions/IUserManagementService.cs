using Banking.Domain;

namespace Banking.Application.Abstractions;

public interface IUserManagementService
{
    Task<Guid> CreateAsync(string username, string password, UserRole role, CancellationToken cancellationToken);

    Task EnsureAdminAsync(string username, string password, CancellationToken cancellationToken);
}
