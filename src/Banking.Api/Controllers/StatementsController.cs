using Banking.Api.Contracts;
using Banking.Application;
using Banking.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts/{accountId:guid}")]
public sealed class StatementsController : ControllerBase
{
    private readonly CurrentActorAccessor _actors;
    private readonly IStatementService _statements;

    public StatementsController(IStatementService statements, CurrentActorAccessor actors)
    {
        _statements = statements;
        _actors = actors;
    }

    [HttpGet("statement")]
    [ProducesResponseType<StatementResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StatementResponse>> Get(
        Guid accountId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        StatementDetails statement = await _statements.GetStatementAsync(
            _actors.Get(User),
            accountId,
            from,
            to,
            cancellationToken);
        return Ok(StatementResponse.FromApplication(statement));
    }

    [HttpGet("statement.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> GetCsv(
        Guid accountId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        StatementDetails statement = await _statements.GetStatementAsync(
            _actors.Get(User),
            accountId,
            from,
            to,
            cancellationToken);
        byte[] content = Encoding.UTF8.GetBytes(FormatCsv(statement));
        return File(content, "text/csv; charset=utf-8", $"statement-{accountId:D}.csv");
    }

    private static string Escape(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string FormatCsv(StatementDetails statement)
    {
        var output = new StringBuilder();
        output.AppendLine(
            "accountId,fromInclusive,toExclusive,openingBalance,closingBalance," +
            "operationId,occurredAt,type,signedAmount,transferId");
        if (statement.Operations.Count == 0)
        {
            AppendCsvRow(output, statement, null);
        }
        else
        {
            foreach (StatementOperationDetails operation in statement.Operations)
                AppendCsvRow(output, statement, operation);
        }

        return output.ToString();
    }

    private static void AppendCsvRow(
        StringBuilder output,
        StatementDetails statement,
        StatementOperationDetails? operation)
    {
        string[] fields =
        [
            statement.AccountId.ToString("D"),
            statement.FromInclusive.ToString("O", CultureInfo.InvariantCulture),
            statement.ToExclusive.ToString("O", CultureInfo.InvariantCulture),
            statement.OpeningBalance.ToString("0.00", CultureInfo.InvariantCulture),
            statement.ClosingBalance.ToString("0.00", CultureInfo.InvariantCulture),
            operation?.Id.ToString("D") ?? string.Empty,
            operation?.OccurredAt.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            operation?.Type.ToString() ?? string.Empty,
            operation?.SignedAmount.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
            operation?.TransferId?.ToString("D") ?? string.Empty,
        ];
        output.AppendLine(string.Join(',', fields.Select(Escape)));
    }
}
