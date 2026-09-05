using Banking.Application;
using Banking.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Banking.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly BankingDbContext _context;

    public EfUnitOfWork(BankingDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("A resource with the same unique value already exists.");
        }
    }
}
