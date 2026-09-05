namespace Banking.Domain;

public readonly record struct Money
{
    // Keeps cent values below JavaScript's maximum safe integer for exact browser round-trips.
    public const decimal MaximumAmount = 9_000_000_000_000.00m;

    public Money(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Money cannot be negative.");
        if (decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Money cannot have more than two decimal places.");
        if (amount > MaximumAmount)
            throw new ArgumentOutOfRangeException(nameof(amount), $"Money cannot exceed {MaximumAmount:F2}.");

        Amount = amount;
    }

    public decimal Amount { get; }

    public static Money Zero => new(0);

    public static Money operator +(Money left, Money right)
    {
        return new Money(left.Amount + right.Amount);
    }

    public static Money operator -(Money left, Money right)
    {
        return new Money(left.Amount - right.Amount);
    }
}
