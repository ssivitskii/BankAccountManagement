using Banking.Application;
using Banking.Application.Abstractions;
using Banking.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Banking.Infrastructure.Persistence;

public sealed class EfTransferService : ITransferService
{
    private const string IdempotencyScope = "transfer";
    private readonly BankingDbContext _context;
    private readonly ILogger<EfTransferService> _logger;
    private readonly TimeProvider _timeProvider;

    public EfTransferService(
        BankingDbContext context,
        TimeProvider timeProvider,
        ILogger<EfTransferService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<TransferDetails> TransferAsync(
        Actor actor,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ValidateIdempotencyKey(idempotencyKey);
        var money = new Money(amount);
        if (money.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Transfer amount must be greater than zero.");
        if (sourceAccountId == destinationAccountId)
            throw new ArgumentException("Source and destination accounts must differ.", nameof(destinationAccountId));

        string keyHash = Hash(idempotencyKey);
        string requestHash = Hash(string.Join(
            "\n",
            sourceAccountId.ToString("D"),
            destinationAccountId.ToString("D"),
            money.Amount.ToString("0.00", CultureInfo.InvariantCulture)));
        return await ExecuteTransferAsync(
            actor,
            sourceAccountId,
            destinationAccountId,
            money,
            keyHash,
            requestHash,
            cancellationToken).ConfigureAwait(false);
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static TransferDetails Map(Transfer transfer, bool isReplay)
    {
        return new TransferDetails(
            transfer.Id,
            transfer.SourceAccountId,
            transfer.DestinationAccountId,
            transfer.InitiatedByUserId,
            transfer.Amount.Amount,
            transfer.OccurredAt,
            isReplay);
    }

    private static void ValidateIdempotencyKey(string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        if (idempotencyKey.Length is < 1 or > 128
            || idempotencyKey.Any(character => character is < ' ' or > '~'))
        {
            throw new ArgumentException(
                "Idempotency-Key must contain 1 to 128 printable ASCII characters.",
                nameof(idempotencyKey));
        }
    }

    private async Task<TransferDetails> ExecuteTransferAsync(
        Actor actor,
        Guid sourceAccountId,
        Guid destinationAccountId,
        Money money,
        string keyHash,
        string requestHash,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);
        int reserved = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "IdempotencyRecords" ("ActorId", "Scope", "KeyHash", "RequestHash", "TransferId", "CreatedAt")
            VALUES ({actor.UserId}, {IdempotencyScope}, {keyHash}, {requestHash}, NULL, {now})
            ON CONFLICT ("ActorId", "Scope", "KeyHash") DO NOTHING
            """,
            cancellationToken).ConfigureAwait(false);
        if (reserved == 0)
        {
            TransferDetails replay = await LoadReplayAsync(actor.UserId, keyHash, requestHash, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return replay;
        }

        Guid[] accountIds = [sourceAccountId, destinationAccountId];
        Array.Sort(accountIds);
        Account[] accounts = await _context.Accounts
            .FromSqlInterpolated(
                $"SELECT * FROM \"Accounts\" WHERE \"Id\" = ANY ({accountIds}) ORDER BY \"Id\" FOR UPDATE")
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (accounts.Length != 2)
            throw new NotFoundException("Source or destination account was not found.");

        Account source = accounts.Single(account => account.Id == sourceAccountId);
        Account destination = accounts.Single(account => account.Id == destinationAccountId);
        if (actor.Role != UserRole.Admin && source.OwnerId != actor.UserId)
            throw new ForbiddenException("The source account belongs to another customer.");

        source.Debit(money);
        destination.Credit(money);
        var transfer = new Transfer(source.Id, destination.Id, actor.UserId, money, now);
        _context.Transfers.Add(transfer);
        _context.Operations.Add(new Operation(
            source.Id,
            OperationType.TransferOut,
            money,
            now,
            transfer.Id));
        _context.Operations.Add(new Operation(
            destination.Id,
            OperationType.TransferIn,
            money,
            now,
            transfer.Id));
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "IdempotencyRecords"
            SET "TransferId" = {transfer.Id}
            WHERE "ActorId" = {actor.UserId} AND "Scope" = {IdempotencyScope} AND "KeyHash" = {keyHash}
            """,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Transfer {TransferId} committed from account {SourceAccountId} to account {DestinationAccountId} by actor {ActorId}",
            transfer.Id,
            source.Id,
            destination.Id,
            actor.UserId);
        return Map(transfer, isReplay: false);
    }

    private async Task<TransferDetails> LoadReplayAsync(
        Guid actorId,
        string keyHash,
        string requestHash,
        CancellationToken cancellationToken)
    {
        IdempotencyRecord record = await _context.IdempotencyRecords.AsNoTracking()
            .SingleAsync(
                item => item.ActorId == actorId && item.Scope == IdempotencyScope && item.KeyHash == keyHash,
                cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
            throw new ConflictException("The Idempotency-Key was already used with a different transfer request.");
        if (record.TransferId is null)
            throw new ConflictException("The transfer request is still being processed.");

        Transfer transfer = await _context.Transfers.AsNoTracking()
            .SingleAsync(item => item.Id == record.TransferId.Value, cancellationToken)
            .ConfigureAwait(false);
        return Map(transfer, isReplay: true);
    }
}
