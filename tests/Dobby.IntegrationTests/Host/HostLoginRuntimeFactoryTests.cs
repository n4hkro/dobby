using Dobby.Configuration;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Application;
using Dobby.Host.Login;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Shouldly;

namespace Dobby.IntegrationTests.Host;

public sealed class HostLoginRuntimeFactoryTests
{
    [Fact]
    public void Create_starts_one_shot_audit_retention_sweep_without_login_submission()
    {
        using var fixture = HostRuntimePersistenceFixture.Create();
        var old = DateTimeOffset.UtcNow.AddDays(-366).ToString("O");
        var retained = DateTimeOffset.UtcNow.AddDays(-364).ToString("O");
        fixture.Executor.Execute(
            "INSERT INTO AuditLog(AuditId, OccurredAtUtc, EventName, ActorId, Target, Result, CorrelationId, DetailsJson) VALUES ('old', @Old, 'audit.old', 'system', 'audit', 'success', @CorrelationId, '{}');",
            new { Old = old, CorrelationId = Guid.NewGuid().ToString("D") });
        fixture.Executor.Execute(
            "INSERT INTO AuditLog(AuditId, OccurredAtUtc, EventName, ActorId, Target, Result, CorrelationId, DetailsJson) VALUES ('retained', @Retained, 'audit.retained', 'system', 'audit', 'success', @CorrelationId, '{}');",
            new { Retained = retained, CorrelationId = Guid.NewGuid().ToString("D") });

        var runtime = HostLoginRuntimeFactory.Create(
            fixture.Options,
            TimeProvider.System,
            new RecordingProtector(),
            new RecordingProtector());

        SpinWait.SpinUntil(
            () => fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE AuditId = 'old';") == 0,
            TimeSpan.FromSeconds(5)).ShouldBeTrue("audit retention must run as a one-shot post-LoginVisible maintenance operation.");
        fixture.Executor.Query<string>("SELECT AuditId FROM AuditLog ORDER BY AuditId;")
            .ShouldBe(["retained"]);
        WaitForAuditRetentionSweep(runtime);
    }

