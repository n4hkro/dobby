using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authentication;
using System.IO;

namespace Dobby.Host.Startup;

public sealed class OwnerBootstrapCoordinator(
    IHostPersistenceTransactionScope transactionScope,
    OwnerBootstrapService ownerBootstrapService,
    IHostTransactionAuditTrail auditTrail,
    TimeProvider timeProvider) : IOwnerBootstrapCoordinator
{
    private readonly object bootstrapGate = new();

    private static readonly IReadOnlyDictionary<string, string> BootstrapDetails = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bootstrapKind"] = "first-user-owner",
        ["principalRole"] = "Owner",
        ["permissionState"] = "is-owner-short-circuit",
        ["composition"] = "B04-transaction-aware-host-audit"
    };

    public Result BootstrapFirstOwner(string username, string? displayName, string password, CorrelationId correlationId)
    {
        lock (bootstrapGate)
        {
            Result<string>? bootstrapResult = null;

            try
            {
                transactionScope.ExecuteInTransaction(executor =>
                {
                    bootstrapResult = ownerBootstrapService.BootstrapOwner(
                        executor,
                        username,
                        displayName,
                        password,
                        timeProvider.GetUtcNow());

                    if (!bootstrapResult.IsSuccess)
                    {
                        throw new OwnerBootstrapFailedException(bootstrapResult.ErrorCode!.Value);
                    }

                    auditTrail.AppendInScope(executor, new AuditEvent(
                        AuditEventName.UserCreate,
                        "system",
                        bootstrapResult.Value,
                        AuditResult.Success,
                        correlationId,
                        timeProvider.GetUtcNow(),
                        BootstrapDetails));
                });
            }
            catch (OwnerBootstrapFailedException ex)
            {
                return Result.Failure(ex.ErrorCode);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                return Result.Failure(ErrorCode.DatabaseUnavailable);
            }

            return Result.Success();
        }
    }

    private sealed class OwnerBootstrapFailedException(ErrorCode errorCode) : Exception
    {
        public ErrorCode ErrorCode { get; } = errorCode;
    }
}
