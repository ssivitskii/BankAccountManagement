using Banking.Domain;

namespace Banking.Application;

public sealed record Actor(Guid UserId, UserRole Role);
