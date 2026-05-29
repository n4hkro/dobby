using Dobby.Configuration;
using Dobby.Host.Settings;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Shouldly;

namespace Dobby.IntegrationTests.Host;

public sealed class ShellPreferenceUserIsolationTests
{
    [Fact]
    public void Authenticated_user_scopes_persist_shell_view_preferences_without_collision_or_raw_user_ids()
    {
        using var fixture = SettingsPreferenceFixture.Create();
        var ownerScope = SettingsPreferenceScope.ForAuthenticatedUser("owner@example.local");
        var managedScope = SettingsPreferenceScope.ForAuthenticatedUser("managed@example.local");
        var ownerSource = new HostConfigurationSettingsPreferenceSource(fixture.Executor, ownerScope);
        var managedSource = new HostConfigurationSettingsPreferenceSource(fixture.Executor, managedScope);

        ownerSource.Save(SettingsPreferences.Default with
        {
            ShellView = SettingsPreferences.Default.ShellView with
            {
                SidebarCollapsed = true,
                SidebarOpenNodeIds = ["settings", "settings.about"],
                SidebarRouteOrder = ["settings", "home"],
                ThirdBarRouteOrder = ["about.fields", "about.plugins"],
                TopToolbarPluginOrder = ["plugin.owner.reports"],
                HiddenTopToolbarPluginIds = ["plugin.owner.hidden"],
                WindowWidth = 1600,
                WindowHeight = 920,
                WindowLeft = 10,
                WindowTop = 20,
                LastActiveModuleRouteKey = "Dobby.Shell.Navigation.Settings"
            }
        }).IsSuccess.ShouldBeTrue();
        managedSource.Save(SettingsPreferences.Default with
        {
            ShellView = SettingsPreferences.Default.ShellView with
            {
                SidebarCollapsed = false,
                SidebarOpenNodeIds = ["home"],
                SidebarRouteOrder = ["home", "plugins"],
                ThirdBarRouteOrder = ["plugins.details"],
                TopToolbarPluginOrder = ["plugin.managed.cashier"],
                HiddenTopToolbarPluginIds = ["plugin.managed.hidden"],
                WindowWidth = 1280,
                WindowHeight = 720,
                WindowLeft = 30,
                WindowTop = 40,
                LastActiveModuleRouteKey = "Dobby.Shell.Navigation.Home"
            }
        }).IsSuccess.ShouldBeTrue();

        new HostConfigurationSettingsPreferenceSource(fixture.Executor, ownerScope)
            .Load().Value.ShellView.ShouldSatisfyAllConditions(
                view => view.SidebarCollapsed.ShouldBeTrue(),
                view => view.SidebarOpenNodeIds.ShouldBe(["settings", "settings.about"]),
                view => view.SidebarRouteOrder.ShouldBe(["settings", "home"]),
                view => view.ThirdBarRouteOrder.ShouldBe(["about.fields", "about.plugins"]),
                view => view.TopToolbarPluginOrder.ShouldBe(["plugin.owner.reports"]),
                view => view.HiddenTopToolbarPluginIds.ShouldBe(["plugin.owner.hidden"]),
                view => view.WindowWidth.ShouldBe(1600),
                view => view.WindowHeight.ShouldBe(920),
                view => view.WindowLeft.ShouldBe(10),
                view => view.WindowTop.ShouldBe(20),
                view => view.LastActiveModuleRouteKey.ShouldBe("Dobby.Shell.Navigation.Settings"));
        new HostConfigurationSettingsPreferenceSource(fixture.Executor, managedScope)
            .Load().Value.ShellView.ShouldSatisfyAllConditions(
                view => view.SidebarCollapsed.ShouldBeFalse(),
                view => view.SidebarOpenNodeIds.ShouldBe(["home"]),
                view => view.SidebarRouteOrder.ShouldBe(["home", "plugins"]),
                view => view.ThirdBarRouteOrder.ShouldBe(["plugins.details"]),
                view => view.TopToolbarPluginOrder.ShouldBe(["plugin.managed.cashier"]),
                view => view.HiddenTopToolbarPluginIds.ShouldBe(["plugin.managed.hidden"]),
                view => view.WindowWidth.ShouldBe(1280),
                view => view.WindowHeight.ShouldBe(720),
                view => view.WindowLeft.ShouldBe(30),
                view => view.WindowTop.ShouldBe(40),
                view => view.LastActiveModuleRouteKey.ShouldBe("Dobby.Shell.Navigation.Home"));

        var keys = fixture.Executor.Query<string>("SELECT ConfigKey FROM HostConfiguration WHERE Source = 'settings' ORDER BY ConfigKey;");
        keys.ShouldAllBe(key => key.StartsWith("settings.user.", StringComparison.Ordinal));
        keys.ShouldAllBe(key => !key.Contains("owner", StringComparison.OrdinalIgnoreCase));
        keys.ShouldAllBe(key => !key.Contains("managed", StringComparison.OrdinalIgnoreCase));
        keys.ShouldAllBe(key => !key.Contains("@", StringComparison.Ordinal));
        keys.Distinct(StringComparer.Ordinal).Count().ShouldBe(keys.Count);
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
            var root = Path.Combine(Path.GetTempPath(), "dobby-shell-preferences-" + Guid.NewGuid().ToString("N"));
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
