using Banking.Domain;

namespace Banking.Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<Account?> FindByNumberAsync(string number, CancellationToken cancellationToken);

    void Add(Account account);
}
