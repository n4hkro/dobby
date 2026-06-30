using Dobby.Core;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.Shell.Layout;
using Dobby.Shell.WorkspaceTabs;
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
    private bool licenseImportBusy;
    private PluginRuntimeMode licenseRuntimeMode = PluginRuntimeMode.Denied;
    private string? licenseFeedbackKey;
    private string? licensePluginId;
    private string? licensePluginName;
    private bool canViewPluginsManager;
    private bool canImportPluginsManager;
    private bool canConfigurePluginsManager;
    private bool canDeletePluginsManager;
    private bool pluginsManagerBusy;
    private IReadOnlyList<ShellPluginManagerItem> pluginsManagerItems = [];
    private string? selectedPluginManagerPluginId;
    private string? pluginsManagerFeedbackKey;
    private readonly List<ShellWorkspaceTab> workspaceTabs = [];

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
        ResetWorkspaceTabsToHome();
        RebuildNavigationLevels(RouteRegistry.HomeRouteKey, updateActiveWorkspaceTab: false);
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
        ResetWorkspaceTabsToHome();
        RebuildNavigationLevels(RouteRegistry.HomeRouteKey, updateActiveWorkspaceTab: false);
    }

    public static ShellViewModel CreateDefault(HostStringCatalog strings)
    {
        return new ShellViewModel(strings);
    }

    public IReadOnlyList<ShellNavigationItem> TopNavigation { get; private set; } = [];

    public IReadOnlyList<ShellNavigationItem> SidebarNavigation { get; private set; } = [];

    public IReadOnlyList<ShellNavigationItem> ThirdBarNavigation { get; private set; } = [];

    public string BrandName => strings.GetString(HostResourceKeys.ShellBrand);

    public string BrandMark => "D";

    public IReadOnlyList<ShellWorkspaceTab> WorkspaceTabs => workspaceTabs.ToArray();

    public ShellWorkspaceTab ActiveWorkspaceTab => workspaceTabs.First(tab => tab.IsActive);

    public string WorkspaceNewTabLabel => strings.GetString(HostResourceKeys.ShellWorkspaceNewTab);

    public string WorkspaceCloseTabLabel => strings.GetString(HostResourceKeys.ShellWorkspaceCloseTab);

    public string WindowMinimizeLabel => strings.GetString(HostResourceKeys.ShellWindowMinimize);

    public string WindowMaximizeLabel => strings.GetString(HostResourceKeys.ShellWindowMaximize);

    public string WindowRestoreLabel => strings.GetString(HostResourceKeys.ShellWindowRestore);

    public string WindowCloseLabel => strings.GetString(HostResourceKeys.ShellWindowClose);

    public string StatusText => strings.GetString(HostResourceKeys.ShellStatusReady);

    public Visibility HomePanelVisibility => IsActiveBuiltInRoute(RouteRegistry.HomeRouteKey) && !HasActivePluginPage
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility PluginsSurfaceVisibility => IsActiveBuiltInRoute(RouteRegistry.PluginsRouteKey) && !HasActivePluginPage
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility SettingsSurfaceVisibility => IsActiveBuiltInRoute(RouteRegistry.SettingsRouteKey) && !HasActivePluginPage
        ? Visibility.Visible
        : Visibility.Collapsed;

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

    public string PluginsManagerHeading => strings.GetString(HostResourceKeys.PluginsManagerHeading);

    public string PluginsManagerInstallLabel => strings.GetString(HostResourceKeys.PluginsManagerInstall);

    public string PluginsManagerEnableLabel => strings.GetString(HostResourceKeys.PluginsManagerEnable);

    public string PluginsManagerDisableLabel => strings.GetString(HostResourceKeys.PluginsManagerDisable);

    public string PluginsManagerUninstallLabel => strings.GetString(HostResourceKeys.PluginsManagerUninstall);

    public string PluginsManagerEmptyText => strings.GetString(HostResourceKeys.PluginsManagerEmpty);

    public string PluginsManagerFeedbackText => string.IsNullOrWhiteSpace(pluginsManagerFeedbackKey)
        ? string.Empty
        : strings.GetString(pluginsManagerFeedbackKey);

    public IReadOnlyList<ShellPluginManagerItem> PluginsManagerItems => canViewPluginsManager
        ? pluginsManagerItems
        : [];

    public ShellPluginManagerItem? PluginsManagerSelectedItem => pluginsManagerItems
        .FirstOrDefault(item => item.PluginId.Equals(selectedPluginManagerPluginId, StringComparison.Ordinal));

    public string PluginsManagerSelectedDetailText => PluginsManagerSelectedItem is { } selected
        ? PluginManagerDetailText(selected)
        : string.Empty;

    public Visibility PluginsManagerPanelVisibility => PluginsSurfaceVisibility == Visibility.Visible &&
        (canViewPluginsManager || canImportPluginsManager)
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility PluginsManagerListVisibility => canViewPluginsManager && pluginsManagerItems.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility PluginsManagerEmptyVisibility => canViewPluginsManager && pluginsManagerItems.Count == 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility PluginsManagerInstallVisibility => canImportPluginsManager
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool PluginsManagerCanView => canViewPluginsManager;

    public bool PluginsManagerCanImport => canImportPluginsManager;

    public bool PluginsManagerCanConfigure => canConfigurePluginsManager;

    public bool PluginsManagerCanDelete => canDeletePluginsManager;

    public bool PluginsManagerInstallIsEnabled => canImportPluginsManager && !pluginsManagerBusy;

    public bool PluginsManagerEnableIsEnabled => CanUseSelectedPluginAction(canConfigurePluginsManager) &&
        PluginsManagerSelectedItem is { IsEnabled: false, IsPendingUninstall: false };

    public bool PluginsManagerDisableIsEnabled => CanUseSelectedPluginAction(canConfigurePluginsManager) &&
        PluginsManagerSelectedItem is { IsEnabled: true, IsPendingUninstall: false };

    public bool PluginsManagerUninstallIsEnabled => CanUseSelectedPluginAction(canDeletePluginsManager) &&
        PluginsManagerSelectedItem is { IsPendingUninstall: false };

    public string LicenseHeading => strings.GetString(HostResourceKeys.LicenseHeading);

    public string LicenseStatusLabel => strings.GetString(HostResourceKeys.LicenseStatusLabel);

    public string LicenseListLabel => strings.GetString(HostResourceKeys.LicenseListLabel);

    public string LicenseDetailLabel => strings.GetString(HostResourceKeys.LicenseDetailLabel);

    public string LicenseImportLabel => strings.GetString(HostResourceKeys.LicenseImportAction);

    public string LicenseRuntimeModeText => canViewLicenseStatus
        ? strings.GetString(LicenseModeKey(licenseRuntimeMode))
        : string.Empty;

    public string LicenseListText => canViewLicenseStatus
        ? LicenseListDisplayText()
        : string.Empty;

    public string LicenseDetailText => canViewLicenseStatus
        ? LicenseRuntimeModeText
        : string.Empty;

    public string LicenseFeedbackText => string.IsNullOrWhiteSpace(licenseFeedbackKey)
        ? string.Empty
        : strings.GetString(licenseFeedbackKey);

    public Visibility LicensePanelVisibility => PluginsSurfaceVisibility == Visibility.Visible &&
        (canViewLicenseStatus || canImportLicense)
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LicenseStatusVisibility => canViewLicenseStatus
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LicenseListVisibility => canViewLicenseStatus
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LicenseDetailVisibility => canViewLicenseStatus
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LicenseImportVisibility => canImportLicense
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool LicenseImportIsEnabled => canImportLicense && !licenseImportBusy;

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

    public string DashboardCasesOverviewHeading => strings.GetString(HostResourceKeys.ShellDashboardCasesOverview);

    public IReadOnlyList<ShellDashboardFilterItem> DashboardPipelineFilters =>
    [
        new("pipelines", strings.GetString(HostResourceKeys.ShellDashboardPipelines), IsActive: true),
        new("in-progress", strings.GetString(HostResourceKeys.ShellDashboardInProgress), IsActive: false),
        new("completed", strings.GetString(HostResourceKeys.ShellDashboardCompleted), IsActive: false),
        new("expired", strings.GetString(HostResourceKeys.ShellDashboardExpired), IsActive: false)
    ];

    public IReadOnlyList<ShellDashboardColumn> DashboardPipelineColumns =>
    [
        new(
            "new-cases",
            strings.GetString(HostResourceKeys.ShellDashboardNewCases),
            [
                new ShellDashboardCard("Retro of issues", "Current wait: 19 minutes", "RI", "4"),
                new ShellDashboardCard("Conversion issues", "Approximate wait: 15 minutes", "CI", "2"),
                new ShellDashboardCard("Identify issue resolution", "Expected wait: 25 minutes", "IR", "3")
            ]),
        new(
            "in-progress",
            strings.GetString(HostResourceKeys.ShellDashboardInProgress),
            [
                new ShellDashboardCard("Tech Visionaries", "Wait time: 21 minutes", "TV", "1"),
                new ShellDashboardCard("Innovative Solutions", "Estimated wait: 28 minutes", "IS", "5"),
                new ShellDashboardCard("Future Tech Alliance", "Expected wait: 33 minutes", "FA", "2")
            ]),
        new(
            "pending-info",
            strings.GetString(HostResourceKeys.ShellDashboardPendingInfo),
            [
                new ShellDashboardCard("Retro of issues", "Progress wait: 40 minutes", "RI", "1"),
                new ShellDashboardCard("Identify Dependencies", "Expected wait: 12 minutes", "ID", "3"),
                new ShellDashboardCard("Digital Pioneers", "Expected wait: 8 minutes", "DP", "2")
            ]),
        new(
            "closed",
            strings.GetString(HostResourceKeys.ShellDashboardClosed),
            [
                new ShellDashboardCard("Retro of issues", "Current wait: 25 minutes", "RI", "0")
            ])
    ];

    public IReadOnlyList<ShellDashboardMetric> DashboardMetrics =>
    [
        new(strings.GetString(HostResourceKeys.ShellDashboardTopDrivers), "54%", "Billing inquiry", 54),
        new(strings.GetString(HostResourceKeys.ShellDashboardCaseMetrics), "925", "Resolution score", 76)
    ];

    public event PropertyChangedEventHandler? PropertyChanged;

    public ShellWorkspaceTab OpenWorkspaceTab()
    {
        DeactivateAllWorkspaceTabs();
        var tab = CreateWorkspaceTab(RouteRegistry.HomeRouteKey, isActive: true);
        workspaceTabs.Add(tab);
        RebuildNavigationLevels(tab.RouteKey, updateActiveWorkspaceTab: false);
        NotifyWorkspaceTabsChanged();
        return tab;
    }

    public Result ActivateWorkspaceTab(Guid tabId)
    {
        var tabIndex = IndexOfWorkspaceTab(tabId);
        if (tabIndex < 0)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        for (var index = 0; index < workspaceTabs.Count; index++)
        {
            workspaceTabs[index] = workspaceTabs[index] with
            {
                IsActive = workspaceTabs[index].TabId == tabId
            };
        }

        RebuildNavigationLevels(workspaceTabs[tabIndex].RouteKey, updateActiveWorkspaceTab: false);
        NotifyWorkspaceTabsChanged();
        return Result.Success();
    }

    public Result CloseWorkspaceTab(Guid tabId)
    {
        var tabIndex = IndexOfWorkspaceTab(tabId);
        if (tabIndex < 0)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        if (workspaceTabs.Count == 1)
        {
            ResetWorkspaceTabsToHome();
            RebuildNavigationLevels(RouteRegistry.HomeRouteKey, updateActiveWorkspaceTab: false);
            NotifyWorkspaceTabsChanged();
            return Result.Success();
        }

        var nextActiveIndex = tabIndex == 0 ? 0 : tabIndex - 1;
        workspaceTabs.RemoveAt(tabIndex);
        for (var index = 0; index < workspaceTabs.Count; index++)
        {
            workspaceTabs[index] = workspaceTabs[index] with { IsActive = index == nextActiveIndex };
        }

        RebuildNavigationLevels(workspaceTabs[nextActiveIndex].RouteKey, updateActiveWorkspaceTab: false);
        NotifyWorkspaceTabsChanged();
        return Result.Success();
    }

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
        ResetWorkspaceTabsToHome();
        RebuildNavigationLevels(RouteRegistry.HomeRouteKey, updateActiveWorkspaceTab: false);
        OnLayoutChanged();
        NotifyWorkspaceTabsChanged();
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
        if (model is null)
        {
            UpdateActiveWorkspaceRoute(ActiveRouteKey);
        }
        else
        {
            UpdateActiveWorkspaceTitle(ResolvePluginPageTitle(model));
        }

        OnPropertyChanged(nameof(ActivePluginPageModel));
        OnPropertyChanged(nameof(HasActivePluginPage));
        NotifyRouteSurfaceChanged();
    }

    public void SetLicenseSurface(
        bool canViewStatus,
        bool canImport,
        PluginRuntimeMode currentMode,
        string? feedbackKey = null,
        string? pluginId = null,
        string? pluginName = null)
    {
        canViewLicenseStatus = canViewStatus;
        canImportLicense = canImport;
        licenseRuntimeMode = currentMode;
        licenseFeedbackKey = feedbackKey;
        licensePluginId = pluginId;
        licensePluginName = pluginName;

        OnPropertyChanged(nameof(LicensePanelVisibility));
        OnPropertyChanged(nameof(LicenseStatusVisibility));
        OnPropertyChanged(nameof(LicenseListVisibility));
        OnPropertyChanged(nameof(LicenseDetailVisibility));
        OnPropertyChanged(nameof(LicenseImportVisibility));
        OnPropertyChanged(nameof(LicenseImportIsEnabled));
        OnPropertyChanged(nameof(LicenseRuntimeModeText));
        OnPropertyChanged(nameof(LicenseListText));
        OnPropertyChanged(nameof(LicenseDetailText));
        OnPropertyChanged(nameof(LicenseFeedbackText));
    }

    public void SetPluginsManagerSurface(
        bool canView,
        bool canImport,
        bool canConfigure,
        bool canDelete,
        IReadOnlyList<ShellPluginManagerItem> plugins,
        string? feedbackKey = null,
        bool isBusy = false)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        canViewPluginsManager = canView;
        canImportPluginsManager = canImport;
        canConfigurePluginsManager = canConfigure;
        canDeletePluginsManager = canDelete;
        pluginsManagerBusy = isBusy;
        pluginsManagerItems = plugins
            .OrderBy(plugin => plugin.DisplayName, StringComparer.Ordinal)
            .ThenBy(plugin => plugin.PluginId, StringComparer.Ordinal)
            .ToArray();
        pluginsManagerFeedbackKey = feedbackKey;
        if (selectedPluginManagerPluginId is null ||
            pluginsManagerItems.All(item => !item.PluginId.Equals(selectedPluginManagerPluginId, StringComparison.Ordinal)))
        {
            selectedPluginManagerPluginId = pluginsManagerItems.FirstOrDefault()?.PluginId;
        }

        NotifyPluginsManagerChanged();
    }

    public void SelectPlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        selectedPluginManagerPluginId = pluginsManagerItems.Any(item => item.PluginId.Equals(pluginId, StringComparison.Ordinal))
            ? pluginId
            : null;
        NotifyPluginsManagerChanged();
    }

    public void SetPluginsManagerBusy(bool isBusy, string? feedbackKey = null)
    {
        pluginsManagerBusy = isBusy;
        if (isBusy)
        {
            pluginsManagerFeedbackKey = feedbackKey ?? HostResourceKeys.PluginsManagerInstallBusy;
        }
        else if (IsPluginsManagerBusyFeedbackKey(pluginsManagerFeedbackKey))
        {
            pluginsManagerFeedbackKey = null;
        }

        NotifyPluginsManagerChanged();
    }

    public void SetPluginsManagerFeedback(string? feedbackKey)
    {
        pluginsManagerFeedbackKey = feedbackKey;
        OnPropertyChanged(nameof(PluginsManagerFeedbackText));
    }

    private static bool IsPluginsManagerBusyFeedbackKey(string? feedbackKey)
    {
        return feedbackKey is HostResourceKeys.PluginsManagerInstallBusy
            or HostResourceKeys.PluginsManagerEnableBusy
            or HostResourceKeys.PluginsManagerDisableBusy
            or HostResourceKeys.PluginsManagerUninstallBusy;
    }

    public void SetLicenseImportFeedback(string? feedbackKey)
    {
        licenseFeedbackKey = feedbackKey;
        OnPropertyChanged(nameof(LicenseFeedbackText));
    }

    public void SetLicenseImportBusy(bool isBusy)
    {
        licenseImportBusy = isBusy;
        if (isBusy)
        {
            licenseFeedbackKey = HostResourceKeys.LicenseImportBusy;
        }
        else if (licenseFeedbackKey == HostResourceKeys.LicenseImportBusy)
        {
            licenseFeedbackKey = null;
        }

        OnPropertyChanged(nameof(LicenseImportIsEnabled));
        OnPropertyChanged(nameof(LicenseFeedbackText));
    }

    public void SetLicenseRuntimeMode(PluginRuntimeMode currentMode, string? feedbackKey = null)
    {
        licenseRuntimeMode = currentMode;
        licenseFeedbackKey = feedbackKey;
        OnPropertyChanged(nameof(LicenseRuntimeModeText));
        OnPropertyChanged(nameof(LicenseDetailText));
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

    private void NotifyPluginsManagerChanged()
    {
        OnPropertyChanged(nameof(PluginsManagerPanelVisibility));
        OnPropertyChanged(nameof(PluginsManagerListVisibility));
        OnPropertyChanged(nameof(PluginsManagerEmptyVisibility));
        OnPropertyChanged(nameof(PluginsManagerInstallVisibility));
        OnPropertyChanged(nameof(PluginsManagerCanView));
        OnPropertyChanged(nameof(PluginsManagerCanImport));
        OnPropertyChanged(nameof(PluginsManagerCanConfigure));
        OnPropertyChanged(nameof(PluginsManagerCanDelete));
        OnPropertyChanged(nameof(PluginsManagerInstallIsEnabled));
        OnPropertyChanged(nameof(PluginsManagerEnableIsEnabled));
        OnPropertyChanged(nameof(PluginsManagerDisableIsEnabled));
        OnPropertyChanged(nameof(PluginsManagerUninstallIsEnabled));
        OnPropertyChanged(nameof(PluginsManagerItems));
        OnPropertyChanged(nameof(PluginsManagerSelectedItem));
        OnPropertyChanged(nameof(PluginsManagerSelectedDetailText));
        OnPropertyChanged(nameof(PluginsManagerFeedbackText));
    }

    private void RebuildNavigationLevels(string? routeKey, bool updateActiveWorkspaceTab = true)
    {
        var path = navigation.ResolvePathOrHome(routeKey);
        var top = path[0];
        var sidebar = path.Count > 1 ? path[1] : null;

        var activeRouteKeys = path.Select(node => node.RouteKey).ToHashSet(StringComparer.Ordinal);

        TopNavigation = navigation.TopToolbar
            .Select((node, index) => BuildNavigationItem(node, index, activeRouteKeys))
            .ToArray();
        SidebarNavigation = top.Children
            .Select((node, index) => BuildNavigationItem(node, index, activeRouteKeys))
            .ToArray();
        ThirdBarNavigation = (sidebar?.Children ?? [])
            .Select((node, index) => BuildNavigationItem(node, index, activeRouteKeys))
            .ToArray();
        ActiveRouteKey = path[^1].RouteKey;
        if (updateActiveWorkspaceTab)
        {
            UpdateActiveWorkspaceRoute(ActiveRouteKey);
        }

        OnPropertyChanged(nameof(TopNavigation));
        OnPropertyChanged(nameof(SidebarNavigation));
        OnPropertyChanged(nameof(ThirdBarNavigation));
        OnPropertyChanged(nameof(ActiveRouteKey));
        NotifyRouteSurfaceChanged();
    }

    private ShellNavigationItem BuildNavigationItem(
        NavigationNode node,
        int index,
        IReadOnlySet<string> activeRouteKeys)
    {
        return new ShellNavigationItem(
            node.RouteKey,
            strings.GetString(node.LabelKey),
            index,
            NavigationIconPathData(node.RouteKey, index),
            activeRouteKeys.Contains(node.RouteKey));
    }

    private static string NavigationIconPathData(string routeKey, int index)
    {
        if (routeKey.Equals(RouteRegistry.HomeRouteKey, StringComparison.Ordinal))
        {
            return "M3,10 L10,4 L17,10 V18 H12 V13 H8 V18 H3 Z";
        }

        if (routeKey.Equals(RouteRegistry.PluginsRouteKey, StringComparison.Ordinal))
        {
            return "M6,3 H14 V8 H19 V16 H14 V21 H6 V16 H1 V8 H6 Z";
        }

        if (routeKey.Equals(RouteRegistry.SettingsRouteKey, StringComparison.Ordinal))
        {
            return "M10,2 L12,2 L13,5 L16,6 L18,4 L20,6 L18,8 L19,11 L22,12 L22,14 L19,15 L18,18 L20,20 L18,22 L16,20 L13,21 L12,24 L10,24 L9,21 L6,20 L4,22 L2,20 L4,18 L3,15 L0,14 L0,12 L3,11 L4,8 L2,6 L4,4 L6,6 L9,5 Z M11,9 A4,4 0 1 0 11,17 A4,4 0 1 0 11,9";
        }

        return (index % 3) switch
        {
            0 => "M4,4 H18 V8 H4 Z M4,11 H18 V15 H4 Z M4,18 H18 V22 H4 Z",
            1 => "M12,3 L20,8 V18 L12,23 L4,18 V8 Z",
            _ => "M4,5 H20 V9 H4 Z M4,13 H20 V17 H4 Z"
        };
    }

    private bool IsActiveBuiltInRoute(string routeKey)
    {
        return ActiveRouteKey.Equals(routeKey, StringComparison.Ordinal);
    }

    private void NotifyRouteSurfaceChanged()
    {
        OnPropertyChanged(nameof(HomePanelVisibility));
        OnPropertyChanged(nameof(PluginsSurfaceVisibility));
        OnPropertyChanged(nameof(SettingsSurfaceVisibility));
        OnPropertyChanged(nameof(PluginsManagerPanelVisibility));
        OnPropertyChanged(nameof(LicensePanelVisibility));
    }

    private void ResetWorkspaceTabsToHome()
    {
        workspaceTabs.Clear();
        workspaceTabs.Add(CreateWorkspaceTab(RouteRegistry.HomeRouteKey, isActive: true));
    }

    private ShellWorkspaceTab CreateWorkspaceTab(string routeKey, bool isActive)
    {
        return new ShellWorkspaceTab(
            Guid.CreateVersion7(),
            ResolveWorkspaceTitle(routeKey),
            routeKey,
            isActive,
            CanClose: true);
    }

    private string ResolveWorkspaceTitle(string routeKey)
    {
        var path = navigation.ResolvePathOrHome(routeKey);
        return strings.GetString(path[^1].LabelKey);
    }

    private string ResolvePluginPageTitle(IPluginPageModel model)
    {
        var label = model.GetType().GetProperty("Label")?.GetValue(model) as string;
        return string.IsNullOrWhiteSpace(label)
            ? ResolveWorkspaceTitle(ActiveRouteKey)
            : label;
    }

    private int IndexOfWorkspaceTab(Guid tabId)
    {
        for (var index = 0; index < workspaceTabs.Count; index++)
        {
            if (workspaceTabs[index].TabId == tabId)
            {
                return index;
            }
        }

        return -1;
    }

    private void DeactivateAllWorkspaceTabs()
    {
        for (var index = 0; index < workspaceTabs.Count; index++)
        {
            workspaceTabs[index] = workspaceTabs[index] with { IsActive = false };
        }
    }

    private void UpdateActiveWorkspaceRoute(string routeKey)
    {
        var activeIndex = workspaceTabs.FindIndex(tab => tab.IsActive);
        if (activeIndex < 0)
        {
            return;
        }

        workspaceTabs[activeIndex] = workspaceTabs[activeIndex] with
        {
            RouteKey = routeKey,
            Title = ResolveWorkspaceTitle(routeKey)
        };
        NotifyWorkspaceTabsChanged();
    }

    private void UpdateActiveWorkspaceTitle(string title)
    {
        var activeIndex = workspaceTabs.FindIndex(tab => tab.IsActive);
        if (activeIndex < 0)
        {
            return;
        }

        workspaceTabs[activeIndex] = workspaceTabs[activeIndex] with
        {
            Title = title
        };
        NotifyWorkspaceTabsChanged();
    }

    private void NotifyWorkspaceTabsChanged()
    {
        OnPropertyChanged(nameof(WorkspaceTabs));
        OnPropertyChanged(nameof(ActiveWorkspaceTab));
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

    private string LicenseListDisplayText()
    {
        if (!string.IsNullOrWhiteSpace(licensePluginName) && !string.IsNullOrWhiteSpace(licensePluginId))
        {
            return $"{licensePluginName} ({licensePluginId})";
        }

        if (!string.IsNullOrWhiteSpace(licensePluginName))
        {
            return licensePluginName;
        }

        return string.IsNullOrWhiteSpace(licensePluginId)
            ? strings.GetString(HostResourceKeys.LicenseNoPlugin)
            : licensePluginId;
    }

    private bool CanUseSelectedPluginAction(bool hasPermission)
    {
        return hasPermission &&
            canViewPluginsManager &&
            !pluginsManagerBusy &&
            PluginsManagerSelectedItem is not null;
    }

    private string PluginManagerDetailText(ShellPluginManagerItem plugin)
    {
        var detail = $"{plugin.DisplayName} {plugin.Version} - {plugin.StatusSummary}";
        return plugin.RestartRequired
            ? $"{detail} - {strings.GetString(HostResourceKeys.PluginsManagerRestartRequired)}"
            : detail;
    }
}

public sealed record ShellNavigationItem(
    string RouteKey,
    string Label,
    int Order,
    string IconPathData,
    bool IsActive);

public sealed record ShellPresentationItem(string Key, string Label);

public sealed record ShellDashboardFilterItem(string Key, string Label, bool IsActive);

public sealed record ShellDashboardColumn(
    string Key,
    string Title,
    IReadOnlyList<ShellDashboardCard> Cards);

public sealed record ShellDashboardCard(
    string Title,
    string Detail,
    string Initials,
    string Status);

public sealed record ShellDashboardMetric(
    string Title,
    string Value,
    string Caption,
    int Percent);

public sealed record ShellPluginManagerItem(
    string PluginId,
    string DisplayName,
    string Version,
    string Description,
    bool IsEnabled,
    string StatusSummary,
    bool IsPendingUninstall,
    bool RestartRequired);
