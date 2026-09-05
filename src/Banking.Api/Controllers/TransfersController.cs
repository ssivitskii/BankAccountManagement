using Banking.Api.Contracts;
using Banking.Application;
using Banking.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transfers")]
public sealed class TransfersController : ControllerBase
{
    private readonly CurrentActorAccessor _actors;
    private readonly ITransferService _transfers;

    public TransfersController(ITransferService transfers, CurrentActorAccessor actors)
    {
        _transfers = transfers;
        _actors = actors;
    }

    [HttpPost]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransferResponse>> Create(
        CreateTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request.FromAccountId == Guid.Empty || request.ToAccountId == Guid.Empty)
            throw new ArgumentException("Both account IDs are required.");
        if (idempotencyKey is null)
            throw new ArgumentException("Idempotency-Key header is required.");

        TransferDetails result = await _transfers.TransferAsync(
            _actors.Get(User),
            request.FromAccountId,
            request.ToAccountId,
            request.Amount,
            idempotencyKey,
            cancellationToken);
        var response = TransferResponse.FromApplication(result);
        return result.IsReplay ? Ok(response) : StatusCode(StatusCodes.Status201Created, response);
    }
}
