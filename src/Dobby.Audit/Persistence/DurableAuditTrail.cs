using System.Text.Json;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;

namespace Dobby.Audit;

public sealed class DurableAuditTrail : IAuditTrail, IHostTransactionAuditTrail
{
    private readonly IHostPersistenceCommandExecutor executor;

    public DurableAuditTrail(IHostPersistenceCommandExecutor executor)
    {
        this.executor = executor;
    }

    public void Append(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        Append(executor, auditEvent);
    }

    public void AppendInScope(IHostPersistenceCommandExecutor executor, AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(auditEvent);

        Append(executor, auditEvent);
    }

    private static void Append(IHostPersistenceCommandExecutor executor, AuditEvent auditEvent)
    {
        var sanitized = AuditDetailsSanitizer.Sanitize(auditEvent);

        executor.Execute(
            "INSERT INTO AuditLog(AuditId, OccurredAtUtc, EventName, ActorId, Target, Result, CorrelationId, DetailsJson) VALUES (@AuditId, @OccurredAtUtc, @EventName, @ActorId, @Target, @Result, @CorrelationId, @DetailsJson);",
            new
            {
                AuditId = Guid.CreateVersion7().ToString("D"),
                OccurredAtUtc = sanitized.OccurredAt.ToUniversalTime().ToString("O"),
                sanitized.EventName,
                sanitized.ActorId,
                sanitized.Target,
                Result = sanitized.Result.ToPayloadValue(),
                CorrelationId = sanitized.CorrelationId.ToString(),
                DetailsJson = JsonSerializer.Serialize(sanitized.Details ?? new Dictionary<string, string>(StringComparer.Ordinal))
            });
    }

    public IReadOnlyList<AuditEvent> Snapshot()
    {
        var rows = executor.Query<AuditLogRow>(
            "SELECT OccurredAtUtc, EventName, ActorId, Target, Result, CorrelationId, DetailsJson FROM AuditLog ORDER BY OccurredAtUtc, AuditId;");

        return rows.Select(row => new AuditEvent(
                row.EventName,
                row.ActorId ?? string.Empty,
                row.Target ?? string.Empty,
                ParseResult(row.Result),
                new CorrelationId(Guid.Parse(row.CorrelationId ?? Guid.Empty.ToString("D"))),
                DateTimeOffset.Parse(row.OccurredAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                JsonSerializer.Deserialize<Dictionary<string, string>>(row.DetailsJson ?? "{}")))
            .ToArray();
    }

    private static AuditResult ParseResult(string value)
    {
        return value switch
        {
            "success" => AuditResult.Success,
            "failed" => AuditResult.Failed,
            "denied" => AuditResult.Denied,
            _ => throw new InvalidOperationException("Unsupported audit result value in durable audit log.")
        };
    }

    private sealed class AuditLogRow
    {
        public string OccurredAtUtc { get; init; } = string.Empty;

        public string EventName { get; init; } = string.Empty;

        public string? ActorId { get; init; }

        public string? Target { get; init; }

        public string Result { get; init; } = string.Empty;

        public string? CorrelationId { get; init; }

        public string? DetailsJson { get; init; }
    }
}
