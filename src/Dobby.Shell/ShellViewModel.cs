using Dobby.Core;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.Shell.Layout;
using Dobby.Shell.WorkSessions;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Dobby.Shell;

public sealed class ShellViewModel : INotifyPropertyChanged
{
    private IReadOnlyList<NavigationContribution> contributions;
    private readonly bool usesStaticNavigation;
    private readonly HostStringCatalog strings;
    private NavigationComposition navigation;
    private IPluginPageModel? activePluginPageModel;
    private bool canViewLicenseStatus;
    private bool canImportLicense;
    private PluginRuntimeMode licenseRuntimeMode = PluginRuntimeMode.Denied;
    private string? licenseFeedbackKey;

    public ShellViewModel(HostStringCatalog strings)
        : this(strings, Array.Empty<NavigationContribution>())
    {
    }

    public ShellViewModel(HostStringCatalog strings, IEnumerable<NavigationContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(contributions);

        this.strings = strings;
        this.contributions = contributions.ToArray();
        usesStaticNavigation = false;
        navigation = NavigationComposer.Compose(this.contributions);
        Layout = ShellLayoutState.CreateDefault(navigation);
        RebuildNavigationLevels(RouteRegistry.HomeRouteKey);
    }

    public ShellViewModel(HostStringCatalog strings, NavigationComposition navigation)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(navigation);

