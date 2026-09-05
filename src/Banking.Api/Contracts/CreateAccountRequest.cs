using System.ComponentModel.DataAnnotations;

namespace Banking.Api.Contracts;

public sealed class CreateAccountRequest
{
    [Required]
    [RegularExpression("^[A-Za-z0-9-]{4,34}$")]
    public string Number { get; init; } = string.Empty;

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal InitialBalance { get; init; }

    public Guid? OwnerId { get; init; }
}
