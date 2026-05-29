using Dobby.Infrastructure.Persistence;

namespace Dobby.Audit;

public interface IHostTransactionAuditTrail
{
    void AppendInScope(IHostPersistenceCommandExecutor executor, AuditEvent auditEvent);
}
