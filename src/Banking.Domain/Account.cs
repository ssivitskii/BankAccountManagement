namespace Banking.Domain;

public sealed class Account
{
    public Account(Guid ownerId, string number, Money initialBalance)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Owner ID is required.", nameof(ownerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Number = number.Trim();
        Balance = initialBalance;
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public string Number { get; private set; }

    public Money Balance { get; private set; }

    public void Credit(Money amount)
    {
        EnsurePositive(amount);
        Balance += amount;
    }

    public void Debit(Money amount)
    {
        EnsurePositive(amount);
        if (Balance.Amount < amount.Amount)
            throw new InsufficientFundsException();
        Balance -= amount;
    }

    private Account()
    {
        Number = null!;
    }

    private static void EnsurePositive(Money amount)
    {
        if (amount.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Transaction amount must be greater than zero.");
    }
}
