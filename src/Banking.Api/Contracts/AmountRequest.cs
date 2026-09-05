using System.ComponentModel.DataAnnotations;

namespace Banking.Api.Contracts;

public sealed class AmountRequest
{
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Amount { get; init; }
}
