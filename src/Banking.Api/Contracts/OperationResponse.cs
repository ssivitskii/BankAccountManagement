using Banking.Application;
using Banking.Domain;

namespace Banking.Api.Contracts;

public sealed record OperationResponse(
    Guid Id,
    OperationType Type,
    decimal Amount,
    DateTimeOffset OccurredAt,
    Guid? TransferId)
{
    public static OperationResponse FromApplication(OperationDetails details)
    {
        return new OperationResponse(details.Id, details.Type, details.Amount, details.OccurredAt, details.TransferId);
    }
}