    [Fact]
    public void Create_reports_audit_retention_sweep_as_observed_background_operation()
    {
        using var fixture = HostRuntimePersistenceFixture.Create();
        var reporter = new RecordingBackgroundOperationReporter();

        _ = HostLoginRuntimeFactory.Create(
            fixture.Options,
            TimeProvider.System,
            new RecordingProtector(),
            new RecordingProtector(),
            reporter);

        SpinWait.SpinUntil(
            () => reporter.Snapshots.Any(snapshot => snapshot.Status == BackgroundOperationStatus.Completed),
            TimeSpan.FromSeconds(5)).ShouldBeTrue("audit retention sweep must report observed completion.");

        reporter.Snapshots.Select(snapshot => snapshot.Status)
            .ShouldContain(BackgroundOperationStatus.Running);
        var final = reporter.Snapshots.Last();
        final.OwnerKind.ShouldBe(BackgroundOperationOwnerKind.Host);
        final.DisplayNameKey.ShouldBe("Dobby.Host.AuditRetentionSweep");
        final.Progress.IsIndeterminate.ShouldBeTrue();
        final.CanCancel.ShouldBeFalse();
        final.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void Create_composes_sprint6_authorization_user_management_and_settings_sources()
    {
        using var fixture = HostRuntimePersistenceFixture.Create();

        var runtime = HostLoginRuntimeFactory.Create(
            fixture.Options,
            TimeProvider.System,
            new RecordingProtector(),
            new RecordingProtector());

        runtime.OperationAuthorizationPolicy.ShouldNotBeNull();
        runtime.ManagedUserService.ShouldNotBeNull();
        runtime.SettingsPreferenceSource.ShouldNotBeNull();
        runtime.AccountSettingsModel.ShouldNotBeNull();
        runtime.SettingsNavigationModel.ShouldNotBeNull();
        runtime.AboutInfoProvider.ShouldNotBeNull();
        WaitForAuditRetentionSweep(runtime);
    }

    [Fact]
    public void Create_composes_sprint7_shell_runtime_sources_for_post_login_shell()
    {
        using var fixture = HostRuntimePersistenceFixture.Create();
        var reporter = new RecordingBackgroundOperationReporter();

        var runtime = HostLoginRuntimeFactory.Create(
            fixture.Options,
            TimeProvider.System,
            new RecordingProtector(),
            new RecordingProtector(),
            reporter);
        var scopedPreferenceSource = runtime.SettingsPreferenceSourceFactory("owner@example.local");
        var preferences = SettingsPreferences.Default with
        {
            ShellView = SettingsPreferences.Default.ShellView with
            {
                SidebarCollapsed = true
            }
        };

        scopedPreferenceSource.Save(preferences).IsSuccess.ShouldBeTrue();

        runtime.DiagnosticsExportCoordinator.ShouldNotBeNull();
        runtime.BackgroundOperations.ShouldBeSameAs(reporter);
        scopedPreferenceSource.Load().Value.ShellView.SidebarCollapsed.ShouldBeTrue();
        var keys = fixture.Executor.Query<string>(
            "SELECT ConfigKey FROM HostConfiguration WHERE ConfigKey LIKE 'settings.user.%.shell.sidebar-collapsed';");
        keys.Count.ShouldBe(1);
        keys.Single().ShouldNotContain("owner@example.local");
        WaitForAuditRetentionSweep(runtime);
    }

    [Fact]
    public void Create_wires_about_version_from_host_assembly_metadata()
    {
        using var fixture = HostRuntimePersistenceFixture.Create();
        var expectedVersion = typeof(HostLoginRuntimeFactory).Assembly.GetName().Version!;
        var expectedDisplayVersion = $"{expectedVersion.Major}.{expectedVersion.Minor}.{expectedVersion.Build}";

        var runtime = HostLoginRuntimeFactory.Create(
            fixture.Options,
            TimeProvider.System,
            new RecordingProtector(),
            new RecordingProtector());

        var info = runtime.AboutInfoProvider.GetInfo(isOwner: true, ownerIdentity: "owner");

        info.AppVersion.ShouldBe(expectedDisplayVersion);
        info.BuildNumber.ShouldBe(expectedDisplayVersion);
        info.BuildDate.ShouldNotBe("unknown");
        WaitForAuditRetentionSweep(runtime);
    }

    private static void WaitForAuditRetentionSweep(HostLoginRuntime runtime)
    {
        switch (runtime.BackgroundOperations)
        {
            case BackgroundOperationReporter reporter:
                SpinWait.SpinUntil(
                    () => reporter.LatestSnapshot?.Status is BackgroundOperationStatus.Completed or BackgroundOperationStatus.Failed,
                    TimeSpan.FromSeconds(5)).ShouldBeTrue("audit retention sweep must finish before test data root cleanup.");
                break;
            case RecordingBackgroundOperationReporter reporter:
                SpinWait.SpinUntil(
                    () => reporter.Snapshots.Any(snapshot => snapshot.Status is BackgroundOperationStatus.Completed or BackgroundOperationStatus.Failed),
                    TimeSpan.FromSeconds(5)).ShouldBeTrue("audit retention sweep must finish before test data root cleanup.");
                break;
        }
    }

    private sealed class HostRuntimePersistenceFixture : IDisposable
    {
        private HostRuntimePersistenceFixture(string root)
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
                Options.Runtime.DataRoot,
                Options.Storage.ProviderMode.ToString(),
                Options.Storage.RequireDataAtRestProtection,
                Options.Storage.BusyTimeoutMilliseconds);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(persistenceOptions, probe);
            new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
        }

        public string Root { get; }

        public DobbyOptions Options { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public static HostRuntimePersistenceFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-host-runtime-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new HostRuntimePersistenceFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class RecordingProtector : IProtectedDataProtector
    {
        private static readonly byte[] ProtectedPrefix = "protected:"u8.ToArray();

        public byte[] Protect(byte[] plaintext, byte[] entropy)
        {
            return ProtectedPrefix.Concat(plaintext.Select(value => (byte)(value ^ 0x5A))).ToArray();
        }

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy)
        {
            protectedBlob.Take(ProtectedPrefix.Length).SequenceEqual(ProtectedPrefix).ShouldBeTrue();
            return protectedBlob.Skip(ProtectedPrefix.Length).Select(value => (byte)(value ^ 0x5A)).ToArray();
        }
    }

    private sealed class RecordingBackgroundOperationReporter : IBackgroundOperationReporter
    {
        private readonly List<BackgroundOperationSnapshot> snapshots = [];
        private readonly RecordingProgress progress;

        public RecordingBackgroundOperationReporter()
        {
            progress = new RecordingProgress(snapshots);
        }

        public IReadOnlyList<BackgroundOperationSnapshot> Snapshots
        {
            get
            {
                lock (snapshots)
                {
                    return snapshots.ToArray();
                }
            }
        }

        public IProgress<BackgroundOperationSnapshot> Progress => progress;

        private sealed class RecordingProgress(List<BackgroundOperationSnapshot> snapshots)
            : IProgress<BackgroundOperationSnapshot>
        {
            public void Report(BackgroundOperationSnapshot value)
            {
                lock (snapshots)
                {
                    snapshots.Add(value);
                }
            }
        }
    }
}
