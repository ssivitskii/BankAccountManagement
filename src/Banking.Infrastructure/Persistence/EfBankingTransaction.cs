using Banking.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Banking.Infrastructure.Persistence;

public sealed class EfBankingTransaction : IBankingTransaction
{
    private readonly BankingDbContext _context;

    public EfBankingTransaction(BankingDbContext context)
    {
        _context = context;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
        T result = await action(cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
