namespace Banking.Application;

public sealed record StatementDetails(
    Guid AccountId,
    DateTimeOffset FromInclusive,
    DateTimeOffset ToExclusive,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<StatementOperationDetails> Operations);
