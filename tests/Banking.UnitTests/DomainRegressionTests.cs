using Banking.Domain;

namespace Banking.UnitTests;

public sealed class DomainRegressionTests
{
    [Fact]
    public void OperationIdsAreNonEmptyAndUnique()
    {
        var accountId = Guid.NewGuid();

        var first = new Operation(accountId, OperationType.Deposit, new Money(10));
        var second = new Operation(accountId, OperationType.Deposit, new Money(20));

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void NegativeMoneyIsRejected(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(amount));
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(12.345)]
    public void MoneyWithMoreThanTwoDecimalPlacesIsRejected(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(amount));
    }

    [Fact]
    public void UndefinedUserRoleIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new User("customer", (UserRole)99));
    }

    [Fact]
    public void NegativeInitialBalanceIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Account(Guid.NewGuid(), "ACCOUNT-1", new Money(-1)));
    }

    [Fact]
    public void ZeroDepositAndWithdrawalAreRejected()
    {
        var account = new Account(Guid.NewGuid(), "ACCOUNT-1", new Money(100));

        Assert.Throws<ArgumentOutOfRangeException>(() => account.Credit(Money.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => account.Debit(Money.Zero));
        Assert.Equal(100, account.Balance.Amount);
    }

    [Fact]
    public void FailedDebitDoesNotMutateBalance()
    {
        var account = new Account(Guid.NewGuid(), "ACCOUNT-1", new Money(100));

        Assert.Throws<InsufficientFundsException>(() => account.Debit(new Money(101)));

        Assert.Equal(100, account.Balance.Amount);
    }

    [Fact]
    public void TransferAndLinkedOperationsEnforceLedgerInvariants()
    {
        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();
        var transfer = new Transfer(
            sourceId,
            destinationId,
            initiatorId,
            new Money(25),
            DateTimeOffset.UnixEpoch);
        var debit = new Operation(
            sourceId,
            OperationType.TransferOut,
            new Money(25),
            DateTimeOffset.UnixEpoch,
            transfer.Id);
        var credit = new Operation(
            destinationId,
            OperationType.TransferIn,
            new Money(25),
            DateTimeOffset.UnixEpoch,
            transfer.Id);

        Assert.NotEqual(Guid.Empty, transfer.Id);
        Assert.Equal(transfer.Id, debit.TransferId);
        Assert.Equal(transfer.Id, credit.TransferId);
        Assert.Throws<ArgumentException>(() => new Operation(
            sourceId,
            OperationType.TransferOut,
            new Money(1)));
        Assert.Throws<ArgumentException>(() => new Transfer(
            sourceId,
            sourceId,
            initiatorId,
            new Money(1),
            DateTimeOffset.UnixEpoch));
    }
}
