using Dobby.Audit;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Host;
using Dobby.Security.Authorization;
using Dobby.Security.CurrentContext;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class HostCustomerDeploymentContextProvisioningCoordinatorTests
{
    [Fact]
    public void Provision_denies_inactive_or_unprivileged_actor_before_context_replacement()
    {
        var store = new InMemoryCurrentContextStore(new CurrentCustomerDeploymentContext("default", "default"));
        var audit = new InMemoryAuditTrail();
        var coordinator = CreateCoordinator(
            store,
            audit,
            new UserPermissionState("managed", IsOwner: false, IsActive: true));

        var result = coordinator.Provision(
            "managed",
            "customer-a",
            "deployment-a",
            CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        store.Contexts.ShouldBe([new CurrentCustomerDeploymentContext("default", "default")]);
        var denied = audit.Snapshot().Single();
        denied.EventName.ShouldBe(AuditEventName.PermissionDenied);
        denied.ActorId.ShouldBe("managed");
        denied.Target.ShouldBe("customer-deployment.context.execute");
        denied.Result.ShouldBe(AuditResult.Denied);
    }

    [Fact]
    public void Provision_authorized_actor_replaces_context_and_audits_without_raw_identifiers()
    {
        var store = new InMemoryCurrentContextStore(new CurrentCustomerDeploymentContext("default", "default"));
        var audit = new InMemoryAuditTrail();
        var coordinator = CreateCoordinator(
            store,
            audit,
            new UserPermissionState("owner", IsOwner: true, IsActive: true));

        var result = coordinator.Provision(
            "owner",
            "customer-a",
            "deployment-a",
            CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Context.ShouldBe(new CurrentCustomerDeploymentContext("customer-a", "deployment-a"));
        store.Contexts.ShouldBe([new CurrentCustomerDeploymentContext("customer-a", "deployment-a")]);
        var success = audit.Snapshot().Single();
        success.EventName.ShouldBe(AuditEventName.CustomerDeploymentContextProvision);
        success.ActorId.ShouldBe("owner");
        success.Target.ShouldBe("current-context");
        success.Result.ShouldBe(AuditResult.Success);
        success.Details.ShouldNotBeNull();
        success.Details!["customerIdHash"].ShouldNotBe("customer-a");
        success.Details!["deploymentIdHash"].ShouldNotBe("deployment-a");
    }

    [Fact]
    public void Provision_store_failure_returns_typed_failure_and_audits_redacted_failure()
    {
        var audit = new InMemoryAuditTrail();
        var coordinator = CreateCoordinator(
            new ThrowingCurrentContextStore(),
            audit,
            new UserPermissionState("owner", IsOwner: true, IsActive: true));

        var result = coordinator.Provision(
            "owner",
            "customer-a",
            "deployment-a",
            CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        var failed = audit.Snapshot().Single();
        failed.EventName.ShouldBe(AuditEventName.CustomerDeploymentContextProvision);
        failed.Result.ShouldBe(AuditResult.Failed);
        failed.Details.ShouldNotBeNull();
        failed.Details!["customerIdHash"].ShouldNotBe("customer-a");
        failed.Details!["deploymentIdHash"].ShouldNotBe("deployment-a");
        failed.Details!["supportCode"].ShouldBe("context-provisioning-failed");
    }

    private static HostCustomerDeploymentContextProvisioningCoordinator CreateCoordinator(
        ICurrentCustomerDeploymentContextStore store,
        IAuditTrail audit,
        UserPermissionState actor)
    {
        return new HostCustomerDeploymentContextProvisioningCoordinator(
            new CurrentCustomerDeploymentContextService(store),
            new OperationAuthorizationPolicy(
                OperationTargetRegistry.CreateDefault(),
                new SingleUserGrantReader(actor)),
            audit,
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-20T00:00:00Z")));
    }

    private sealed class SingleUserGrantReader(UserPermissionState actor) : IPermissionGrantReader
    {
        public Result<UserPermissionState> LoadUserState(string userId)
        {
            return userId == actor.UserId
                ? Result.Success(actor)
                : Result.Failure<UserPermissionState>(ErrorCode.NotFound);
        }

        public Result<bool> HasGrant(string userId, OperationKey key)
        {
            return Result.Success(false);
        }
    }

    private sealed class InMemoryCurrentContextStore(params CurrentCustomerDeploymentContext[] contexts)
        : ICurrentCustomerDeploymentContextStore
    {
        private readonly List<CurrentCustomerDeploymentContext> contexts = contexts.ToList();

        public IReadOnlyList<CurrentCustomerDeploymentContext> Contexts => contexts;

        public IReadOnlyList<CurrentCustomerDeploymentContext> ReadActiveContexts() => contexts;

        public void SeedDefaultContext(CurrentCustomerDeploymentContext context)
        {
            contexts.Add(context);
        }

        public void ProvisionContext(CurrentCustomerDeploymentContext context)
        {
            contexts.Clear();
            contexts.Add(context);
        }
    }

    private sealed class ThrowingCurrentContextStore : ICurrentCustomerDeploymentContextStore
    {
        public IReadOnlyList<CurrentCustomerDeploymentContext> ReadActiveContexts()
        {
            return [new CurrentCustomerDeploymentContext("default", "default")];
        }

        public void SeedDefaultContext(CurrentCustomerDeploymentContext context)
        {
        }

        public void ProvisionContext(CurrentCustomerDeploymentContext context)
        {
            throw new InvalidOperationException("simulated store failure");
        }
    }
}
