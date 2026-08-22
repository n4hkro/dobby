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
        using var authenticated = new CancellationTokenSource();
        var coordinator = new AuthenticatedPersistenceMaintenanceCoordinator(
            maintenance,
            new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), grants),
            reporter,
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
    }

    [Fact]
    public async Task Non_owner_without_maintenance_grant_is_denied_before_service_invocation()
    {
        var maintenance = Substitute.For<IHostPersistenceMaintenanceService>();
        var grants = Substitute.For<IPermissionGrantReader>();
        grants.LoadUserState("managed").Returns(Result.Success(
            new UserPermissionState("managed", IsOwner: false, IsActive: true)));
        grants.HasGrant("managed", Arg.Any<OperationKey>()).Returns(Result.Success(false));
        using var authenticated = new CancellationTokenSource();
        var coordinator = new AuthenticatedPersistenceMaintenanceCoordinator(
            maintenance,
            new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), grants),
            new RecordingReporter(),
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
    }

    [Fact]
    public async Task Restore_requires_explicit_current_store_quarantine_confirmation()
    {
        var maintenance = Substitute.For<IHostPersistenceMaintenanceService>();
        var grants = Substitute.For<IPermissionGrantReader>();
        using var authenticated = new CancellationTokenSource();
        var coordinator = new AuthenticatedPersistenceMaintenanceCoordinator(
            maintenance,
            new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), grants),
            new RecordingReporter(),
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
