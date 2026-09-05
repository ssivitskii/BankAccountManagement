namespace Banking.Application.Abstractions;

public interface ITransferService
{
    Task<TransferDetails> TransferAsync(
        Actor actor,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
