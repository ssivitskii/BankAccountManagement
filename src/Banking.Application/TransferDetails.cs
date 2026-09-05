namespace Banking.Application;

public sealed record TransferDetails(
    Guid Id,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    Guid InitiatedByUserId,
    decimal Amount,
    DateTimeOffset OccurredAt,
    bool IsReplay);
