using Dobby.Audit;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authorization;
using NSubstitute;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class AuthenticatedPersistenceMaintenanceCoordinatorTests
{
    [Fact]
    public async Task Owner_backup_runs_on_authenticated_background_boundary_with_progress()
    {
        var maintenance = Substitute.For<IHostPersistenceMaintenanceService>();
        var snapshot = new HostPersistenceSnapshot(
            "backup.db",
            "backup.db.metadata.json",
            new string('a', 64),
            4096,
            DateTimeOffset.UtcNow);
        maintenance.CreateSupportSnapshot(
                "backup.db",
                Arg.Any<HostPersistenceBackupPolicy>(),
                Arg.Is<CancellationToken>(token => token.CanBeCanceled))
            .Returns(PersistenceResult<HostPersistenceSnapshot>.Success(snapshot));
        var grants = Substitute.For<IPermissionGrantReader>();
        grants.LoadUserState("owner").Returns(Result.Success(
            new UserPermissionState("owner", IsOwner: true, IsActive: true)));
        var reporter = new RecordingReporter();
        var auditTrail = new InMemoryAuditTrail();
        using var authenticated = new CancellationTokenSource();
        var coordinator = new AuthenticatedPersistenceMaintenanceCoordinator(
            maintenance,
            new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), grants),
            reporter,
            auditTrail,
            TimeProvider.System,
            authenticated.Token);

        var result = await coordinator.CreateSnapshotAsync(
            "owner",
            "backup.db",
            new HostPersistenceBackupPolicy(),
            CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(snapshot);
        reporter.Snapshots.Select(item => item.Status)
            .ShouldBe([BackgroundOperationStatus.Running, BackgroundOperationStatus.Completed]);
        var audited = auditTrail.Snapshot().ShouldHaveSingleItem();
        audited.EventName.ShouldBe(AuditEventName.PersistenceSnapshotCreated);
        audited.ActorId.ShouldBe("owner");
        audited.Target.ShouldBe("persistence.snapshot");
        audited.Result.ShouldBe(AuditResult.Success);
        audited.Details!["outputName"].ShouldBe("backup.db");
        audited.Details["sha256"].ShouldBe(new string('a', 64));
    }

    [Fact]
    public async Task Non_owner_without_maintenance_grant_is_denied_before_service_invocation()
    {
        var maintenance = Substitute.For<IHostPersistenceMaintenanceService>();
        var grants = Substitute.For<IPermissionGrantReader>();
        grants.LoadUserState("managed").Returns(Result.Success(
            new UserPermissionState("managed", IsOwner: false, IsActive: true)));
        grants.HasGrant("managed", Arg.Any<OperationKey>()).Returns(Result.Success(false));
        var auditTrail = new InMemoryAuditTrail();
        using var authenticated = new CancellationTokenSource();
        var coordinator = new AuthenticatedPersistenceMaintenanceCoordinator(
            maintenance,
            new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), grants),
            new RecordingReporter(),
            auditTrail,
            TimeProvider.System,
            authenticated.Token);

        var result = await coordinator.CreateSnapshotAsync(
            "managed",
            "backup.db",
            new HostPersistenceBackupPolicy(),
            CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        maintenance.DidNotReceive().CreateSupportSnapshot(
            Arg.Any<string>(),
            Arg.Any<HostPersistenceBackupPolicy>(),
            Arg.Any<CancellationToken>());
        var denied = auditTrail.Snapshot().ShouldHaveSingleItem();
        denied.EventName.ShouldBe(AuditEventName.PermissionDenied);
        denied.ActorId.ShouldBe("managed");
        denied.Result.ShouldBe(AuditResult.Denied);
        denied.Target.ShouldContain(BuiltInOperationTargets.PersistenceMaintenanceTargetId);
    }

    [Fact]
    public async Task Restore_requires_explicit_current_store_quarantine_confirmation()
    {
        var maintenance = Substitute.For<IHostPersistenceMaintenanceService>();
        var grants = Substitute.For<IPermissionGrantReader>();
        var auditTrail = new InMemoryAuditTrail();
        using var authenticated = new CancellationTokenSource();
        var coordinator = new AuthenticatedPersistenceMaintenanceCoordinator(
            maintenance,
            new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), grants),
            new RecordingReporter(),
            auditTrail,
            TimeProvider.System,
            authenticated.Token);

        var result = await coordinator.RestoreSnapshotAsync(
            "owner",
            "backup.db",
            confirmCurrentStoreQuarantine: false,
            CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        maintenance.DidNotReceive().RestoreSupportSnapshot(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        var refused = auditTrail.Snapshot().ShouldHaveSingleItem();
        refused.EventName.ShouldBe(AuditEventName.PersistenceRestoreRequested);
        refused.Result.ShouldBe(AuditResult.Denied);
        refused.Details!["reasonCode"].ShouldBe("QuarantineConfirmationMissing");
    }

    [Fact]
    public async Task Restore_records_intent_before_the_destructive_transition_and_outcome_after_it()
    {
        var maintenance = Substitute.For<IHostPersistenceMaintenanceService>();
        var auditTrail = new InMemoryAuditTrail();
        var auditedRowsWhenRestoreStarted = -1;
        maintenance.RestoreSupportSnapshot(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                auditedRowsWhenRestoreStarted = auditTrail.Snapshot().Count;
                return PersistenceResult<HostPersistenceRestoreOutcome>.Success(
                    new HostPersistenceRestoreOutcome(
                        @"C:\ProgramData\Dobby\data\host.db",
                        @"C:\ProgramData\Dobby\backup\restore-quarantine\20260826T101112131415Z-abc"));
            });
        var grants = Substitute.For<IPermissionGrantReader>();
        grants.LoadUserState("owner").Returns(Result.Success(
            new UserPermissionState("owner", IsOwner: true, IsActive: true)));
        using var authenticated = new CancellationTokenSource();
        var coordinator = new AuthenticatedPersistenceMaintenanceCoordinator(
            maintenance,
            new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), grants),
            new RecordingReporter(),
            auditTrail,
            TimeProvider.System,
            authenticated.Token);

        var result = await coordinator.RestoreSnapshotAsync(
            "owner",
            @"C:\ProgramData\Dobby\backup\host-backup-a.db",
            confirmCurrentStoreQuarantine: true,
            CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        auditedRowsWhenRestoreStarted.ShouldBe(1);
        var events = auditTrail.Snapshot();
        events.Select(item => item.EventName).ShouldBe(
        [
            AuditEventName.PersistenceRestoreRequested,
            AuditEventName.PersistenceRestoreCompleted
        ]);
        events[0].Result.ShouldBe(AuditResult.Success);
        events[0].Details!["candidateName"].ShouldBe("host-backup-a.db");
        events[1].Result.ShouldBe(AuditResult.Success);
        events[1].Details!["candidateName"].ShouldBe("host-backup-a.db");
        events[1].Details!["quarantineName"].ShouldBe("20260826T101112131415Z-abc");
        events.SelectMany(item => item.Details!.Values)
            .ShouldAllBe(value => !value.Contains('\\') && !value.Contains(':'));
    }

    [Fact]
    public async Task Failed_restore_is_audited_with_a_stable_reason_code_and_no_path_disclosure()
    {
        var maintenance = Substitute.For<IHostPersistenceMaintenanceService>();
        maintenance.RestoreSupportSnapshot(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PersistenceResult<HostPersistenceRestoreOutcome>.Failure(
                PersistenceError.Create(PersistenceError.IoFailed)));
        var grants = Substitute.For<IPermissionGrantReader>();
        grants.LoadUserState("owner").Returns(Result.Success(
            new UserPermissionState("owner", IsOwner: true, IsActive: true)));
        var auditTrail = new InMemoryAuditTrail();
        using var authenticated = new CancellationTokenSource();
        var coordinator = new AuthenticatedPersistenceMaintenanceCoordinator(
            maintenance,
            new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), grants),
            new RecordingReporter(),
            auditTrail,
            TimeProvider.System,
            authenticated.Token);

        var result = await coordinator.RestoreSnapshotAsync(
            "owner",
            @"C:\ProgramData\Dobby\backup\host-backup-b.db",
            confirmCurrentStoreQuarantine: true,
            CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.IoFailed);
        var events = auditTrail.Snapshot();
        events.Select(item => item.EventName).ShouldBe(
        [
            AuditEventName.PersistenceRestoreRequested,
            AuditEventName.PersistenceRestoreCompleted
        ]);
        events[1].Result.ShouldBe(AuditResult.Failed);
        events[1].Details!["reasonCode"].ShouldBe(PersistenceError.IoFailed);
        events[1].Details!["candidateName"].ShouldBe("host-backup-b.db");
    }

    private sealed class RecordingReporter : IBackgroundOperationReporter
    {
        public List<BackgroundOperationSnapshot> Snapshots { get; } = [];

        public IProgress<BackgroundOperationSnapshot> Progress => new SynchronousProgress(Snapshots);

        private sealed class SynchronousProgress(List<BackgroundOperationSnapshot> snapshots)
            : IProgress<BackgroundOperationSnapshot>
        {
            public void Report(BackgroundOperationSnapshot value) => snapshots.Add(value);
        }
    }
}
