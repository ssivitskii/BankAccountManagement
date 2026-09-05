using Banking.Domain;

namespace Banking.Api.Contracts;

public sealed record StatementOperationResponse(
    Guid Id,
    OperationType Type,
    decimal SignedAmount,
    DateTimeOffset OccurredAt,
    Guid? TransferId);
