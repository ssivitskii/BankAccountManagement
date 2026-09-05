namespace Banking.Application;

public sealed class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException()
        : base("Invalid username or password.")
    {
    }
}
