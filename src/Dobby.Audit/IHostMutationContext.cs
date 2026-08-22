using Dobby.Infrastructure.Persistence;

namespace Dobby.Audit;

public interface IHostMutationContext
{
    IHostPersistenceCommandExecutor Commands { get; }

    void AppendAudit(AuditEvent auditEvent);

    string EnqueueOutbox(HostOutboxMessage message);
}
