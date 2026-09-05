namespace Banking.Infrastructure.Persistence;

internal sealed class IdempotencyRecord
{
    public Guid ActorId { get; set; }

    public string Scope { get; set; } = string.Empty;

    public string KeyHash { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public Guid? TransferId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
