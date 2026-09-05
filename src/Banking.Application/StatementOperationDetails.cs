using Banking.Domain;

namespace Banking.Application;

public sealed record StatementOperationDetails(
    Guid Id,
    OperationType Type,
    decimal SignedAmount,
    DateTimeOffset OccurredAt,
    Guid? TransferId);
