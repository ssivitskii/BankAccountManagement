using Banking.Application;

namespace Banking.Api.Contracts;

public sealed record TransferResponse(
    Guid Id,
    Guid FromAccountId,
    Guid ToAccountId,
    Guid InitiatedByUserId,
    decimal Amount,
    DateTimeOffset OccurredAt,
    bool IsReplay)
{
    public static TransferResponse FromApplication(TransferDetails details)
    {
        return new TransferResponse(
            details.Id,
            details.SourceAccountId,
            details.DestinationAccountId,
            details.InitiatedByUserId,
            details.Amount,
            details.OccurredAt,
            details.IsReplay);
    }
}
