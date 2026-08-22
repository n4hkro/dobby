namespace Dobby.Audit;

public sealed class InMemoryAuditTrail : IAuditTrail
{
    private readonly List<AuditEvent> _events = [];

    public void Append(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var sanitized = AuditDetailsSanitizer.Sanitize(auditEvent);

        _events.Add(sanitized);
    }

    public IReadOnlyList<AuditEvent> Snapshot()
    {
        return _events.ToArray();
    }
}
