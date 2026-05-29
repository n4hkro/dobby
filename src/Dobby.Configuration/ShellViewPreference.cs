namespace Dobby.Configuration;

public sealed record ShellViewPreference(
    bool SidebarCollapsed,
    IReadOnlyList<string> SidebarOpenNodeIds,
    IReadOnlyList<string> SidebarRouteOrder,
    IReadOnlyList<string> ThirdBarRouteOrder,
    IReadOnlyList<string> TopToolbarPluginOrder,
    IReadOnlyList<string> HiddenTopToolbarPluginIds,
    double WindowWidth,
    double WindowHeight,
    double? WindowLeft,
    double? WindowTop,
    string? LastActiveModuleRouteKey,
    bool RestoreLastActiveModuleOnStartup)
{
    public static ShellViewPreference Default { get; } = new(
        SidebarCollapsed: false,
        SidebarOpenNodeIds: [],
        SidebarRouteOrder: [],
        ThirdBarRouteOrder: [],
        TopToolbarPluginOrder: [],
        HiddenTopToolbarPluginIds: [],
        WindowWidth: 1024,
        WindowHeight: 680,
        WindowLeft: null,
        WindowTop: null,
        LastActiveModuleRouteKey: null,
        RestoreLastActiveModuleOnStartup: false);
}
