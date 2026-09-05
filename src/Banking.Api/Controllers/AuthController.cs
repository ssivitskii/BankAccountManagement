using Banking.Api.Contracts;
using Banking.Application;
using Banking.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Banking.Api.Controllers;

[ApiController]
[EnableRateLimiting(AuthRateLimitOptions.PolicyName)]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        AuthResult result = await _authService.RegisterAsync(request.Username, request.Password, cancellationToken);
        return Created($"/api/users/{result.UserId}", AuthResponse.FromApplication(result));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        AuthResult result = await _authService.LoginAsync(request.Username, request.Password, cancellationToken);
        return Ok(AuthResponse.FromApplication(result));
    }
}
