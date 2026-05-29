using Dobby.Configuration;
using Dobby.Host;
using Dobby.Host.Settings;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Shouldly;

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

    private sealed class SettingsPreferenceFixture : IDisposable
    {
        private SettingsPreferenceFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
        }

        public string Root { get; }

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
}
