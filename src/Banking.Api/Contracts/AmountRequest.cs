using System.ComponentModel.DataAnnotations;

namespace Banking.Api.Contracts;

public sealed class AmountRequest
{
    [Range(typeof(decimal), "0.01", "9000000000000.00")]
    public decimal Amount { get; init; }
}
