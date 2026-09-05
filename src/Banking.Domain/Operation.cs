namespace Banking.Domain;

public sealed class Operation
{
    public Operation(
        Guid accountId,
        OperationType type,
        Money amount,
        DateTimeOffset? occurredAt = null,
        Guid? transferId = null)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account ID is required.", nameof(accountId));
        if (amount.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Operation amount must be greater than zero.");
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));
        bool transferOperation = type is OperationType.TransferOut or OperationType.TransferIn;
        if (transferOperation != transferId.HasValue || transferId == Guid.Empty)
            throw new ArgumentException("Transfer operations must reference a non-empty transfer ID.", nameof(transferId));
        Id = Guid.NewGuid();
        AccountId = accountId;
        Type = type;
        Amount = amount;
        OccurredAt = occurredAt ?? DateTimeOffset.UtcNow;
        TransferId = transferId;
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public OperationType Type { get; private set; }

    public Money Amount { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid? TransferId { get; private set; }

    private Operation()
    {
    }
}
