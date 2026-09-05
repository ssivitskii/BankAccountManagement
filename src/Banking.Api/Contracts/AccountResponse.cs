using Banking.Application;

namespace Banking.Api.Contracts;

public sealed record AccountResponse(Guid Id, Guid OwnerId, string Number, decimal Balance)
{
    public static AccountResponse FromApplication(AccountDetails details)
    {
        return new AccountResponse(details.Id, details.OwnerId, details.Number, details.Balance);
    }
}
