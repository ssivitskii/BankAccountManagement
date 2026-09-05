using Banking.Application;
using Banking.Application.Abstractions;
using Banking.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Banking.UnitTests;

public sealed class BankingServiceTests
{
    [Fact]
    public async Task CustomerAccountListContainsOnlyOwnedAccounts()
    {
        var customer = new User("customer", UserRole.Customer);
        var otherCustomer = new User("other-customer", UserRole.Customer);
        var owned = new Account(customer.Id, "ACCOUNT-OWNED", new Money(25));
        var foreign = new Account(otherCustomer.Id, "ACCOUNT-FOREIGN", new Money(50));
        var service = CreateService(new FakeAccountRepository(foreign, owned), customer);

        AccountPage page = await service.ListAccountPageAsync(
            new Actor(customer.Id, UserRole.Customer),
            100,
            null,
            CancellationToken.None);

        AccountDetails account = Assert.Single(page.Items);
        Assert.Equal(owned.Id, account.Id);
    }

    [Fact]
    public async Task AdministratorAccountListContainsEveryAccountInNumberOrder()
    {
        var administrator = new User("administrator", UserRole.Admin);
        var first = new Account(Guid.NewGuid(), "ACCOUNT-A", new Money(25));
        var second = new Account(Guid.NewGuid(), "ACCOUNT-B", new Money(50));
        var service = CreateService(new FakeAccountRepository(second, first), administrator);

        AccountPage page = await service.ListAccountPageAsync(
            new Actor(administrator.Id, UserRole.Admin),
            100,
            null,
            CancellationToken.None);

        Assert.Equal([first.Id, second.Id], page.Items.Select(account => account.Id));
    }

