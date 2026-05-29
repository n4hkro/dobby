using Dobby.Configuration;
using Dobby.Navigation;
using Dobby.Shell.Layout;

namespace Dobby.Host;

public static class ShellPreferenceMapper
{
    public static ShellLayoutPreference ToRuntimePreference(ShellViewPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        var lastActiveRouteKey = preference.RestoreLastActiveModuleOnStartup
            ? preference.LastActiveModuleRouteKey
            : null;

        return new ShellLayoutPreference(
            preference.SidebarCollapsed,
            preference.SidebarOpenNodeIds,
            new NavigationUserOverrides(
                preference.TopToolbarPluginOrder,
                preference.SidebarRouteOrder,
                preference.ThirdBarRouteOrder,
                preference.HiddenTopToolbarPluginIds,
                ResetToDefaults: false),
            new ShellWindowPlacement(
                preference.WindowWidth,
                preference.WindowHeight,
                preference.WindowLeft,
                preference.WindowTop),
            lastActiveRouteKey);
    }

    public static ShellViewPreference ToDurablePreference(
        ShellLayoutPreference preference,
        bool restoreLastActiveModuleOnStartup = false)
    {
        ArgumentNullException.ThrowIfNull(preference);

        var navigationOverrides = preference.NavigationOverrides;
        var sidebarRouteOrder = navigationOverrides.ResetToDefaults ? [] : navigationOverrides.SidebarRouteOrder;
        var thirdBarRouteOrder = navigationOverrides.ResetToDefaults ? [] : navigationOverrides.ThirdBarRouteOrder;
        var topToolbarOrder = navigationOverrides.ResetToDefaults ? [] : navigationOverrides.TopToolbarOrder;
        var hiddenRouteKeys = navigationOverrides.ResetToDefaults ? [] : navigationOverrides.HiddenRouteKeys.ToArray();

        return new ShellViewPreference(
            preference.SidebarCollapsed,
            preference.SidebarOpenNodeIds,
            sidebarRouteOrder,
            thirdBarRouteOrder,
            topToolbarOrder,
            hiddenRouteKeys,
            preference.WindowPlacement.Width,
            preference.WindowPlacement.Height,
            preference.WindowPlacement.Left,
            preference.WindowPlacement.Top,
            preference.LastActiveModuleRouteKey,
            restoreLastActiveModuleOnStartup);
    }
}
