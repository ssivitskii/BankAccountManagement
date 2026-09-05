namespace Banking.Api;

public sealed class AuthRateLimitOptions
{
    public const string PolicyName = "authentication";
    public const string SectionName = "AuthRateLimit";

    public int PermitLimit { get; init; } = 20;

    public int WindowSeconds { get; init; } = 60;
}
