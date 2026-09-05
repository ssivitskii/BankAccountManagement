namespace Banking.Application.Abstractions;

public interface IBankingService
{
    Task<AccountPage> ListAccountPageAsync(
        Actor actor,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);

    Task<AccountDetails> CreateAccountAsync(
        Actor actor,
        string number,
        decimal initialBalance,
        Guid? ownerId,
        CancellationToken cancellationToken);

    Task<AccountDetails> GetAccountAsync(Actor actor, Guid accountId, CancellationToken cancellationToken);

    Task<decimal> GetBalanceAsync(Actor actor, Guid accountId, CancellationToken cancellationToken);

    Task DepositAsync(Actor actor, Guid accountId, decimal amount, CancellationToken cancellationToken);

    Task WithdrawAsync(Actor actor, Guid accountId, decimal amount, CancellationToken cancellationToken);

    Task<IReadOnlyList<OperationDetails>> GetOperationsAsync(
        Actor actor,
        Guid accountId,
        CancellationToken cancellationToken);

    Task<OperationPage> GetOperationPageAsync(
        Actor actor,
        Guid accountId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);
}
