using Dobby.Infrastructure.Persistence;
using Dobby.Security.CurrentContext;

namespace Dobby.Host;

public sealed class HostCustomerDeploymentContextStore(
    IHostPersistenceCommandExecutor executor) : ICurrentCustomerDeploymentContextStore
{
    private const string DefaultContextId = "default";

    public IReadOnlyList<CurrentCustomerDeploymentContext> ReadActiveContexts()
    {
        var rows = executor.Query<CurrentContextRow>(
            """
            SELECT CustomerId, DeploymentId
            FROM CustomerDeploymentContext
            ORDER BY ContextId;
            """);

        return rows
            .Select(row => new CurrentCustomerDeploymentContext(row.CustomerId, row.DeploymentId))
            .ToArray();
    }

    public void SeedDefaultContext(CurrentCustomerDeploymentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var now = DateTimeOffset.UtcNow.ToString("O");
        executor.Execute(
            """
            INSERT INTO CustomerDeploymentContext (
                ContextId, CustomerId, DeploymentId, Source, ProtectionScope, SchemaVersion, CreatedAtUtc, UpdatedAtUtc
            ) VALUES (
                @ContextId, @CustomerId, @DeploymentId, 'host-bootstrap', 'machine-bound', 1, @Now, @Now
            )
            ON CONFLICT(ContextId) DO NOTHING;
            """,
            new
            {
                ContextId = DefaultContextId,
                context.CustomerId,
                context.DeploymentId,
                Now = now
            });
    }

    private sealed class CurrentContextRow
    {
        public string CustomerId { get; set; } = string.Empty;
        public string DeploymentId { get; set; } = string.Empty;
    }
}
