using Banking.Application.Abstractions;
using Banking.Domain;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Banking.Application;

public sealed class BankingService : IBankingService
{
    private readonly IAccountRepository _accounts;
    private readonly IUserRepository _users;
    private readonly IOperationRepository _operations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBankingTransaction _transaction;
    private readonly ILogger<BankingService> _logger;

    public BankingService(
        IAccountRepository accounts,
        IUserRepository users,
        IOperationRepository operations,
        IUnitOfWork unitOfWork,
        IBankingTransaction transaction,
        ILogger<BankingService> logger)
    {
        _accounts = accounts;
        _users = users;
        _operations = operations;
        _unitOfWork = unitOfWork;
        _transaction = transaction;
        _logger = logger;
    }

    public async Task<AccountDetails> CreateAccountAsync(
        Actor actor,
        string number,
        decimal initialBalance,
        Guid? ownerId,
        CancellationToken cancellationToken)
    {
        Guid actualOwnerId = actor.Role == UserRole.Admin
            ? ownerId ?? throw new ArgumentException("Owner ID is required for an administrator-created account.")
            : actor.UserId;
        if (actor.Role != UserRole.Admin && ownerId is not null && ownerId != actor.UserId)
            throw new ForbiddenException("Customers can only create their own accounts.");
        if (await _users.FindByIdAsync(actualOwnerId, cancellationToken).ConfigureAwait(false) is null)
            throw new NotFoundException("Account owner was not found.");
        if (await _accounts.FindByNumberAsync(number, cancellationToken).ConfigureAwait(false) is not null)
            throw new ConflictException("An account with this number already exists.");
        var account = new Account(actualOwnerId, number, new Money(initialBalance));
        _accounts.Add(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Account {AccountId} created for owner {OwnerId}", account.Id, actualOwnerId);
        return Map(account);
    }

    public async Task<AccountDetails> GetAccountAsync(Actor actor, Guid accountId, CancellationToken cancellationToken)
    {
        Account account = await FindAccountAsync(accountId, cancellationToken).ConfigureAwait(false);
        EnsureAccess(actor, account);
        return Map(account);
    }

    public async Task<decimal> GetBalanceAsync(Actor actor, Guid accountId, CancellationToken cancellationToken)
    {
        return (await GetAccountAsync(actor, accountId, cancellationToken).ConfigureAwait(false)).Balance;
    }

    public Task DepositAsync(Actor actor, Guid accountId, decimal amount, CancellationToken cancellationToken)
    {
        return MutateBalanceAsync(actor, accountId, new Money(amount), OperationType.Deposit, cancellationToken);
    }

    public Task WithdrawAsync(Actor actor, Guid accountId, decimal amount, CancellationToken cancellationToken)
    {
        return MutateBalanceAsync(actor, accountId, new Money(amount), OperationType.Withdrawal, cancellationToken);
    }

    public async Task<IReadOnlyList<OperationDetails>> GetOperationsAsync(
        Actor actor,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Account account = await FindAccountAsync(accountId, cancellationToken).ConfigureAwait(false);
        EnsureAccess(actor, account);
        IReadOnlyList<Operation> operations = await _operations.GetByAccountIdAsync(accountId, cancellationToken)
            .ConfigureAwait(false);
        return operations.Select(Map).ToArray();
    }

    public async Task<OperationPage> GetOperationPageAsync(
        Actor actor,
        Guid accountId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit), "Page limit must be between 1 and 100.");
        Account account = await FindAccountAsync(accountId, cancellationToken).ConfigureAwait(false);
        EnsureAccess(actor, account);
        (DateTimeOffset? timestamp, Guid? id) = DecodeCursor(cursor);
        IReadOnlyList<Operation> operations = await _operations.GetPageAsync(
            accountId,
            limit + 1,
            timestamp,
            id,
            cancellationToken).ConfigureAwait(false);
        Operation[] items = operations.Take(limit).ToArray();
        string? nextCursor = operations.Count > limit
            ? EncodeCursor(items[^1])
            : null;
        return new OperationPage(items.Select(Map).ToArray(), nextCursor);
    }

    private static void EnsureAccess(Actor actor, Account account)
    {
        if (actor.Role != UserRole.Admin && actor.UserId != account.OwnerId)
            throw new ForbiddenException("The account belongs to another customer.");
    }

    private static (DateTimeOffset? Timestamp, Guid? Id) DecodeCursor(string? cursor)
    {
        if (cursor is null)
            return (null, null);
        string[] parts = cursor.Split('.', StringSplitOptions.None);
        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long ticks)
            || !Guid.TryParseExact(parts[1], "N", out Guid id)
            || ticks < DateTimeOffset.MinValue.UtcTicks
            || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            throw new ArgumentException("The operation cursor is invalid.", nameof(cursor));
        }

        return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    private static string EncodeCursor(Operation operation)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{operation.OccurredAt.UtcTicks:x16}.{operation.Id:N}");
    }

    private static AccountDetails Map(Account account)
    {
        return new AccountDetails(account.Id, account.OwnerId, account.Number, account.Balance.Amount);
    }

    private static OperationDetails Map(Operation operation)
    {
        return new OperationDetails(
            operation.Id,
            operation.Type,
            operation.Amount.Amount,
            operation.OccurredAt,
            operation.TransferId);
    }

    private async Task MutateBalanceAsync(
        Actor actor,
        Guid accountId,
        Money amount,
        OperationType type,
        CancellationToken cancellationToken)
    {
        Operation operation = await _transaction.ExecuteAsync(
            async token =>
            {
                Account account = await _accounts.FindByIdForUpdateAsync(accountId, token).ConfigureAwait(false)
                    ?? throw new NotFoundException("Account was not found.");
                EnsureAccess(actor, account);
                if (type == OperationType.Deposit)
                    account.Credit(amount);
                else
                    account.Debit(amount);
                var pendingOperation = new Operation(account.Id, type, amount);
                _operations.Add(pendingOperation);
                return pendingOperation;
            },
            cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Operation {OperationId} of type {OperationType} recorded for account {AccountId}",
            operation.Id,
            type,
            operation.AccountId);
    }

    private async Task<Account> FindAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _accounts.FindByIdAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Account was not found.");
    }
}
