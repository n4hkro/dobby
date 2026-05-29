namespace Dobby.Audit;

public sealed class InMemoryAuditTrail : IAuditTrail
{
    private readonly List<AuditEvent> _events = [];

    public void Append(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var sanitized = auditEvent with
        {
            Details = AuditDetailsSanitizer.Sanitize(auditEvent.Details)
        };

        _events.Add(sanitized);
    }

    public IReadOnlyList<AuditEvent> Snapshot()
    {
        return _events.ToArray();
    }
}
