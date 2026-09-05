using Banking.Application;

namespace Banking.Api.Contracts;

public sealed record StatementResponse(
    Guid AccountId,
    DateTimeOffset FromInclusive,
    DateTimeOffset ToExclusive,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<StatementOperationResponse> Operations)
{
    public static StatementResponse FromApplication(StatementDetails statement)
    {
        return new StatementResponse(
            statement.AccountId,
            statement.FromInclusive,
            statement.ToExclusive,
            statement.OpeningBalance,
            statement.ClosingBalance,
            statement.Operations.Select(operation => new StatementOperationResponse(
                operation.Id,
                operation.Type,
                operation.SignedAmount,
                operation.OccurredAt,
                operation.TransferId)).ToArray());
    }
}
