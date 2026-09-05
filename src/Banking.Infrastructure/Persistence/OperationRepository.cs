using Banking.Application.Abstractions;
using Banking.Domain;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence;

public sealed class OperationRepository : IOperationRepository
{
    private readonly BankingDbContext _context;

    public OperationRepository(BankingDbContext context)
    {
        _context = context;
    }

    public void Add(Operation operation)
    {
        _context.Operations.Add(operation);
    }

    public async Task<IReadOnlyList<Operation>> GetByAccountIdAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        return await _context.Operations.AsNoTracking()
            .Where(operation => operation.AccountId == accountId)
            .OrderByDescending(operation => operation.OccurredAt)
            .ThenByDescending(operation => operation.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Operation>> GetPageAsync(
        Guid accountId,
        int count,
        DateTimeOffset? beforeTimestamp,
        Guid? beforeId,
        CancellationToken cancellationToken)
    {
        IQueryable<Operation> query = beforeTimestamp is null
            ? _context.Operations.AsNoTracking()
                .Where(operation => operation.AccountId == accountId)
                .OrderByDescending(operation => operation.OccurredAt)
                .ThenByDescending(operation => operation.Id)
                .Take(count)
            : _context.Operations.FromSqlInterpolated(
                    $"""
                    SELECT * FROM "Operations"
                    WHERE "AccountId" = {accountId}
                      AND ("OccurredAt", "Id") < ({beforeTimestamp.Value}, {beforeId!.Value})
                    ORDER BY "OccurredAt" DESC, "Id" DESC
                    LIMIT {count}
                    """)
                .AsNoTracking();
        return await query.ToArrayAsync(cancellationToken).ConfigureAwait(false);
    }
}
