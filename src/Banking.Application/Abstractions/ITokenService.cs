using Banking.Domain;

namespace Banking.Application.Abstractions;

public interface ITokenService
{
    AuthResult Create(User user);
}
