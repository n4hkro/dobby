namespace Dobby.Audit;

public interface IAuditTrail
{
    void Append(AuditEvent auditEvent);

    IReadOnlyList<AuditEvent> Snapshot();
}
