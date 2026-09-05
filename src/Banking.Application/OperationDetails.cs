using Banking.Domain;

namespace Banking.Application;

public sealed record OperationDetails(
    Guid Id,
    OperationType Type,
    decimal Amount,
    DateTimeOffset OccurredAt,
    Guid? TransferId = null);
