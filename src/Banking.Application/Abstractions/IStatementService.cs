namespace Banking.Application.Abstractions;

public interface IStatementService
{
    Task<StatementDetails> GetStatementAsync(
        Actor actor,
        Guid accountId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken);
}
