using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dobby.Infrastructure.Persistence;

public sealed record HostOutboxMessage(
    string IdempotencyKey,
    string Purpose,
    string MessageType,
    IReadOnlyDictionary<string, string> Fields,
    string? EvidenceCorrelationId = null);

public sealed record HostOutboxDelivery(
    string OutboxId,
    string IdempotencyKey,
    string Purpose,
    string MessageType,
    IReadOnlyDictionary<string, string> Fields,
    int PreviousAttemptCount);

public interface IHostTransactionOutbox
{
    string EnqueueInScope(IHostPersistenceCommandExecutor executor, HostOutboxMessage message);
}

public interface IHostOutboxMessageHandler
{
    Task DispatchAsync(HostOutboxDelivery delivery, CancellationToken cancellationToken);
}

public readonly record struct HostOutboxRoute(string Purpose, string MessageType);

public sealed class HostTransactionOutbox(TimeProvider timeProvider) : IHostTransactionOutbox
{
    public const int MaximumPayloadBytes = 16 * 1024;
    public const int MaximumFieldCount = 32;
    public const int MaximumFieldValueLength = 512;

    private static readonly Regex TokenPattern = new(
        "^[a-z0-9][a-z0-9.-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FieldNamePattern = new(
        "^[A-Za-z][A-Za-z0-9._-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string EnqueueInScope(IHostPersistenceCommandExecutor executor, HostOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(message);

        ValidateBoundedText(message.IdempotencyKey, 160, nameof(message.IdempotencyKey), allowTokenOnly: false);
        ValidateBoundedText(message.Purpose, 80, nameof(message.Purpose), allowTokenOnly: true);
        ValidateBoundedText(message.MessageType, 120, nameof(message.MessageType), allowTokenOnly: true);
        ArgumentNullException.ThrowIfNull(message.Fields);
        if (message.EvidenceCorrelationId is not null &&
            (!Guid.TryParse(message.EvidenceCorrelationId, out _) || message.EvidenceCorrelationId.Length > 64))
        {
            throw new ArgumentException("Outbox evidence correlation id is malformed.", nameof(message));
        }
        if (message.Fields.Count > MaximumFieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(message.Fields));
        }

        var ordered = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in message.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key) ||
                field.Key.Length > 64 ||
                !FieldNamePattern.IsMatch(field.Key) ||
                field.Value is null ||
                field.Value.Length > MaximumFieldValueLength)
            {
                throw new ArgumentException("Outbox safe fields are malformed or exceed their bounds.", nameof(message));
            }

            ordered.Add(field.Key, field.Value);
        }

        var payloadJson = JsonSerializer.Serialize(ordered);
        if (Encoding.UTF8.GetByteCount(payloadJson) > MaximumPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(message.Fields));
        }

        var existing = executor.Query<ExistingOutboxRow>(
            "SELECT OutboxId, Purpose, MessageType, PayloadJson, EvidenceCorrelationId FROM HostOutbox WHERE IdempotencyKey = @IdempotencyKey;",
            new { message.IdempotencyKey }).SingleOrDefault();
        if (existing is not null)
        {
            if (!existing.Purpose.Equals(message.Purpose, StringComparison.Ordinal) ||
                !existing.MessageType.Equals(message.MessageType, StringComparison.Ordinal) ||
                !existing.PayloadJson.Equals(payloadJson, StringComparison.Ordinal) ||
                !StringComparer.Ordinal.Equals(existing.EvidenceCorrelationId, message.EvidenceCorrelationId))
            {
                throw new InvalidOperationException("Outbox idempotency key was reused for a different message.");
            }

            return existing.OutboxId;
        }

        var outboxId = Guid.CreateVersion7().ToString("D");
        var createdAt = timeProvider.GetUtcNow().ToString("O");
        executor.Execute(
            """
            INSERT INTO HostOutbox(
                OutboxId, IdempotencyKey, Purpose, MessageType, PayloadJson,
                CreatedAtUtc, NextAttemptAtUtc, AttemptCount, CompletedAtUtc,
                LastErrorCode, LeaseId, LeaseExpiresAtUtc, EvidenceCorrelationId)
            VALUES (
                @OutboxId, @IdempotencyKey, @Purpose, @MessageType, @PayloadJson,
                @CreatedAtUtc, @CreatedAtUtc, 0, NULL, NULL, NULL, NULL, @EvidenceCorrelationId);
            """,
            new
            {
                OutboxId = outboxId,
                message.IdempotencyKey,
                message.Purpose,
                message.MessageType,
                message.EvidenceCorrelationId,
                PayloadJson = payloadJson,
                CreatedAtUtc = createdAt
            });
        return outboxId;
    }

    private static void ValidateBoundedText(
        string value,
        int maximumLength,
        string parameterName,
        bool allowTokenOnly)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > maximumLength ||
            value.Any(char.IsControl) ||
            (allowTokenOnly && !TokenPattern.IsMatch(value)))
        {
            throw new ArgumentException("Outbox identifier is malformed or exceeds its bound.", parameterName);
        }
    }

    private sealed class ExistingOutboxRow
    {
        public string OutboxId { get; init; } = string.Empty;

        public string Purpose { get; init; } = string.Empty;

        public string MessageType { get; init; } = string.Empty;

        public string PayloadJson { get; init; } = string.Empty;

        public string? EvidenceCorrelationId { get; init; }
    }
}
