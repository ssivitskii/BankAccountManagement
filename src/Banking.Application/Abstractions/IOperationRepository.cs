using Banking.Domain;

namespace Banking.Application.Abstractions;

public interface IOperationRepository
{
    void Add(Operation operation);

    Task<IReadOnlyList<Operation>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Operation>> GetPageAsync(
        Guid accountId,
        int count,
        DateTimeOffset? beforeTimestamp,
        Guid? beforeId,
        CancellationToken cancellationToken);
}
