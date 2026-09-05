using Banking.Api.Contracts;
using Banking.Application.Abstractions;
using Banking.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/users")]
public sealed class AdminController : ControllerBase
{
    private readonly IUserManagementService _users;

    public AdminController(IUserManagementService users)
    {
        _users = users;
    }

    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        Guid id = await _users.CreateAsync(request.Username, request.Password, request.Role, cancellationToken);
        return Created($"/api/users/{id}", new UserResponse(id, request.Username, request.Role));
    }
}
