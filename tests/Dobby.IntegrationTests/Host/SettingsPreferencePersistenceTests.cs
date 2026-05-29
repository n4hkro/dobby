using Dobby.Configuration;
using Dobby.Host.Settings;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Shouldly;

namespace Dobby.IntegrationTests.Host;

public sealed class SettingsPreferencePersistenceTests
{
    [Fact]
    public void Preferences_persist_through_host_configuration_table_without_new_schema()
    {
        using var fixture = SettingsPreferenceFixture.Create();
        var scope = SettingsPreferenceScope.ForAuthenticatedUser("owner@example.local");
        var source = new HostConfigurationSettingsPreferenceSource(fixture.Executor, scope);

        source.Save(new SettingsPreferences(
            new AppearancePreference(AppThemePreference.Dark),
            new SecuritySessionPreference(AutoLockEnabled: true, AutoLockMinutes: 12),
            SettingsPreferences.Default.ShellView with
            {
                SidebarCollapsed = true,
                SidebarOpenNodeIds = ["settings", "settings.about"],
                SidebarRouteOrder = ["settings", "home"],
                ThirdBarRouteOrder = ["filters", "summary"],
                TopToolbarPluginOrder = ["plugin.reports", "plugin.cashier"],
                HiddenTopToolbarPluginIds = ["plugin.hidden"],
                WindowWidth = 1440,
                WindowHeight = 900,
                WindowLeft = 16,
                WindowTop = 24,
                LastActiveModuleRouteKey = "Dobby.Shell.Navigation.Settings",
                RestoreLastActiveModuleOnStartup = false
            })).IsSuccess.ShouldBeTrue();

        var reloaded = new HostConfigurationSettingsPreferenceSource(fixture.Executor, scope).Load().Value;
        reloaded.Appearance.Theme.ShouldBe(AppThemePreference.Dark);
        reloaded.SecuritySession.AutoLockEnabled.ShouldBeTrue();
        reloaded.SecuritySession.AutoLockMinutes.ShouldBe(12);
        reloaded.ShellView.ShouldSatisfyAllConditions(
            view => view.SidebarCollapsed.ShouldBeTrue(),
            view => view.SidebarOpenNodeIds.ShouldBe(["settings", "settings.about"]),
            view => view.SidebarRouteOrder.ShouldBe(["settings", "home"]),
            view => view.ThirdBarRouteOrder.ShouldBe(["filters", "summary"]),
            view => view.TopToolbarPluginOrder.ShouldBe(["plugin.reports", "plugin.cashier"]),
            view => view.HiddenTopToolbarPluginIds.ShouldBe(["plugin.hidden"]),
            view => view.WindowWidth.ShouldBe(1440),
            view => view.WindowHeight.ShouldBe(900),
            view => view.WindowLeft.ShouldBe(16),
            view => view.WindowTop.ShouldBe(24),
            view => view.LastActiveModuleRouteKey.ShouldBe("Dobby.Shell.Navigation.Settings"),
            view => view.RestoreLastActiveModuleOnStartup.ShouldBeFalse());

        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM HostConfiguration WHERE Source = 'settings';")
            .ShouldBeGreaterThanOrEqualTo(3);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'SettingsPreferences';")
            .ShouldBe(0);
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
            var root = Path.Combine(Path.GetTempPath(), "dobby-settings-preferences-" + Guid.NewGuid().ToString("N"));
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
