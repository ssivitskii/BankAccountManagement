namespace Banking.Domain;

public sealed class Transfer
{
    public Transfer(
        Guid sourceAccountId,
        Guid destinationAccountId,
        Guid initiatedByUserId,
        Money amount,
        DateTimeOffset occurredAt)
    {
        if (sourceAccountId == Guid.Empty)
            throw new ArgumentException("Source account ID is required.", nameof(sourceAccountId));
        if (destinationAccountId == Guid.Empty)
            throw new ArgumentException("Destination account ID is required.", nameof(destinationAccountId));
        if (sourceAccountId == destinationAccountId)
            throw new ArgumentException("Source and destination accounts must differ.", nameof(destinationAccountId));
        if (initiatedByUserId == Guid.Empty)
            throw new ArgumentException("Initiating user ID is required.", nameof(initiatedByUserId));
        if (amount.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Transfer amount must be greater than zero.");

        Id = Guid.NewGuid();
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        InitiatedByUserId = initiatedByUserId;
        Amount = amount;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid SourceAccountId { get; private set; }

    public Guid DestinationAccountId { get; private set; }

    public Guid InitiatedByUserId { get; private set; }

    public Money Amount { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private Transfer()
    {
    }
}
