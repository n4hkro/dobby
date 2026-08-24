using Dobby.Configuration;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Startup;

public sealed class StrictLoginFirstRuntimeTests
{
    [Fact]
    public void Failed_login_and_recovery_surface_construct_no_authenticated_runtime_component()
    {
        using var fixture = LoginFirstFixture.Create();
        var probe = new RecordingCompositionProbe();
        var preAuth = HostLoginRuntimeFactory.CreatePreLogin(
            fixture.Options,
            fixture.Time,
            fixture.Protector,
            applyPreLoginMigrations: false,
            compositionProbe: probe);

        var failed = preAuth.LoginWorkflow.Submit(new LoginSubmission(
            "owner",
            " ",
            null,
            RememberMe: false,
            CorrelationId.New()));
        _ = preAuth.RecoveryViewModel;

        failed.IsAuthenticated.ShouldBeFalse();
        probe.Events.ShouldBe([LoginRuntimeCompositionEvents.PreAuthRuntimeConstructed]);
        probe.Events.Intersect(LoginRuntimeCompositionEvents.ForbiddenBeforeAuthentication).ShouldBeEmpty();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM CustomerDeploymentContext;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE AuditId = 'old-before-login';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='InstalledPluginState';").ShouldBe(0);
    }

    [Fact]
    public async Task Successful_session_commit_is_the_gate_for_authenticated_composition_and_disposal()
    {
        using var fixture = LoginFirstFixture.Create();
        var probe = new RecordingCompositionProbe();
        var preAuth = HostLoginRuntimeFactory.CreatePreLogin(
            fixture.Options,
            fixture.Time,
            fixture.Protector,
            applyPreLoginMigrations: false,
            compositionProbe: probe);
        var login = preAuth.LoginWorkflow.Submit(new LoginSubmission(
            "owner",
            "first owner password",
            "Owner",
            RememberMe: false,
            CorrelationId.New()));

        login.IsAuthenticated.ShouldBeTrue();
        login.Session.ShouldNotBeNull();
        probe.Events.ShouldBe([LoginRuntimeCompositionEvents.PreAuthRuntimeConstructed]);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Sessions WHERE SessionId = @SessionId AND InvalidatedAtUtc IS NULL;",
            new { login.Session!.SessionId }).ShouldBe(1);

        var authenticatedProbe = new RecordingCompositionProbe();
        var backgroundOperations = new RecordingBackgroundOperationReporter();
        var firstRuntime = HostLoginRuntimeFactory.CreateAuthenticated(
            fixture.Options,
            fixture.Time,
            login.Session,
            fixture.Protector,
            backgroundOperations,
            applyPreLoginMigrations: false,
            compositionProbe: authenticatedProbe);
        try
        {
            var terminal = await backgroundOperations.TerminalSnapshot.WaitAsync(TimeSpan.FromSeconds(30));
            terminal.Status.ShouldBe(
                BackgroundOperationStatus.Completed,
                "authenticated audit retention must complete before the runtime is disposed and cancelled.");
            fixture.Executor.QueryScalar<int>(
                "SELECT COUNT(*) FROM AuditLog WHERE AuditId = 'old-before-login';").ShouldBe(0);

            var backupPath = Path.Combine(fixture.Root, "backup", "authenticated-maintenance.db");
            var backup = await firstRuntime.PersistenceMaintenanceCoordinator.CreateSnapshotAsync(
                login.Session.UserId,
                backupPath,
                new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
                CorrelationId.New());
            backup.IsSuccess.ShouldBeTrue();
            File.Exists(backupPath).ShouldBeTrue();
        }
        finally
        {
            await firstRuntime.DisposeAsync();
        }

