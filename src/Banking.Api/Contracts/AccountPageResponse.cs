using Banking.Application;

namespace Banking.Api.Contracts;

public sealed record AccountPageResponse(IReadOnlyList<AccountResponse> Items, string? NextCursor)
{
    public static AccountPageResponse FromApplication(AccountPage page)
    {
        return new AccountPageResponse(
            page.Items.Select(AccountResponse.FromApplication).ToArray(),
            page.NextCursor);
    }
}
