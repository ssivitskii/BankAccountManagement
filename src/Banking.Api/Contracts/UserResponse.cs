using Banking.Domain;

namespace Banking.Api.Contracts;

public sealed record UserResponse(Guid Id, string Username, UserRole Role);
