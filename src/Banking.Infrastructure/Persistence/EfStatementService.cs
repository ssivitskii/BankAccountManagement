using Banking.Application;
using Banking.Application.Abstractions;
using Banking.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Banking.Infrastructure.Persistence;

public sealed class EfStatementService : IStatementService
{
    public const int MaximumOperations = 10000;
    private static readonly TimeSpan MaximumRange = TimeSpan.FromDays(366);
    private readonly BankingDbContext _context;
    private readonly TimeProvider _timeProvider;

    public EfStatementService(BankingDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<StatementDetails> GetStatementAsync(
        Actor actor,
        Guid accountId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken)
    {
        DateTimeOffset from = fromInclusive.ToUniversalTime();
        DateTimeOffset to = toExclusive.ToUniversalTime();
        if (from >= to)
            throw new ArgumentException("Statement 'from' must be earlier than 'to'.");
        if (to - from > MaximumRange)
            throw new ArgumentException("Statement date range cannot exceed 366 days.");
        if (to > _timeProvider.GetUtcNow())
            throw new ArgumentException("Statement 'to' cannot be in the future.");

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
                .ConfigureAwait(false);
        Account account = await _context.Accounts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Account was not found.");
        if (actor.Role != UserRole.Admin && actor.UserId != account.OwnerId)
            throw new ForbiddenException("The account belongs to another customer.");

        Operation[] statementOperations = await _context.Operations.AsNoTracking()
            .Where(operation => operation.AccountId == accountId
                && operation.OccurredAt >= from
                && operation.OccurredAt < to)
            .OrderBy(operation => operation.OccurredAt)
            .ThenBy(operation => operation.Id)
            .Take(MaximumOperations + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (statementOperations.Length > MaximumOperations)
            throw new ArgumentException($"Statement contains more than {MaximumOperations} operations.");

        decimal changeSinceOpening = await SumSignedOperationsAsync(accountId, from, cancellationToken)
            .ConfigureAwait(false);
        decimal changeSinceClosing = await SumSignedOperationsAsync(accountId, to, cancellationToken)
            .ConfigureAwait(false);
        decimal openingBalance = account.Balance.Amount - changeSinceOpening;
        decimal closingBalance = account.Balance.Amount - changeSinceClosing;
        StatementOperationDetails[] entries = statementOperations
            .Select(operation => new StatementOperationDetails(
                operation.Id,
                operation.Type,
                SignedAmount(operation),
                operation.OccurredAt,
                operation.TransferId))
            .ToArray();
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new StatementDetails(accountId, from, to, openingBalance, closingBalance, entries);
    }

    private static decimal SignedAmount(Operation operation)
    {
        return operation.Type is OperationType.Deposit or OperationType.TransferIn
            ? operation.Amount.Amount
            : -operation.Amount.Amount;
    }

    private Task<decimal> SumSignedOperationsAsync(
        Guid accountId,
        DateTimeOffset fromInclusive,
        CancellationToken cancellationToken)
    {
        return _context.Database.SqlQuery<decimal>(
                $"""
                SELECT COALESCE(SUM(
                    CASE WHEN "Type" IN ('Deposit', 'TransferIn') THEN "Amount" ELSE -"Amount" END), 0) AS "Value"
                FROM "Operations"
                WHERE "AccountId" = {accountId} AND "OccurredAt" >= {fromInclusive}
                """)
            .SingleAsync(cancellationToken);
    }
}
