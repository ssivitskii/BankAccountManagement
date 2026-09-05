using Banking.Domain;

namespace Banking.UnitTests;

public sealed class MoneyTests
{
    [Fact]
    public void MaximumAmountIsAcceptedAndOneMoreCentIsRejected()
    {
        var maximum = new Money(Money.MaximumAmount);

        Assert.Equal(Money.MaximumAmount, maximum.Amount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(Money.MaximumAmount + 0.01m));
    }

    [Fact]
    public void CreditThatWouldExceedMaximumLeavesBalanceUnchanged()
    {
        var account = new Account(Guid.NewGuid(), "ACCOUNT-MAX", new Money(Money.MaximumAmount));

        Assert.Throws<ArgumentOutOfRangeException>(() => account.Credit(new Money(0.01m)));

        Assert.Equal(Money.MaximumAmount, account.Balance.Amount);
    }
}
