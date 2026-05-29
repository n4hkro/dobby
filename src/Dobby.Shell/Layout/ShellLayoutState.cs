using Dobby.Navigation;
using Dobby.Contracts.BackgroundOperations;

namespace Dobby.Shell.Layout;

public enum ShellNotificationKind
{
    Toast,
    Banner,
    Modal
}

public enum ShellFeedbackState
{
    Loading,
    Empty,
    Success,
    Error,
    Confirmation
}

public sealed record ShellProfileMenuItem(string Key, string LabelKey);

public sealed record ShellFocusTarget(string Name, string LabelKey);

public sealed record ShellSettingsCategoryItem(string Key, string Label, bool IsAvailable);

public sealed record ShellAboutField(string Key, string Label, string Value);

public sealed record ShellWindowPlacement(double Width, double Height, double? Left, double? Top)
{
    public static ShellWindowPlacement Default { get; } = new(1024, 680, null, null);
}

public sealed record ShellLayoutPreference(
    bool SidebarCollapsed,
    IReadOnlyList<string> SidebarOpenNodeIds,
    NavigationUserOverrides NavigationOverrides,
    ShellWindowPlacement WindowPlacement,
    string? LastActiveModuleRouteKey)
{
    public ShellLayoutPreference(bool SidebarCollapsed)
        : this(
            SidebarCollapsed,
            SidebarOpenNodeIds: [],
            NavigationUserOverrides.Empty,
            ShellWindowPlacement.Default,
            LastActiveModuleRouteKey: null)
    {
    }

    public static ShellLayoutPreference Default { get; } = new(
        SidebarCollapsed: false,
        SidebarOpenNodeIds: [],
        NavigationUserOverrides.Empty,
        ShellWindowPlacement.Default,
        LastActiveModuleRouteKey: null);
}

public sealed record ShellLayoutState(
    IReadOnlyList<string> SurfaceNames,
    IReadOnlyList<ShellProfileMenuItem> ProfileMenuItems,
    IReadOnlyList<ShellNotificationKind> NotificationKinds,
    IReadOnlyList<ShellFeedbackState> FeedbackStates,
    IReadOnlyList<ShellFocusTarget> FocusTraversal,
    IReadOnlyList<ShellSettingsCategoryItem> SettingsCategories,
    IReadOnlyList<ShellAboutField> AboutFields,
    string BackgroundOperationText,
    int? BackgroundOperationPercent,
    bool BackgroundOperationIsIndeterminate,
    ShellLayoutPreference Preference,
    object Theme,
    NavigationComposition Navigation)
{
    public static ShellLayoutState CreateDefault(NavigationComposition? navigation = null)
    {
        navigation ??= NavigationComposer.Compose([]);

        return new ShellLayoutState(
            SurfaceNames:
            [
                "TopToolbar",
                "Sidebar",
                "ThirdBar",
                "MainContent",
                "BottomStatus",
                "MinimizedWorkStrip"
            ],
            ProfileMenuItems:
            [
                new ShellProfileMenuItem("profile-account-info", "Dobby.Shell.Profile.AccountInfo"),
                new ShellProfileMenuItem("change-password", "Dobby.Shell.Profile.ChangePassword"),
                new ShellProfileMenuItem("manual-lock", "Dobby.Shell.Profile.ManualLock"),
                new ShellProfileMenuItem("logout", "Dobby.Shell.Profile.Logout"),
                new ShellProfileMenuItem("clear-saved-login", "Dobby.Shell.Profile.ClearSavedLogin")
            ],
            NotificationKinds:
            [
                ShellNotificationKind.Toast,
                ShellNotificationKind.Banner,
                ShellNotificationKind.Modal
            ],
            FeedbackStates:
            [
                ShellFeedbackState.Loading,
                ShellFeedbackState.Empty,
                ShellFeedbackState.Success,
                ShellFeedbackState.Error,
                ShellFeedbackState.Confirmation
            ],
            FocusTraversal:
            [
                new ShellFocusTarget("TopToolbar.Home", "Dobby.Shell.Home.Title"),
                new ShellFocusTarget("TopToolbar.Plugins", "Dobby.Shell.Plugins.Title"),
                new ShellFocusTarget("TopToolbar.Settings", "Dobby.Shell.Settings.Title"),
                new ShellFocusTarget("Sidebar.Toggle", "Dobby.Shell.Sidebar.Toggle"),
                new ShellFocusTarget("Theme.Toggle", "Dobby.Shell.Theme.Toggle"),
                new ShellFocusTarget("ProfileMenu.AccountInfo", "Dobby.Shell.Profile.AccountInfo"),
                new ShellFocusTarget("ProfileMenu.ChangePassword", "Dobby.Shell.Profile.ChangePassword"),
                new ShellFocusTarget("ProfileMenu.ManualLock", "Dobby.Shell.Profile.ManualLock"),
                new ShellFocusTarget("ProfileMenu.Logout", "Dobby.Shell.Profile.Logout"),
                new ShellFocusTarget("ProfileMenu.ClearSavedLogin", "Dobby.Shell.Profile.ClearSavedLogin")
            ],
            SettingsCategories: [],
            AboutFields: [],
            BackgroundOperationText: "Dobby.Shell.Status.Ready",
            BackgroundOperationPercent: null,
            BackgroundOperationIsIndeterminate: true,
            ShellLayoutPreference.Default,
            "Light",
            navigation);
    }

    public string CreateSnapshotDescriptor(int width, int height, object theme)
    {
        var lines = new List<string>
        {
            $"viewport={width}x{height}",
            $"theme={theme}"
        };
        lines.AddRange(SurfaceNames.Select(surface => $"surface={surface}"));
        lines.AddRange(Navigation.TopToolbar.SelectMany(NodeLines));
        return string.Join(Environment.NewLine, lines);
    }

    public ShellLayoutState WithSidebarCollapsed(bool collapsed)
    {
        return this with { Preference = Preference with { SidebarCollapsed = collapsed } };
    }

    public ShellLayoutState WithTheme(object theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return this with { Theme = theme };
    }

    public ShellLayoutState WithSettingsAndAbout(
        IReadOnlyList<ShellSettingsCategoryItem> settingsCategories,
        IReadOnlyList<ShellAboutField> aboutFields)
    {
        ArgumentNullException.ThrowIfNull(settingsCategories);
        ArgumentNullException.ThrowIfNull(aboutFields);

        return this with
        {
            SettingsCategories = settingsCategories,
            AboutFields = aboutFields
        };
    }

    public ShellLayoutState WithBackgroundOperation(BackgroundOperationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return this with
        {
            BackgroundOperationText = snapshot.DisplayNameKey,
            BackgroundOperationPercent = snapshot.Progress.Percent,
            BackgroundOperationIsIndeterminate = snapshot.Progress.IsIndeterminate
        };
    }

    private static IEnumerable<string> NodeLines(NavigationNode node)
    {
        yield return $"node={node.Id},target={node.Target}";
        foreach (var child in node.Children.SelectMany(NodeLines))
        {
            yield return child;
        }
    }
}
