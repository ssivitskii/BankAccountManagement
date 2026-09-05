using Banking.Application;

namespace Banking.Api.Contracts;

public sealed record OperationPageResponse(
    IReadOnlyList<OperationResponse> Items,
    string? NextCursor)
{
    public static OperationPageResponse FromApplication(OperationPage page)
    {
        return new OperationPageResponse(
            page.Items.Select(OperationResponse.FromApplication).ToArray(),
            page.NextCursor);
    }
}