    [Fact]
    public async Task AccountPagesUseStableNumberAndIdCursor()
    {
        var administrator = new User("administrator", UserRole.Admin);
        var first = new Account(Guid.NewGuid(), "ACCOUNT-A", new Money(25));
        var second = new Account(Guid.NewGuid(), "ACCOUNT-A", new Money(50));
        var third = new Account(Guid.NewGuid(), "ACCOUNT-B", new Money(75));
        var service = CreateService(
            new FakeAccountRepository(third, first, second),
            administrator);
        Account[] expected = [.. new[] { first, second }
            .OrderBy(account => account.Id)
            .Append(third)];

        AccountPage firstPage = await service.ListAccountPageAsync(
            new Actor(administrator.Id, UserRole.Admin),
            2,
            null,
            CancellationToken.None);
        AccountPage secondPage = await service.ListAccountPageAsync(
            new Actor(administrator.Id, UserRole.Admin),
            2,
            firstPage.NextCursor,
            CancellationToken.None);

        Assert.Equal(expected[..2].Select(account => account.Id), firstPage.Items.Select(account => account.Id));
        Assert.NotNull(firstPage.NextCursor);
        Assert.Equal(expected[2].Id, Assert.Single(secondPage.Items).Id);
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task MultipleOperationsRemainInHistory()
    {
        var user = new User("customer", UserRole.Customer);
        var account = new Account(user.Id, "ACCOUNT-1", new Money(100));
        var accounts = new FakeAccountRepository(account);
        var operations = new FakeOperationRepository();
        var service = new BankingService(
            accounts,
            new FakeUserRepository(user),
            operations,
            new FakeUnitOfWork(),
            new FakeTransaction(),
            NullLogger<BankingService>.Instance);
        var actor = new Actor(user.Id, UserRole.Customer);

        await service.DepositAsync(actor, account.Id, 10, CancellationToken.None);
        await service.WithdrawAsync(actor, account.Id, 20, CancellationToken.None);
        IReadOnlyList<OperationDetails> history = await service.GetOperationsAsync(
            actor,
            account.Id,
            CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.Equal(90, account.Balance.Amount);
        Assert.Equal(2, history.Select(operation => operation.Id).Distinct().Count());
    }

    [Fact]
    public async Task CustomerCannotReadAnotherOwnersAccount()
    {
        var owner = new User("owner", UserRole.Customer);
        var account = new Account(owner.Id, "ACCOUNT-1", Money.Zero);
        var service = new BankingService(
            new FakeAccountRepository(account),
            new FakeUserRepository(owner),
            new FakeOperationRepository(),
            new FakeUnitOfWork(),
            new FakeTransaction(),
            NullLogger<BankingService>.Instance);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetAccountAsync(
            new Actor(Guid.NewGuid(), UserRole.Customer),
            account.Id,
            CancellationToken.None));
    }

    [Fact]
    public async Task FailedTransactionIsNotLoggedAsSuccessful()
    {
        var user = new User("customer", UserRole.Customer);
        var account = new Account(user.Id, "ACCOUNT-1", new Money(100));
        var logger = new CountingLogger();
        var service = new BankingService(
            new FakeAccountRepository(account),
            new FakeUserRepository(user),
            new FakeOperationRepository(),
            new FakeUnitOfWork(),
            new ThrowingAfterActionTransaction(),
            logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DepositAsync(
            new Actor(user.Id, UserRole.Customer),
            account.Id,
            10,
            CancellationToken.None));

        Assert.Equal(0, logger.Entries);
    }

    private static BankingService CreateService(IAccountRepository accounts, User user)
    {
        return new BankingService(
            accounts,
            new FakeUserRepository(user),
            new FakeOperationRepository(),
            new FakeUnitOfWork(),
            new FakeTransaction(),
            NullLogger<BankingService>.Instance);
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly Dictionary<Guid, Account> _accounts;

        public FakeAccountRepository(params Account[] accounts)
        {
            _accounts = accounts.ToDictionary(account => account.Id);
        }

        public Task<IReadOnlyList<Account>> ListPageAsync(
            Guid? ownerId,
            int count,
            string? afterNumber,
            Guid? afterId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Account> result = _accounts.Values
                .Where(account => ownerId is null || account.OwnerId == ownerId.Value)
                .Where(account => afterNumber is null
                    || string.CompareOrdinal(account.Number, afterNumber) > 0
                    || (string.Equals(account.Number, afterNumber, StringComparison.Ordinal)
                        && account.Id.CompareTo(afterId!.Value) > 0))
                .OrderBy(account => account.Number, StringComparer.Ordinal)
                .ThenBy(account => account.Id)
                .Take(count)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(_accounts.GetValueOrDefault(id));
        }

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(_accounts.GetValueOrDefault(id));
        }

        public Task<Account?> FindByNumberAsync(string number, CancellationToken cancellationToken)
        {
            return Task.FromResult(_accounts.Values.SingleOrDefault(
                account => string.Equals(account.Number, number, StringComparison.Ordinal)));
        }

        public void Add(Account account)
        {
            _accounts.Add(account.Id, account);
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly User _user;

        public FakeUserRepository(User user)
        {
            _user = user;
        }

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(id == _user.Id ? _user : null);
        }

        public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
        {
            return Task.FromResult(string.Equals(username, _user.Username, StringComparison.Ordinal) ? _user : null);
        }

        public void Add(User user)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeOperationRepository : IOperationRepository
    {
        private readonly List<Operation> _operations = [];

        public void Add(Operation operation)
        {
            _operations.Add(operation);
        }

        public Task<IReadOnlyList<Operation>> GetByAccountIdAsync(
            Guid accountId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Operation> result = _operations.Where(operation => operation.AccountId == accountId).ToArray();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Operation>> GetPageAsync(
            Guid accountId,
            int count,
            DateTimeOffset? beforeTimestamp,
            Guid? beforeId,
            CancellationToken cancellationToken)
        {
            IEnumerable<Operation> query = _operations
                .Where(operation => operation.AccountId == accountId)
                .OrderByDescending(operation => operation.OccurredAt)
                .ThenByDescending(operation => operation.Id);
            if (beforeTimestamp is not null)
            {
                query = query.Where(operation => operation.OccurredAt < beforeTimestamp
                    || (operation.OccurredAt == beforeTimestamp && operation.Id.CompareTo(beforeId!.Value) < 0));
            }

            IReadOnlyList<Operation> result = query.Take(count).ToArray();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTransaction : IBankingTransaction
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
        {
            return action(cancellationToken);
        }
    }

    private sealed class ThrowingAfterActionTransaction : IBankingTransaction
    {
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken)
        {
            await action(cancellationToken);
            throw new InvalidOperationException("Commit failed.");
        }
    }

    private sealed class CountingLogger : ILogger<BankingService>
    {
        public int Entries { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries++;
        }
    }
}
