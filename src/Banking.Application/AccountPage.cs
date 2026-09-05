namespace Banking.Application;

public sealed record AccountPage(IReadOnlyList<AccountDetails> Items, string? NextCursor);
