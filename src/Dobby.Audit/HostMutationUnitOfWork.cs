using Dobby.Infrastructure.Persistence;
using Dobby.Core;

namespace Dobby.Audit;

public interface IHostMutationContext
{
    IHostPersistenceCommandExecutor Commands { get; }

    void AppendAudit(AuditEvent auditEvent);

    string EnqueueOutbox(HostOutboxMessage message);
}

public interface IHostMutationUnitOfWork
{
    T Execute<T>(Func<IHostMutationContext, T> action);

    void Execute(Action<IHostMutationContext> action);

    Result<T> ExecuteResult<T>(Func<IHostMutationContext, Result<T>> action);

    Result ExecuteResult(Func<IHostMutationContext, Result> action);
}

public sealed class HostMutationUnitOfWork(
    IHostPersistenceTransactionScope transactionScope,
    IHostTransactionAuditTrail auditTrail,
    IHostTransactionOutbox outbox) : IHostMutationUnitOfWork
{
    public T Execute<T>(Func<IHostMutationContext, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        T? result = default;
        var completed = false;
        transactionScope.ExecuteInTransaction(executor =>
        {
            result = action(new Context(executor, auditTrail, outbox));
            completed = true;
        });

        return completed
            ? result!
            : throw new InvalidOperationException("Host mutation transaction did not complete.");
    }

    public void Execute(Action<IHostMutationContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _ = Execute(context =>
        {
            action(context);
            return true;
        });
    }

    public Result<T> ExecuteResult<T>(Func<IHostMutationContext, Result<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            return Execute(context =>
            {
                var result = action(context);
                return result.IsSuccess
                    ? result
                    : throw new RejectedMutationException(result.ErrorCode!.Value);
            });
        }
        catch (RejectedMutationException exception)
        {
            return Result.Failure<T>(exception.ErrorCode);
        }
    }

    public Result ExecuteResult(Func<IHostMutationContext, Result> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            return Execute(context =>
            {
                var result = action(context);
                return result.IsSuccess
                    ? result
                    : throw new RejectedMutationException(result.ErrorCode!.Value);
            });
        }
        catch (RejectedMutationException exception)
        {
            return Result.Failure(exception.ErrorCode);
        }
    }

    private sealed class Context(
        IHostPersistenceCommandExecutor executor,
        IHostTransactionAuditTrail auditTrail,
        IHostTransactionOutbox outbox) : IHostMutationContext
    {
        public IHostPersistenceCommandExecutor Commands => executor;

        public void AppendAudit(AuditEvent auditEvent)
        {
            auditTrail.AppendInScope(executor, auditEvent);
        }

        public string EnqueueOutbox(HostOutboxMessage message)
        {
            return outbox.EnqueueInScope(executor, message);
        }
    }

    private sealed class RejectedMutationException(ErrorCode errorCode) : Exception
    {
        public ErrorCode ErrorCode { get; } = errorCode;
    }
}