        authenticatedProbe.Events.ShouldBe([
            LoginRuntimeCompositionEvents.AuthenticatedRuntimeConstructionStarted,
            LoginRuntimeCompositionEvents.BusinessRepositoriesConstructed,
            LoginRuntimeCompositionEvents.LicensingConstructed,
            LoginRuntimeCompositionEvents.PluginRuntimeConstructed,
            LoginRuntimeCompositionEvents.PersistenceMaintenanceConstructed,
            LoginRuntimeCompositionEvents.AuditRetentionStarted,
            LoginRuntimeCompositionEvents.AuthenticatedRuntimeConstructed
        ]);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM CustomerDeploymentContext;").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE AuditId = 'old-before-login';").ShouldBe(0);

        var secondRuntime = HostLoginRuntimeFactory.CreateAuthenticated(
            fixture.Options,
            fixture.Time,
            login.Session,
            fixture.Protector,
            applyPreLoginMigrations: false);
        secondRuntime.ShouldNotBeSameAs(firstRuntime);
        secondRuntime.PostLoginSessionRuntime.ShouldNotBeSameAs(firstRuntime.PostLoginSessionRuntime);
        secondRuntime.PluginRuntimeCoordinator.ShouldNotBeSameAs(firstRuntime.PluginRuntimeCoordinator);
        await secondRuntime.DisposeAsync();
    }

    private sealed class LoginFirstFixture : IDisposable
    {
        private LoginFirstFixture(string root)
        {
            Root = root;
            Options = new DobbyOptions
            {
                Runtime = new RuntimeOptions
                {
                    DataRoot = root,
                    EnvironmentName = RuntimeEnvironmentName.Test
                }
            };
            var persistenceOptions = new HostPersistenceOptions(
                root,
                Options.Storage.ProviderMode.ToString(),
                Options.Storage.RequireDataAtRestProtection,
                Options.Storage.BusyTimeoutMilliseconds);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(persistenceOptions, probe);
            new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
            Time = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-21T00:00:00Z"));
            Protector = new RecordingProtector();
            Executor.Execute(
                "INSERT INTO AuditLog(AuditId, OccurredAtUtc, EventName, ActorId, Target, Result, CorrelationId, DetailsJson) VALUES ('old-before-login', '2020-01-01T00:00:00.0000000Z', 'test.old', 'system', 'host', 'success', @CorrelationId, '{}');",
                new { CorrelationId = Guid.NewGuid().ToString("D") });
        }

        public string Root { get; }

        public DobbyOptions Options { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public FakeTimeProvider Time { get; }

        public RecordingProtector Protector { get; }

        public static LoginFirstFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-login-first-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new LoginFirstFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class RecordingCompositionProbe : ILoginRuntimeCompositionProbe
    {
        private readonly List<string> events = [];

        public IReadOnlyList<string> Events => events;

        public void Record(string eventName)
        {
            events.Add(eventName);
        }
    }

    public sealed class RecordingProtector : IProtectedDataProtector
    {
        private static readonly byte[] Prefix = "protected:"u8.ToArray();

        public byte[] Protect(byte[] plaintext, byte[] entropy)
        {
            return Prefix.Concat(plaintext.Select(value => (byte)(value ^ 0x5A))).ToArray();
        }

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy)
        {
            protectedBlob.Take(Prefix.Length).SequenceEqual(Prefix).ShouldBeTrue();
            return protectedBlob.Skip(Prefix.Length).Select(value => (byte)(value ^ 0x5A)).ToArray();
        }
    }

    private sealed class RecordingBackgroundOperationReporter : IBackgroundOperationReporter
    {
        private readonly TaskCompletionSource<BackgroundOperationSnapshot> terminalSnapshot = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public IProgress<BackgroundOperationSnapshot> Progress => new ProgressRecorder(terminalSnapshot);

        public Task<BackgroundOperationSnapshot> TerminalSnapshot => terminalSnapshot.Task;

        private sealed class ProgressRecorder(
            TaskCompletionSource<BackgroundOperationSnapshot> terminalSnapshot)
            : IProgress<BackgroundOperationSnapshot>
        {
            public void Report(BackgroundOperationSnapshot value)
            {
                if (value.Status is BackgroundOperationStatus.Completed or BackgroundOperationStatus.Cancelled or BackgroundOperationStatus.Failed)
                {
                    terminalSnapshot.TrySetResult(value);
                }
            }
        }
    }
}
