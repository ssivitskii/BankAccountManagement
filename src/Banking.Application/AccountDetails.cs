namespace Banking.Application;

public sealed record AccountDetails(Guid Id, Guid OwnerId, string Number, decimal Balance);
