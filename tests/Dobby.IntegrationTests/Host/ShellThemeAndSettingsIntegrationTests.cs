using Dobby.Configuration;
using Dobby.Host;
using Dobby.Host.Login;
using Dobby.Host.Settings;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Shouldly;
using System.Reflection;

namespace Dobby.IntegrationTests.Host;

public sealed class ShellThemeAndSettingsIntegrationTests
{
    [Fact]
    public void Top_toolbar_theme_switch_and_settings_appearance_share_scoped_host_configuration_preference()
    {
        using var fixture = SettingsPreferenceFixture.Create();
        var scope = SettingsPreferenceScope.ForAuthenticatedUser("owner@example.local");
        var source = new HostConfigurationSettingsPreferenceSource(fixture.Executor, scope);
        var coordinator = new ShellThemeCoordinator(source);

        coordinator.SetTheme(AppThemePreference.Dark).IsSuccess.ShouldBeTrue();

        var appearance = new AppearanceSettingsModel(new HostConfigurationSettingsPreferenceSource(fixture.Executor, scope));
        appearance.CurrentTheme.ShouldBe(AppThemePreference.Dark);
    }

    [Fact]
    public async Task Authenticated_session_runtime_consumes_saved_auto_lock_preference()
    {
        using var fixture = SettingsPreferenceFixture.Create();
        var reporter = new RecordingBackgroundOperationReporter();
        var userId = "owner@example.local";
        var scope = SettingsPreferenceScope.ForAuthenticatedUser(userId);
        var source = new HostConfigurationSettingsPreferenceSource(fixture.Executor, scope);
        source.Save(SettingsPreferences.Default with
        {
            SecuritySession = new SecuritySessionPreference(AutoLockEnabled: true, AutoLockMinutes: 12)
        }).IsSuccess.ShouldBeTrue();

        var runtime = HostLoginRuntimeFactory.Create(
            fixture.Options,
            TimeProvider.System,
            new RecordingProtector(),
            new RecordingProtector(),
            reporter,
            activeSession: new SessionRecord(
                Guid.NewGuid().ToString("D"),
                userId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(8),
                null,
                1));

        try
        {
            var sessionOptions = ReadSessionOptions(runtime.SessionLifecycleService);
            sessionOptions.AutoLockEnabledByDefault.ShouldBeTrue();
            sessionOptions.IdleTimeoutMinutes.ShouldBe(12);
            runtime.SettingsPreferenceSourceFactory(userId).Load().Value.SecuritySession.AutoLockMinutes.ShouldBe(12);
        }
        finally
        {
            WaitForAuditRetentionSweep(reporter);
            await runtime.DisposeAsync();
        }
    }

    private sealed class SettingsPreferenceFixture : IDisposable
    {
        private SettingsPreferenceFixture(string root)
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
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
        }

        public string Root { get; }

        public DobbyOptions Options { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public static SettingsPreferenceFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-shell-theme-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new SettingsPreferenceFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private static SessionOptions ReadSessionOptions(SessionLifecycleService sessionLifecycleService)
    {
        var field = typeof(SessionLifecycleService)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .SingleOrDefault(candidate => candidate.FieldType == typeof(SessionOptions));
        field.ShouldNotBeNull();
        return (SessionOptions)field!.GetValue(sessionLifecycleService)!;
    }

    private static void WaitForAuditRetentionSweep(RecordingBackgroundOperationReporter reporter)
    {
        SpinWait.SpinUntil(
            () => reporter.Snapshots.Any(snapshot => snapshot.Status is Dobby.Contracts.BackgroundOperations.BackgroundOperationStatus.Completed or Dobby.Contracts.BackgroundOperations.BackgroundOperationStatus.Failed),
            TimeSpan.FromSeconds(5)).ShouldBeTrue("audit retention sweep must finish before test data root cleanup.");
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

    private sealed class RecordingBackgroundOperationReporter : Dobby.Contracts.BackgroundOperations.IBackgroundOperationReporter
    {
        private readonly List<Dobby.Contracts.BackgroundOperations.BackgroundOperationSnapshot> snapshots = [];
        private readonly RecordingProgress progress;

        public RecordingBackgroundOperationReporter()
        {
            progress = new RecordingProgress(snapshots);
        }

        public IReadOnlyList<Dobby.Contracts.BackgroundOperations.BackgroundOperationSnapshot> Snapshots
        {
            get
            {
                lock (snapshots)
                {
                    return snapshots.ToArray();
                }
            }
        }

        public IProgress<Dobby.Contracts.BackgroundOperations.BackgroundOperationSnapshot> Progress => progress;

        private sealed class RecordingProgress(List<Dobby.Contracts.BackgroundOperations.BackgroundOperationSnapshot> snapshots)
            : IProgress<Dobby.Contracts.BackgroundOperations.BackgroundOperationSnapshot>
        {
            public void Report(Dobby.Contracts.BackgroundOperations.BackgroundOperationSnapshot value)
            {
                lock (snapshots)
                {
                    snapshots.Add(value);
                }
            }
        }
    }
}
