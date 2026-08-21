using System.Text.Json;

namespace Dobby.Infrastructure.Persistence;

public sealed record HostOutboxDispatchResult(int Claimed, int Completed, int Failed);

public sealed class HostOutboxDispatcher(
    IHostPersistenceTransactionScope transactionScope,
    IReadOnlyDictionary<HostOutboxRoute, IHostOutboxMessageHandler> handlers,
    TimeProvider timeProvider,
    int maximumBatchSize = 32,
    TimeSpan? leaseDuration = null)
{
    private static readonly int[] RetrySeconds = [1, 5, 30, 120, 600, 3600];
    private readonly TimeSpan effectiveLeaseDuration = leaseDuration ?? TimeSpan.FromMinutes(2);

    public async Task<HostOutboxDispatchResult> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        if (maximumBatchSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBatchSize));
        }

        if (effectiveLeaseDuration <= TimeSpan.Zero || effectiveLeaseDuration > TimeSpan.FromMinutes(30))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var leaseId = Guid.CreateVersion7().ToString("D");
        var claimed = Claim(leaseId);
        var completed = 0;
        var failed = 0;

        for (var index = 0; index < claimed.Count; index++)
        {
            var row = claimed[index];
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fields = JsonSerializer.Deserialize<Dictionary<string, string>>(row.PayloadJson)
                    ?? throw new JsonException("Outbox fields are missing.");
                var route = new HostOutboxRoute(row.Purpose, row.MessageType);
                if (!handlers.TryGetValue(route, out var handler))
                {
                    throw new InvalidOperationException("No outbox handler is registered for this route.");
                }

                await handler.DispatchAsync(
                    new HostOutboxDelivery(
                        row.OutboxId,
                        row.IdempotencyKey,
                        row.Purpose,
                        row.MessageType,
                        fields,
                        row.AttemptCount),
                    cancellationToken).ConfigureAwait(false);
                MarkCompleted(row.OutboxId, leaseId);
                completed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ReleaseClaims(claimed.Skip(index), leaseId);
                throw;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                MarkFailed(row, leaseId, "handler-failed");
                failed++;
            }
        }

        return new HostOutboxDispatchResult(claimed.Count, completed, failed);
    }

    private IReadOnlyList<OutboxRow> Claim(string leaseId)
    {
        var claimed = new List<OutboxRow>();
        var now = timeProvider.GetUtcNow();
        var nowText = now.ToString("O");
        var leaseExpiresAt = now.Add(effectiveLeaseDuration).ToString("O");
        transactionScope.ExecuteInTransaction(executor =>
        {
            var candidates = executor.Query<OutboxRow>(
                """
                SELECT OutboxId, IdempotencyKey, Purpose, MessageType, PayloadJson, AttemptCount
                FROM HostOutbox
                WHERE CompletedAtUtc IS NULL
                  AND NextAttemptAtUtc <= @NowUtc
                  AND (LeaseExpiresAtUtc IS NULL OR LeaseExpiresAtUtc <= @NowUtc)
                ORDER BY CreatedAtUtc, OutboxId
                LIMIT @MaximumBatchSize;
                """,
                new { NowUtc = nowText, MaximumBatchSize = maximumBatchSize });
            foreach (var candidate in candidates)
            {
                var updated = executor.Execute(
                    """
                    UPDATE HostOutbox
                    SET LeaseId = @LeaseId, LeaseExpiresAtUtc = @LeaseExpiresAtUtc
                    WHERE OutboxId = @OutboxId
                      AND CompletedAtUtc IS NULL
                      AND (LeaseExpiresAtUtc IS NULL OR LeaseExpiresAtUtc <= @NowUtc);
                    """,
                    new
                    {
                        LeaseId = leaseId,
                        LeaseExpiresAtUtc = leaseExpiresAt,
                        candidate.OutboxId,
                        NowUtc = nowText
                    });
                if (updated == 1)
                {
                    claimed.Add(candidate);
                }
            }
        });
        return claimed;
    }

    private void MarkCompleted(string outboxId, string leaseId)
    {
        transactionScope.ExecuteInTransaction(executor =>
        {
            var updated = executor.Execute(
                """
                UPDATE HostOutbox
                SET CompletedAtUtc = @CompletedAtUtc,
                    LastErrorCode = NULL,
                    LeaseId = NULL,
                    LeaseExpiresAtUtc = NULL
                WHERE OutboxId = @OutboxId AND LeaseId = @LeaseId AND CompletedAtUtc IS NULL;
                """,
                new
                {
                    CompletedAtUtc = timeProvider.GetUtcNow().ToString("O"),
                    OutboxId = outboxId,
                    LeaseId = leaseId
                });
            if (updated != 1)
            {
                throw new InvalidOperationException("Outbox completion lease was lost.");
            }
        });
    }

    private void MarkFailed(OutboxRow row, string leaseId, string errorCode)
    {
        var attemptCount = checked(row.AttemptCount + 1);
        var delay = RetrySeconds[Math.Min(attemptCount - 1, RetrySeconds.Length - 1)];
        transactionScope.ExecuteInTransaction(executor =>
        {
            executor.Execute(
                """
                UPDATE HostOutbox
                SET AttemptCount = @AttemptCount,
                    NextAttemptAtUtc = @NextAttemptAtUtc,
                    LastErrorCode = @LastErrorCode,
                    LeaseId = NULL,
                    LeaseExpiresAtUtc = NULL
                WHERE OutboxId = @OutboxId AND LeaseId = @LeaseId AND CompletedAtUtc IS NULL;
                """,
                new
                {
                    AttemptCount = attemptCount,
                    NextAttemptAtUtc = timeProvider.GetUtcNow().AddSeconds(delay).ToString("O"),
                    LastErrorCode = errorCode,
                    row.OutboxId,
                    LeaseId = leaseId
                });
        });
    }

    private void ReleaseClaims(IEnumerable<OutboxRow> rows, string leaseId)
    {
        transactionScope.ExecuteInTransaction(executor =>
        {
            foreach (var row in rows)
            {
                executor.Execute(
                    "UPDATE HostOutbox SET LeaseId = NULL, LeaseExpiresAtUtc = NULL WHERE OutboxId = @OutboxId AND LeaseId = @LeaseId AND CompletedAtUtc IS NULL;",
                    new { row.OutboxId, LeaseId = leaseId });
            }
        });
    }

    private sealed class OutboxRow
    {
        public string OutboxId { get; init; } = string.Empty;

        public string IdempotencyKey { get; init; } = string.Empty;

        public string Purpose { get; init; } = string.Empty;

        public string MessageType { get; init; } = string.Empty;

        public string PayloadJson { get; init; } = string.Empty;

        public int AttemptCount { get; init; }
    }
}
