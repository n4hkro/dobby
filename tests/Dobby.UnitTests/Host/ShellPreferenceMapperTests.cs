using Dobby.Configuration;
using Dobby.Host;
using Dobby.Navigation;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class ShellPreferenceMapperTests
{
    [Fact]
    public void Shell_runtime_preference_adapter_round_trips_all_committed_sprint7_fields()
    {
        var durable = new ShellViewPreference(
            SidebarCollapsed: true,
            SidebarOpenNodeIds: ["sample.analytics"],
            SidebarRouteOrder: ["plugin.sample.analytics.daily"],
            ThirdBarRouteOrder: ["plugin.sample.analytics.daily.filters"],
            TopToolbarPluginOrder: ["plugin.sample.analytics"],
            HiddenTopToolbarPluginIds: ["plugin.sample.reports"],
            WindowWidth: 1366,
            WindowHeight: 768,
            WindowLeft: 100,
            WindowTop: 120,
            LastActiveModuleRouteKey: "plugin.sample.analytics.daily",
            RestoreLastActiveModuleOnStartup: true);

        var runtime = ShellPreferenceMapper.ToRuntimePreference(durable);
        var roundTrip = ShellPreferenceMapper.ToDurablePreference(runtime);

        runtime.SidebarCollapsed.ShouldBeTrue();
        runtime.SidebarOpenNodeIds.ShouldBe(["sample.analytics"]);
        runtime.NavigationOverrides.TopToolbarOrder.ShouldBe(["plugin.sample.analytics"]);
        runtime.NavigationOverrides.SidebarRouteOrder.ShouldBe(["plugin.sample.analytics.daily"]);
        runtime.NavigationOverrides.ThirdBarRouteOrder.ShouldBe(["plugin.sample.analytics.daily.filters"]);
        runtime.NavigationOverrides.HiddenRouteKeys.ShouldBe(["plugin.sample.reports"]);
        runtime.WindowPlacement.Width.ShouldBe(1366);
        runtime.WindowPlacement.Height.ShouldBe(768);
        runtime.WindowPlacement.Left.ShouldBe(100);
        runtime.WindowPlacement.Top.ShouldBe(120);
        runtime.LastActiveModuleRouteKey.ShouldBe("plugin.sample.analytics.daily");

        roundTrip.SidebarCollapsed.ShouldBeTrue();
        roundTrip.SidebarOpenNodeIds.ShouldBe(["sample.analytics"]);
        roundTrip.SidebarRouteOrder.ShouldBe(["plugin.sample.analytics.daily"]);
        roundTrip.ThirdBarRouteOrder.ShouldBe(["plugin.sample.analytics.daily.filters"]);
        roundTrip.TopToolbarPluginOrder.ShouldBe(["plugin.sample.analytics"]);
        roundTrip.HiddenTopToolbarPluginIds.ShouldBe(["plugin.sample.reports"]);
        roundTrip.WindowWidth.ShouldBe(1366);
        roundTrip.WindowHeight.ShouldBe(768);
        roundTrip.WindowLeft.ShouldBe(100);
        roundTrip.WindowTop.ShouldBe(120);
        roundTrip.LastActiveModuleRouteKey.ShouldBe("plugin.sample.analytics.daily");
        roundTrip.RestoreLastActiveModuleOnStartup.ShouldBeFalse();
    }

    [Fact]
    public void Shell_runtime_preference_adapter_does_not_restore_last_route_when_startup_restore_is_disabled()
    {
        var durable = ShellViewPreference.Default with
        {
            LastActiveModuleRouteKey = "plugin.sample.analytics.daily",
            RestoreLastActiveModuleOnStartup = false
        };

        var runtime = ShellPreferenceMapper.ToRuntimePreference(durable);

        runtime.LastActiveModuleRouteKey.ShouldBeNull();
    }

    [Fact]
    public void Shell_runtime_preference_adapter_clears_navigation_lists_when_runtime_reset_is_requested()
    {
        var runtime = Dobby.Shell.Layout.ShellLayoutPreference.Default with
        {
            NavigationOverrides = new NavigationUserOverrides(
                TopToolbarOrder: ["plugin.sample.analytics"],
                SidebarRouteOrder: ["plugin.sample.analytics.daily"],
                ThirdBarRouteOrder: ["plugin.sample.analytics.daily.filters"],
                HiddenRouteKeys: ["plugin.sample.reports"],
                ResetToDefaults: true)
        };

        var durable = ShellPreferenceMapper.ToDurablePreference(runtime);

        durable.TopToolbarPluginOrder.ShouldBeEmpty();
        durable.SidebarRouteOrder.ShouldBeEmpty();
        durable.ThirdBarRouteOrder.ShouldBeEmpty();
        durable.HiddenTopToolbarPluginIds.ShouldBeEmpty();
    }

    [Fact]
    public void Shell_runtime_preference_adapter_preserves_restore_flag_when_requested_by_host_persist_path()
    {
        var runtime = Dobby.Shell.Layout.ShellLayoutPreference.Default with
        {
            LastActiveModuleRouteKey = "plugin.sample.analytics.daily"
        };

        var durable = ShellPreferenceMapper.ToDurablePreference(
            runtime,
            restoreLastActiveModuleOnStartup: true);

        durable.LastActiveModuleRouteKey.ShouldBe("plugin.sample.analytics.daily");
        durable.RestoreLastActiveModuleOnStartup.ShouldBeTrue();
    }
}
