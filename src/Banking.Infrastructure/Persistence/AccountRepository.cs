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

    public async Task<IReadOnlyList<Account>> ListPageAsync(
        Guid? ownerId,
        int count,
        string? afterNumber,
        Guid? afterId,
        CancellationToken cancellationToken)
    {
        IQueryable<Account> query;
        if (ownerId is null && afterNumber is null)
        {
            query = _context.Accounts.FromSqlInterpolated(
                $"""
                SELECT * FROM "Accounts"
                ORDER BY "Number", "Id"
                LIMIT {count}
                """);
        }
        else if (ownerId is { } owner && afterNumber is null)
        {
            query = _context.Accounts.FromSqlInterpolated(
                $"""
                SELECT * FROM "Accounts"
                WHERE "OwnerId" = {owner}
                ORDER BY "Number", "Id"
                LIMIT {count}
                """);
        }
        else if (ownerId is null && afterNumber is { } number)
        {
            query = _context.Accounts.FromSqlInterpolated(
                $"""
                SELECT * FROM "Accounts"
                WHERE ("Number", "Id") > ({number}, {afterId!.Value})
                ORDER BY "Number", "Id"
                LIMIT {count}
                """);
        }
        else
        {
            query = _context.Accounts.FromSqlInterpolated(
                $"""
                SELECT * FROM "Accounts"
                WHERE "OwnerId" = {ownerId!.Value}
                  AND ("Number", "Id") > ({afterNumber}, {afterId!.Value})
                ORDER BY "Number", "Id"
                LIMIT {count}
                """);
        }

        return await query.AsNoTracking().ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
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
