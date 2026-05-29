using Dobby.Audit;
using Dobby.Core;
using Dobby.Host.Settings;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authentication;
using NSubstitute;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class UserManagementAuditCoordinatorTests
{
    [Fact]
    public void Coordinator_records_successful_user_and_permission_events_with_existing_names()
    {
        var auditTrail = new RecordingTransactionAuditTrail();
        var executor = Substitute.For<IHostPersistenceCommandExecutor>();
        var coordinator = new UserManagementAuditCoordinator(auditTrail, executor, TimeProvider.System);
        var correlationId = CorrelationId.New();

        coordinator.AppendUserMutation("owner", "managed", ManagedUserAuditAction.Create, succeeded: true, correlationId);
        coordinator.AppendPermissionMutation("owner", "managed", "users.management.view", grant: true, succeeded: true, correlationId);

        auditTrail.Events.Select(item => item.EventName)
            .ShouldBe([AuditEventName.UserCreate, AuditEventName.PermissionGrant]);
    }

    [Fact]
    public void Coordinator_records_denial_and_owner_protection_without_new_event_names()
    {
        var auditTrail = new RecordingTransactionAuditTrail();
        var executor = Substitute.For<IHostPersistenceCommandExecutor>();
        var coordinator = new UserManagementAuditCoordinator(auditTrail, executor, TimeProvider.System);

        coordinator.AppendPermissionDenied("managed", "users.management.grant", CorrelationId.New());
        coordinator.AppendOwnerProtectionDenied("managed", "owner", CorrelationId.New());

        var events = auditTrail.Events;
        events.Select(item => item.EventName)
            .ShouldBe([AuditEventName.PermissionDenied, AuditEventName.OwnerProtectionDenied]);
        AuditEventName.All.ShouldContain(events[0].EventName);
        AuditEventName.All.ShouldContain(events[1].EventName);
    }

    [Fact]
    public void Coordinator_appends_user_management_events_through_host_transaction_audit_seam()
    {
        var transactionAuditTrail = new RecordingTransactionAuditTrail();
        var executor = Substitute.For<IHostPersistenceCommandExecutor>();
        var coordinator = new UserManagementAuditCoordinator(transactionAuditTrail, executor, TimeProvider.System);

        coordinator.AppendPermissionDenied("managed", "users.management.create", CorrelationId.New());

        transactionAuditTrail.Executor.ShouldBeSameAs(executor);
        transactionAuditTrail.Events.Single().EventName.ShouldBe(AuditEventName.PermissionDenied);
    }

    private sealed class RecordingTransactionAuditTrail : IHostTransactionAuditTrail
    {
        private readonly List<AuditEvent> events = [];

        public IHostPersistenceCommandExecutor? Executor { get; private set; }

        public IReadOnlyList<AuditEvent> Events => events;

        public void AppendInScope(IHostPersistenceCommandExecutor executor, AuditEvent auditEvent)
        {
            Executor = executor;
            events.Add(auditEvent);
        }
    }
}
