namespace Banking.Application;

public sealed record OperationPage(IReadOnlyList<OperationDetails> Items, string? NextCursor);
