using Banking.Application.Abstractions;
using Banking.Domain;
using Microsoft.AspNetCore.Identity;

namespace Banking.Application;

public sealed class UserManagementService : IUserManagementService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserManagementService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<Guid> CreateAsync(
        string username,
        string password,
        UserRole role,
        CancellationToken cancellationToken)
    {
        AuthService.ValidatePassword(password);
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        if (await _users.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false) is not null)
            throw new ConflictException("The username is already registered.");
        var user = new User(username, role);
        user.SetPasswordHash(_passwordHasher.HashPassword(user, password));
        _users.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return user.Id;
    }

    public async Task EnsureAdminAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (await _users.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false) is null)
            await CreateAsync(username, password, UserRole.Admin, cancellationToken).ConfigureAwait(false);
    }
}