        this.strings = strings;
        contributions = [];
        usesStaticNavigation = true;
        this.navigation = navigation;
        Layout = ShellLayoutState.CreateDefault(navigation);
        RebuildNavigationLevels(RouteRegistry.HomeRouteKey);
    }

    public static ShellViewModel CreateDefault(HostStringCatalog strings)
    {
        return new ShellViewModel(strings);
    }

    public IReadOnlyList<ShellNavigationItem> TopNavigation { get; private set; } = [];

    public IReadOnlyList<ShellNavigationItem> SidebarNavigation { get; private set; } = [];

    public IReadOnlyList<ShellNavigationItem> ThirdBarNavigation { get; private set; } = [];

    public string BrandName => strings.GetString(HostResourceKeys.ShellBrand);

    public string StatusText => strings.GetString(HostResourceKeys.ShellStatusReady);

    public string DiagnosticsExportLabel => strings.GetString(HostResourceKeys.ShellDiagnosticsExport);

    public string DiagnosticsExportedLabel => strings.GetString(HostResourceKeys.ShellDiagnosticsExported);

    public string PasswordChangedLabel => strings.GetString(HostResourceKeys.ShellPasswordChanged);

    public string WorkSessionsLabel => strings.GetString(HostResourceKeys.ShellWorkSessions);

    public string WorkSessionNavigationWarningLabel => strings.GetString(HostResourceKeys.ShellWorkSessionNavigationWarning);

    public string WorkSessionStayLabel => strings.GetString(HostResourceKeys.ShellWorkSessionStay);

    public string WorkSessionMinimizeLabel => strings.GetString(HostResourceKeys.ShellWorkSessionMinimize);

    public string WorkSessionCloseLabel => strings.GetString(HostResourceKeys.ShellWorkSessionClose);

    public string SidebarToggleLabel => strings.GetString(HostResourceKeys.ShellSidebarToggle);

    public string ThemeToggleLabel => strings.GetString(HostResourceKeys.ShellThemeToggle);

    public string RememberMeLabel => strings.GetString(HostResourceKeys.LoginRememberMe);

    public string ProfileAccountInfoLabel => strings.GetString(HostResourceKeys.ShellProfileAccountInfo);

    public string ChangePasswordLabel => strings.GetString(HostResourceKeys.ShellProfileChangePassword);

    public string ManualLockLabel => strings.GetString(HostResourceKeys.ShellProfileManualLock);

    public string LogoutLabel => strings.GetString(HostResourceKeys.ShellProfileLogout);

    public string ClearSavedLoginLabel => strings.GetString(HostResourceKeys.ShellProfileClearSavedLogin);

    public string SettingsHeading => strings.GetString(HostResourceKeys.ShellSettingsHeading);

    public string AboutHeading => strings.GetString(HostResourceKeys.ShellAboutHeading);

    public string MinimizedWorkStripLabel => strings.GetString(HostResourceKeys.ShellMinimizedWorkStrip);

    public string ThirdBarLabel => strings.GetString(HostResourceKeys.ShellThirdBar);

    public string BackgroundOperationLabel => strings.GetString(HostResourceKeys.ShellBackgroundOperation);

    public string LicenseHeading => strings.GetString(HostResourceKeys.LicenseHeading);

    public string LicenseStatusLabel => strings.GetString(HostResourceKeys.LicenseStatusLabel);

    public string LicenseImportLabel => strings.GetString(HostResourceKeys.LicenseImportAction);

    public string LicenseRuntimeModeText => canViewLicenseStatus
        ? strings.GetString(LicenseModeKey(licenseRuntimeMode))
        : string.Empty;

    public string LicenseFeedbackText => string.IsNullOrWhiteSpace(licenseFeedbackKey)
        ? string.Empty
        : strings.GetString(licenseFeedbackKey);

    public Visibility LicensePanelVisibility => canViewLicenseStatus || canImportLicense
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LicenseStatusVisibility => canViewLicenseStatus
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LicenseImportVisibility => canImportLicense
        ? Visibility.Visible
        : Visibility.Collapsed;

    public ShellLayoutState Layout { get; private set; }

    public string ActiveRouteKey { get; private set; } = RouteRegistry.HomeRouteKey;

    public PendingWorkSessionCloseRequest? PendingWorkSessionCloseRequest { get; private set; }

    public bool HasPendingWorkSessionCloseRequest => PendingWorkSessionCloseRequest is not null;

    public IReadOnlyList<ShellProfileMenuItem> ProfileMenuItems => Layout.ProfileMenuItems;

    public IReadOnlyList<ShellFocusTarget> FocusTraversal => Layout.FocusTraversal;

    public IReadOnlyList<ShellPresentationItem> NotificationPresentation => Layout.NotificationKinds
        .Select(kind => new ShellPresentationItem(kind.ToString(), strings.GetString(NotificationLabelKey(kind))))
        .ToArray();

    public IReadOnlyList<ShellPresentationItem> FeedbackStatePresentation => Layout.FeedbackStates
        .Select(state => new ShellPresentationItem(state.ToString(), strings.GetString(FeedbackLabelKey(state))))
        .ToArray();

    public IReadOnlyList<ShellSettingsCategoryItem> SettingsCategories => Layout.SettingsCategories;

    public IReadOnlyList<ShellAboutField> AboutFields => Layout.AboutFields;

    public IPluginPageModel? ActivePluginPageModel => activePluginPageModel;

    public bool HasActivePluginPage => activePluginPageModel is not null;

    public string BackgroundOperationText => strings.GetString(Layout.BackgroundOperationText);

    public int BackgroundOperationPercent => Layout.BackgroundOperationPercent ?? 0;

    public bool BackgroundOperationIsIndeterminate => Layout.BackgroundOperationIsIndeterminate;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Result ActivateRoute(string? routeKey)
    {
        RebuildNavigationLevels(routeKey);
        return Result.Success();
    }

    public void SetPendingWorkSessionCloseRequest(PendingWorkSessionCloseRequest? pendingRequest)
    {
        PendingWorkSessionCloseRequest = pendingRequest;
        OnPropertyChanged(nameof(PendingWorkSessionCloseRequest));
        OnPropertyChanged(nameof(HasPendingWorkSessionCloseRequest));
    }

    public Result ResolvePendingWorkSessionClose(
        WorkSessionCloseChoice choice,
        WorkSessionCloseCoordinator coordinator)
    {
        var pending = PendingWorkSessionCloseRequest;
        if (pending is null)
        {
            return Result.Success();
        }

        if (choice == WorkSessionCloseChoice.Cancel)
        {
            SetPendingWorkSessionCloseRequest(null);
            return Result.Success();
        }

        ArgumentNullException.ThrowIfNull(coordinator);

        var result = coordinator.ApplyChoice(pending.CloseRequest, choice);
        if (!result.IsSuccess)
        {
            return result;
        }

        SetPendingWorkSessionCloseRequest(null);
        return pending.Action == PendingWorkSessionAction.Navigation
            ? ActivateRoute(pending.RequestedRouteKey)
            : Result.Success();
    }

    public void ToggleSidebarCollapsed()
    {
        Layout = Layout.WithSidebarCollapsed(!Layout.Preference.SidebarCollapsed);
        OnLayoutChanged();
    }

    public void SetTheme(object theme)
    {
        Layout = Layout.WithTheme(theme);
        OnLayoutChanged();
    }

    public void SetShellPreference(ShellLayoutPreference preference)
    {
        ApplyPreference(preference);
    }

    public void ApplyPreference(ShellLayoutPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        if (!usesStaticNavigation)
        {
            navigation = NavigationComposer.Compose(contributions, preference.NavigationOverrides);
        }

        Layout = ShellLayoutState.CreateDefault(navigation) with
        {
            Preference = preference,
            SettingsCategories = Layout.SettingsCategories,
            AboutFields = Layout.AboutFields,
            BackgroundOperationText = Layout.BackgroundOperationText,
            BackgroundOperationPercent = Layout.BackgroundOperationPercent,
            BackgroundOperationIsIndeterminate = Layout.BackgroundOperationIsIndeterminate,
            Theme = Layout.Theme
        };
        RebuildNavigationLevels(preference.LastActiveModuleRouteKey ?? RouteRegistry.HomeRouteKey);
        OnLayoutChanged();
    }

    public ShellLayoutPreference ExportPreference(
        double width,
        double height,
        double? left,
        double? top)
    {
        return Layout.Preference with
        {
            WindowPlacement = new ShellWindowPlacement(width, height, left, top),
            LastActiveModuleRouteKey = ActiveRouteKey
        };
    }

    public void SetSettingsAndAbout(
        IReadOnlyList<ShellSettingsCategoryItem> settingsCategories,
        IReadOnlyList<ShellAboutField> aboutFields)
    {
        Layout = Layout.WithSettingsAndAbout(settingsCategories, aboutFields);
        OnLayoutChanged();
        OnPropertyChanged(nameof(SettingsCategories));
        OnPropertyChanged(nameof(AboutFields));
    }

    public void ShowBackgroundOperation(BackgroundOperationSnapshot snapshot)
    {
        Layout = Layout.WithBackgroundOperation(snapshot);
        OnLayoutChanged();
        OnPropertyChanged(nameof(BackgroundOperationText));
        OnPropertyChanged(nameof(BackgroundOperationPercent));
        OnPropertyChanged(nameof(BackgroundOperationIsIndeterminate));
    }

    public void SetActivePluginPage(IPluginPageModel? model)
    {
        activePluginPageModel = model;
        OnPropertyChanged(nameof(ActivePluginPageModel));
        OnPropertyChanged(nameof(HasActivePluginPage));
    }

    public void SetLicenseSurface(
        bool canViewStatus,
        bool canImport,
        PluginRuntimeMode currentMode,
        string? feedbackKey = null)
    {
        canViewLicenseStatus = canViewStatus;
        canImportLicense = canImport;
        licenseRuntimeMode = currentMode;
        licenseFeedbackKey = feedbackKey;

        OnPropertyChanged(nameof(LicensePanelVisibility));
        OnPropertyChanged(nameof(LicenseStatusVisibility));
        OnPropertyChanged(nameof(LicenseImportVisibility));
        OnPropertyChanged(nameof(LicenseRuntimeModeText));
        OnPropertyChanged(nameof(LicenseFeedbackText));
    }

    public void SetLicenseImportFeedback(string? feedbackKey)
    {
        licenseFeedbackKey = feedbackKey;
        OnPropertyChanged(nameof(LicenseFeedbackText));
    }

    public void SetLicenseRuntimeMode(PluginRuntimeMode currentMode, string? feedbackKey = null)
    {
        licenseRuntimeMode = currentMode;
        licenseFeedbackKey = feedbackKey;
        OnPropertyChanged(nameof(LicenseRuntimeModeText));
        OnPropertyChanged(nameof(LicenseFeedbackText));
    }

    public void ApplyPluginNavigationContributions(IEnumerable<NavigationContribution> pluginContributions)
    {
        ArgumentNullException.ThrowIfNull(pluginContributions);

        contributions = pluginContributions.ToArray();
        navigation = NavigationComposer.Compose(contributions, Layout.Preference.NavigationOverrides);
        var requestedRoute = ActiveRouteKey;
        Layout = ShellLayoutState.CreateDefault(navigation) with
        {
            Preference = Layout.Preference,
            SettingsCategories = Layout.SettingsCategories,
            AboutFields = Layout.AboutFields,
            BackgroundOperationText = Layout.BackgroundOperationText,
            BackgroundOperationPercent = Layout.BackgroundOperationPercent,
            BackgroundOperationIsIndeterminate = Layout.BackgroundOperationIsIndeterminate,
            Theme = Layout.Theme
        };
        RebuildNavigationLevels(requestedRoute);
        OnLayoutChanged();
    }

    private void OnLayoutChanged()
    {
        OnPropertyChanged(nameof(Layout));
        OnPropertyChanged(nameof(ProfileMenuItems));
        OnPropertyChanged(nameof(FocusTraversal));
        OnPropertyChanged(nameof(NotificationPresentation));
        OnPropertyChanged(nameof(FeedbackStatePresentation));
    }

    private void RebuildNavigationLevels(string? routeKey)
    {
        var path = navigation.ResolvePathOrHome(routeKey);
        var top = path[0];
        var sidebar = path.Count > 1 ? path[1] : null;

        TopNavigation = navigation.TopToolbar
            .Select(BuildNavigationItem)
            .ToArray();
        SidebarNavigation = top.Children
            .Select(BuildNavigationItem)
            .ToArray();
        ThirdBarNavigation = (sidebar?.Children ?? [])
            .Select(BuildNavigationItem)
            .ToArray();
        ActiveRouteKey = path[^1].RouteKey;

        OnPropertyChanged(nameof(TopNavigation));
        OnPropertyChanged(nameof(SidebarNavigation));
        OnPropertyChanged(nameof(ThirdBarNavigation));
        OnPropertyChanged(nameof(ActiveRouteKey));
    }

    private ShellNavigationItem BuildNavigationItem(NavigationNode node, int index)
    {
        return new ShellNavigationItem(node.RouteKey, strings.GetString(node.LabelKey), index);
    }

    private static string NotificationLabelKey(ShellNotificationKind kind)
    {
        return kind switch
        {
            ShellNotificationKind.Toast => HostResourceKeys.ShellNotificationToast,
            ShellNotificationKind.Banner => HostResourceKeys.ShellNotificationBanner,
            ShellNotificationKind.Modal => HostResourceKeys.ShellNotificationModal,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static string FeedbackLabelKey(ShellFeedbackState state)
    {
        return state switch
        {
            ShellFeedbackState.Loading => HostResourceKeys.ShellFeedbackLoading,
            ShellFeedbackState.Empty => HostResourceKeys.ShellFeedbackEmpty,
            ShellFeedbackState.Success => HostResourceKeys.ShellFeedbackSuccess,
            ShellFeedbackState.Error => HostResourceKeys.ShellFeedbackError,
            ShellFeedbackState.Confirmation => HostResourceKeys.ShellFeedbackConfirmation,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    private static string LicenseModeKey(PluginRuntimeMode mode)
    {
        return mode switch
        {
            PluginRuntimeMode.FullAccess => HostResourceKeys.LicenseModeFullAccess,
            PluginRuntimeMode.ReadOnlyExpired => HostResourceKeys.LicenseModeReadOnlyExpired,
            _ => HostResourceKeys.LicenseModeDenied
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record ShellNavigationItem(string RouteKey, string Label, int Order);

public sealed record ShellPresentationItem(string Key, string Label);
