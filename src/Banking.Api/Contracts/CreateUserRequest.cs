using Banking.Domain;
using System.ComponentModel.DataAnnotations;

namespace Banking.Api.Contracts;

public sealed class CreateUserRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Username { get; init; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; init; } = UserRole.Customer;
}
