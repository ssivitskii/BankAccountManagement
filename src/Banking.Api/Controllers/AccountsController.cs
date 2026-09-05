using Banking.Api.Contracts;
using Banking.Application;
using Banking.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public sealed class AccountsController : ControllerBase
{
    private readonly IBankingService _banking;
    private readonly CurrentActorAccessor _actors;

    public AccountsController(IBankingService banking, CurrentActorAccessor actors)
    {
        _banking = banking;
        _actors = actors;
    }

    [HttpPost]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AccountResponse>> Create(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        Actor actor = _actors.Get(User);
        AccountDetails account = await _banking.CreateAccountAsync(
            actor,
            request.Number,
            request.InitialBalance,
            request.OwnerId,
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = account.Id }, AccountResponse.FromApplication(account));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        AccountDetails account = await _banking.GetAccountAsync(_actors.Get(User), id, cancellationToken);
        return Ok(AccountResponse.FromApplication(account));
    }

    [HttpGet("{id:guid}/balance")]
    [ProducesResponseType<BalanceResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BalanceResponse>> GetBalance(Guid id, CancellationToken cancellationToken)
    {
        decimal balance = await _banking.GetBalanceAsync(_actors.Get(User), id, cancellationToken);
        return Ok(new BalanceResponse(balance));
    }

    [HttpPost("{id:guid}/deposit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deposit(Guid id, AmountRequest request, CancellationToken cancellationToken)
    {
        await _banking.DepositAsync(_actors.Get(User), id, request.Amount, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/withdraw")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Withdraw(Guid id, AmountRequest request, CancellationToken cancellationToken)
    {
        await _banking.WithdrawAsync(_actors.Get(User), id, request.Amount, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/operations")]
    [ProducesResponseType<OperationPageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationPageResponse>> GetOperations(
        Guid id,
        CancellationToken cancellationToken,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null)
    {
        OperationPage page = await _banking.GetOperationPageAsync(
            _actors.Get(User),
            id,
            limit,
            cursor,
            cancellationToken);
        return Ok(OperationPageResponse.FromApplication(page));
    }
}
