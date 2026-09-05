using Banking.Application.Abstractions;
using Banking.Domain;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly BankingDbContext _context;

    public UserRepository(BankingDbContext context)
    {
        _context = context;
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return _context.Users.SingleOrDefaultAsync(user => user.Username == username, cancellationToken);
    }

    public void Add(User user)
    {
        _context.Users.Add(user);
    }
}
