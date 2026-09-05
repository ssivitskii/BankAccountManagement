using Banking.Application.Abstractions;
using Banking.Domain;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence;

public sealed class AccountRepository : IAccountRepository
{
    private readonly BankingDbContext _context;

    public AccountRepository(BankingDbContext context)
    {
        _context = context;
    }

    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.Accounts.SingleOrDefaultAsync(account => account.Id == id, cancellationToken);
    }

    public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.Accounts
            .FromSqlInterpolated($"SELECT * FROM \"Accounts\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Account?> FindByNumberAsync(string number, CancellationToken cancellationToken)
    {
        return _context.Accounts.SingleOrDefaultAsync(account => account.Number == number, cancellationToken);
    }

    public void Add(Account account)
    {
        _context.Accounts.Add(account);
    }
}
