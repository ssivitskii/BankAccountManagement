namespace Banking.Domain;

public sealed class InsufficientFundsException : Exception
{
    public InsufficientFundsException()
        : base("The account has insufficient funds.")
    {
    }
}
