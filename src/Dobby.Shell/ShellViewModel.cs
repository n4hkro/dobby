using Dobby.Core;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.Shell.Layout;
using Dobby.Shell.State;
using Dobby.Shell.WorkspaceTabs;
using Dobby.Shell.WorkSessions;
using System.Windows;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Dobby.Shell;

public sealed class ShellViewModel : INotifyPropertyChanged
{
    private IReadOnlyList<NavigationContribution> contributions;
    private readonly bool usesStaticNavigation;
    private readonly HostStringCatalog strings;
    private NavigationComposition navigation;
    private IPluginPageModel? activePluginPageModel;
    private readonly ShellLicenseState licenseState;
    private readonly ShellPluginManagerState pluginManagerState;
    private IReadOnlyList<ShellMinimizedWorkSessionItem> minimizedWorkSessions = [];
    private readonly List<ShellWorkspaceTab> workspaceTabs = [];
    private string statusTextKey = HostResourceKeys.ShellStatusReady;
    private string? feedbackMessageKey;
    private string? notificationMessageKey;
    private string? accountStatusMessageKey;
    private bool securityAutoLockEnabled;
    private int securityAutoLockMinutes = 30;
    private bool userManagementAvailable;

    public ShellViewModel(HostStringCatalog strings)
        : this(strings, Array.Empty<NavigationContribution>())
    {
    }

    public ShellViewModel(HostStringCatalog strings, IEnumerable<NavigationContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(contributions);

        this.strings = strings;
        licenseState = new ShellLicenseState();
        pluginManagerState = new ShellPluginManagerState(strings);
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
        licenseState = new ShellLicenseState();
        pluginManagerState = new ShellPluginManagerState(strings);
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

    public string StatusText => strings.GetString(statusTextKey);

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

    public string WorkSessionRestoreLabel => strings.GetString(HostResourceKeys.ShellWorkSessionRestore);

    public string SidebarToggleLabel => strings.GetString(HostResourceKeys.ShellSidebarToggle);

    public string ThemeToggleLabel => strings.GetString(HostResourceKeys.ShellThemeToggle);

    public string RememberMeLabel => strings.GetString(HostResourceKeys.LoginRememberMe);

    public string ProfileAccountInfoLabel => strings.GetString(HostResourceKeys.ShellProfileAccountInfo);

    public string ProfileAnonymousLabel => strings.GetString(HostResourceKeys.ShellProfileAnonymous);

    public string ChangePasswordLabel => strings.GetString(HostResourceKeys.ShellProfileChangePassword);

    public string CurrentPasswordLabel => strings.GetString(HostResourceKeys.ShellProfileCurrentPassword);

    public string NewPasswordLabel => strings.GetString(HostResourceKeys.ShellProfileNewPassword);

    public string ConfirmNewPasswordLabel => strings.GetString(HostResourceKeys.ShellProfileConfirmNewPassword);

    public string ManualLockLabel => strings.GetString(HostResourceKeys.ShellProfileManualLock);

    public string LogoutLabel => strings.GetString(HostResourceKeys.ShellProfileLogout);

    public string SettingsHeading => strings.GetString(HostResourceKeys.ShellSettingsHeading);

    public string AboutHeading => strings.GetString(HostResourceKeys.ShellAboutHeading);

    public string SettingsAppearanceLabel => strings.GetString(HostResourceKeys.SettingsAppearance);

    public string SettingsAppearanceThemeLabel => strings.GetString(HostResourceKeys.SettingsAppearanceTheme);

    public string SettingsAccountLabel => strings.GetString(HostResourceKeys.SettingsAccount);

    public string SettingsSecuritySessionLabel => strings.GetString(HostResourceKeys.SettingsSecuritySession);

    public string SettingsSecuritySessionAutoLockEnabledLabel => strings.GetString(HostResourceKeys.SettingsSecuritySessionAutoLockEnabled);

    public string SettingsSecuritySessionAutoLockMinutesLabel => strings.GetString(HostResourceKeys.SettingsSecuritySessionAutoLockMinutes);

    public string SettingsSecuritySessionSaveLabel => strings.GetString(HostResourceKeys.SettingsSecuritySessionSave);

    public string SettingsUserManagementLabel => strings.GetString(HostResourceKeys.SettingsUserManagement);

    public string SettingsAboutLabel => strings.GetString(HostResourceKeys.SettingsAbout);

    public string AboutDeviceCodeExplanation => strings.GetString(HostResourceKeys.AboutDeviceCodeExplanation);

    public string AboutDeviceCodeCopyLabel => strings.GetString(HostResourceKeys.AboutDeviceCodeCopy);

    public string AboutDeviceCode => AboutFields
        .FirstOrDefault(aboutField => aboutField.Key.Equals("device-code", StringComparison.Ordinal))?.Value ?? string.Empty;

    public Visibility AboutDeviceCodeActionsVisibility => string.IsNullOrWhiteSpace(AboutDeviceCode)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public IReadOnlyList<ShellThemeOptionItem> AppearanceThemeOptions =>
    [
        new("Light", strings.GetString(HostResourceKeys.SettingsAppearanceThemeLight)),
        new("Dark", strings.GetString(HostResourceKeys.SettingsAppearanceThemeDark))
    ];

    public string AppearanceSelectedTheme => Layout.Theme.ToString() ?? "Light";

    public bool SecurityAutoLockEnabled => securityAutoLockEnabled;

    public int SecurityAutoLockMinutes => securityAutoLockMinutes;

    public string SecurityAutoLockMinutesText => securityAutoLockMinutes.ToString(CultureInfo.InvariantCulture);

    public bool UserManagementIsAvailable => userManagementAvailable;

    public Visibility UserManagementSurfaceVisibility => UserManagementIsAvailable
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility UserManagementActionsVisibility => UserManagementIsAvailable
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility UserManagementDeniedVisibility => UserManagementIsAvailable
        ? Visibility.Collapsed
        : Visibility.Visible;

    public bool UserManagementActionsEnabled => UserManagementIsAvailable;

    public string UserManagementPermissionDeniedText => strings.GetString(HostResourceKeys.UserManagementPermissionDenied);

    public IReadOnlyList<ShellPresentationItem> UserManagementActionItems => UserManagementIsAvailable
        ?
        [
            new("create-managed-user", strings.GetString(HostResourceKeys.UserManagementCreateManagedUser)),
            new("update-display-name", strings.GetString(HostResourceKeys.UserManagementUpdateDisplayName)),
            new("update-active-state", strings.GetString(HostResourceKeys.UserManagementUpdateActiveState)),
            new("reset-password", strings.GetString(HostResourceKeys.UserManagementResetPassword)),
            new("grant-permission", strings.GetString(HostResourceKeys.UserManagementGrantPermission)),
            new("revoke-permission", strings.GetString(HostResourceKeys.UserManagementRevokePermission))
        ]
        : [];

    public IReadOnlyList<string> UserManagementActionLabels => UserManagementActionItems
        .Select(item => item.Label)
        .ToArray();

    public string MinimizedWorkStripLabel => strings.GetString(HostResourceKeys.ShellMinimizedWorkStrip);

    public string ThirdBarLabel => strings.GetString(HostResourceKeys.ShellThirdBar);

    public string BackgroundOperationLabel => strings.GetString(HostResourceKeys.ShellBackgroundOperation);

    public string PluginsManagerHeading => strings.GetString(HostResourceKeys.PluginsManagerHeading);

    public string PluginsManagerInstallLabel => strings.GetString(HostResourceKeys.PluginsManagerInstall);

    public string PluginsManagerEnableLabel => strings.GetString(HostResourceKeys.PluginsManagerEnable);

    public string PluginsManagerDisableLabel => strings.GetString(HostResourceKeys.PluginsManagerDisable);

    public string PluginsManagerUninstallLabel => strings.GetString(HostResourceKeys.PluginsManagerUninstall);

    public string CancelOperationLabel => strings.GetString(HostResourceKeys.ShellCancelOperation);

    public string PluginsManagerEmptyText => strings.GetString(HostResourceKeys.PluginsManagerEmpty);

    public string PluginPageUnavailableText => strings.GetString(HostResourceKeys.PluginPageUnavailable);

    public string PluginsManagerFeedbackText => string.IsNullOrWhiteSpace(pluginManagerState.FeedbackKey)
        ? string.Empty
        : strings.GetString(pluginManagerState.FeedbackKey);

    public IReadOnlyList<ShellPluginManagerItem> PluginsManagerItems => pluginManagerState.CanView
        ? pluginManagerState.Items
        : [];

    public ShellPluginManagerItem? PluginsManagerSelectedItem => pluginManagerState.SelectedItem;

    public string PluginsManagerSelectedDetailText => PluginsManagerSelectedItem is { } selected
        ? pluginManagerState.DetailText(selected)
        : string.Empty;

    public IReadOnlyList<ShellPluginManagerDetailRow> PluginsManagerSelectedDetailRows => PluginsManagerSelectedItem is { } selected
        ? pluginManagerState.DetailRows(selected)
        : [];

    public Visibility PluginsManagerPanelVisibility => PluginsSurfaceVisibility == Visibility.Visible &&
        (pluginManagerState.CanView || pluginManagerState.CanImport)
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility PluginsManagerListVisibility => pluginManagerState.CanView && pluginManagerState.Items.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility PluginsManagerEmptyVisibility => pluginManagerState.CanView && pluginManagerState.Items.Count == 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility PluginsManagerInstallVisibility => pluginManagerState.CanImport
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool PluginsManagerCanView => pluginManagerState.CanView;

    public bool PluginsManagerCanImport => pluginManagerState.CanImport;

    public bool PluginsManagerCanConfigure => pluginManagerState.CanConfigure;

    public bool PluginsManagerCanDelete => pluginManagerState.CanDelete;

    public bool PluginsManagerInstallIsEnabled => pluginManagerState.CanImport &&
        !pluginManagerState.IsBusy &&
        (!licenseState.CanViewStatus || licenseState.IsActionAllowed(PluginRuntimeAction.InitialPackageInstall));

    public bool PluginsManagerEnableIsEnabled => CanUseSelectedPluginAction(pluginManagerState.CanConfigure) &&
        PluginsManagerSelectedItem is { IsEnabled: false, IsPendingUninstall: false };

    public bool PluginsManagerDisableIsEnabled => CanUseSelectedPluginAction(pluginManagerState.CanConfigure) &&
        PluginsManagerSelectedItem is { IsEnabled: true, IsPendingUninstall: false };

    public bool PluginsManagerUninstallIsEnabled => CanUseSelectedPluginAction(pluginManagerState.CanDelete) &&
        PluginsManagerSelectedItem is { IsPendingUninstall: false };

    public Visibility PluginsManagerCancelVisibility => pluginManagerState.IsBusy
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string LicenseHeading => strings.GetString(HostResourceKeys.LicenseHeading);

    public string LicenseStatusLabel => strings.GetString(HostResourceKeys.LicenseStatusLabel);

    public string LicenseListLabel => strings.GetString(HostResourceKeys.LicenseListLabel);

    public string LicenseDetailLabel => strings.GetString(HostResourceKeys.LicenseDetailLabel);

    public string LicenseImportLabel => strings.GetString(HostResourceKeys.LicenseImportAction);

    public string LicenseRuntimeModeText => licenseState.CanViewStatus
        ? strings.GetString(LicenseModeKey(licenseState.RuntimeMode))
        : string.Empty;

    public string LicenseListText => licenseState.CanViewStatus
        ? licenseState.ListDisplayText(strings)
        : string.Empty;

    public string LicenseDetailText => licenseState.CanViewStatus
        ? LicenseRuntimeModeText
        : string.Empty;

    public string LicenseFeedbackText => string.IsNullOrWhiteSpace(licenseState.FeedbackKey)
        ? string.Empty
        : strings.GetString(licenseState.FeedbackKey);

    public Visibility LicensePanelVisibility => PluginsSurfaceVisibility == Visibility.Visible &&
        (licenseState.CanViewStatus || licenseState.CanImport)
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LicenseStatusVisibility => licenseState.CanViewStatus
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LicenseListVisibility => licenseState.CanViewStatus
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LicenseDetailVisibility => licenseState.CanViewStatus
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LicenseImportVisibility => licenseState.CanImport
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool LicenseImportIsEnabled => licenseState.CanImport && !licenseState.ImportBusy;

    public Visibility LicenseImportCancelVisibility => licenseState.ImportBusy
        ? Visibility.Visible
        : Visibility.Collapsed;

    public ShellLayoutState Layout { get; private set; }

    public string ActiveRouteKey { get; private set; } = RouteRegistry.HomeRouteKey;

    public PendingWorkSessionCloseRequest? PendingWorkSessionCloseRequest { get; private set; }

    public bool HasPendingWorkSessionCloseRequest => PendingWorkSessionCloseRequest is not null;

    public Visibility WorkSessionMinimizeVisibility =>
        PendingWorkSessionCloseRequest?.CloseRequest.Choices.Contains(WorkSessionCloseChoice.Minimize) == true
            ? Visibility.Visible
            : Visibility.Collapsed;

    public bool IsSidebarCollapsed => Layout.Preference.SidebarCollapsed;

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

    public bool BackgroundOperationCanCancel => Layout.BackgroundOperationCanCancel;

    public IReadOnlyList<ShellMinimizedWorkSessionItem> MinimizedWorkSessions => minimizedWorkSessions;

    public bool HasMinimizedWorkSessions => minimizedWorkSessions.Count > 0;

    public string FeedbackMessageText => string.IsNullOrWhiteSpace(feedbackMessageKey)
        ? string.Empty
        : strings.GetString(feedbackMessageKey);

    public string AccountStatusMessageText => string.IsNullOrWhiteSpace(accountStatusMessageKey)
        ? string.Empty
        : strings.GetString(accountStatusMessageKey);

    public string NotificationMessageText => string.IsNullOrWhiteSpace(notificationMessageKey)
        ? string.Empty
        : strings.GetString(notificationMessageKey);

    public bool HasFeedbackMessage => !string.IsNullOrWhiteSpace(feedbackMessageKey);

    public bool HasNotificationMessage => !string.IsNullOrWhiteSpace(notificationMessageKey);

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

        return ActivateWorkspaceTab(tabId, workspaceTabs[tabIndex].RouteKey);
    }

    public Result ActivateWorkspaceTab(Guid tabId, string resolvedRouteKey)
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

        RebuildNavigationLevels(resolvedRouteKey);
        return Result.Success();
    }

    public Result<string> SelectWorkspaceTabForRouteActivation(Guid tabId)
    {
        var tabIndex = IndexOfWorkspaceTab(tabId);
        if (tabIndex < 0)
        {
            return Result.Failure<string>(ErrorCode.NotFound);
        }

        return Result.Success(workspaceTabs[tabIndex].RouteKey);
    }

    public Result<string?> CloseWorkspaceTab(Guid tabId)
    {
        var tabIndex = IndexOfWorkspaceTab(tabId);
        if (tabIndex < 0)
        {
            return Result.Failure<string?>(ErrorCode.NotFound);
        }

        var wasActive = workspaceTabs[tabIndex].IsActive;
        if (workspaceTabs.Count == 1)
        {
            return Result.Success<string?>(RouteRegistry.HomeRouteKey);
        }

        var nextActiveIndex = NextActiveWorkspaceTabIndexBeforeClose(tabIndex, workspaceTabs.Count);
        if (wasActive)
        {
            return Result.Success<string?>(workspaceTabs[nextActiveIndex].RouteKey);
        }

        workspaceTabs.RemoveAt(tabIndex);
        NotifyWorkspaceTabsChanged();
        return Result.Success<string?>(null);
    }

    public Result CommitCloseWorkspaceTab(Guid tabId, string resolvedRouteKey)
    {
        var tabIndex = IndexOfWorkspaceTab(tabId);
        if (tabIndex < 0)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        if (!workspaceTabs[tabIndex].IsActive)
        {
            return Result.Success();
        }

        if (workspaceTabs.Count == 1)
        {
            ResetWorkspaceTabsToHome();
            RebuildNavigationLevels(resolvedRouteKey);
            return Result.Success();
        }

        var nextActiveIndex = NextActiveWorkspaceTabIndexAfterClose(tabIndex);
        workspaceTabs.RemoveAt(tabIndex);
        for (var index = 0; index < workspaceTabs.Count; index++)
        {
            workspaceTabs[index] = workspaceTabs[index] with { IsActive = index == nextActiveIndex };
        }

        RebuildNavigationLevels(resolvedRouteKey);
        return Result.Success();
    }

    public Result ActivateRoute(string? routeKey)
    {
        RebuildNavigationLevels(ResolveRouteOrHome(routeKey));
        return Result.Success();
    }

    public string ResolveRouteOrHome(string? routeKey)
    {
        return navigation.ResolveOrHome(routeKey).RouteKey;
    }

    public Result ActivateResolvedRoute(string routeKey)
    {
        RebuildNavigationLevels(routeKey);
        return Result.Success();
    }

    public bool SetPendingWorkSessionCloseRequest(PendingWorkSessionCloseRequest? pendingRequest)
    {
        if (pendingRequest is not null &&
            PendingWorkSessionCloseRequest is not null &&
            PendingWorkSessionCloseRequest.RequestId != pendingRequest.RequestId)
        {
            return false;
        }

        PendingWorkSessionCloseRequest = pendingRequest;
        OnPropertyChanged(nameof(PendingWorkSessionCloseRequest));
        OnPropertyChanged(nameof(HasPendingWorkSessionCloseRequest));
        OnPropertyChanged(nameof(WorkSessionMinimizeVisibility));
        return true;
    }

    public Result<string?> ResolvePendingWorkSessionClose(
        WorkSessionCloseChoice choice,
        WorkSessionCloseCoordinator coordinator,
        PendingWorkSessionCloseRequest? expectedRequest = null)
    {
        var pending = PendingWorkSessionCloseRequest;
        if (pending is null)
        {
            return Result.Success<string?>(null);
        }

        if (expectedRequest is not null && pending.RequestId != expectedRequest.RequestId)
        {
            return Result.Failure<string?>(ErrorCode.InvalidInput);
        }

        if (choice == WorkSessionCloseChoice.Cancel)
        {
            SetPendingWorkSessionCloseRequest(null);
            return Result.Success<string?>(null);
        }

        ArgumentNullException.ThrowIfNull(coordinator);

        if (pending.Action == PendingWorkSessionAction.Navigation)
        {
            return Result.Success<string?>(pending.RequestedRouteKey);
        }

        var result = coordinator.ApplyChoice(pending.CloseRequest, choice);
        if (!result.IsSuccess)
        {
            return Result.Failure<string?>(result.ErrorCode ?? ErrorCode.UnsupportedOperation);
        }

        SetPendingWorkSessionCloseRequest(null);
        return Result.Success<string?>(null);
    }

    public Result CommitPendingWorkSessionClose(
        WorkSessionCloseChoice choice,
        WorkSessionCloseCoordinator coordinator,
        PendingWorkSessionCloseRequest expectedRequest)
    {
        ArgumentNullException.ThrowIfNull(expectedRequest);

        var pending = PendingWorkSessionCloseRequest;
        if (pending is null)
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }

        if (pending.RequestId != expectedRequest.RequestId)
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }

        ArgumentNullException.ThrowIfNull(coordinator);

        var result = coordinator.ApplyChoice(pending.CloseRequest, choice);
        if (!result.IsSuccess)
        {
            return Result.Failure(result.ErrorCode ?? ErrorCode.UnsupportedOperation);
        }

        SetPendingWorkSessionCloseRequest(null);
        return Result.Success();
    }

    public void ToggleSidebarCollapsed()
    {
        Layout = Layout.WithSidebarCollapsed(!Layout.Preference.SidebarCollapsed);
        OnLayoutChanged();
        OnPropertyChanged(nameof(IsSidebarCollapsed));
        OnPropertyChanged(nameof(SidebarNavigation));
    }

    public void ToggleSidebarNode(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        var openNodeIds = Layout.Preference.SidebarOpenNodeIds.ToHashSet(StringComparer.Ordinal);
        if (!openNodeIds.Add(nodeId))
        {
            openNodeIds.Remove(nodeId);
        }

        Layout = Layout with
        {
            Preference = Layout.Preference with
            {
                SidebarOpenNodeIds = openNodeIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()
            }
        };
        RebuildNavigationLevels(ActiveRouteKey, updateActiveWorkspaceTab: false);
        OnLayoutChanged();
        OnPropertyChanged(nameof(SidebarNavigation));
    }

    public void SetTheme(object theme)
    {
        Layout = Layout.WithTheme(theme);
        OnLayoutChanged();
        OnPropertyChanged(nameof(AppearanceSelectedTheme));
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
            BackgroundOperationCanCancel = Layout.BackgroundOperationCanCancel,
            Theme = Layout.Theme
        };
        ResetWorkspaceTabsToHome();
        RebuildNavigationLevels(RouteRegistry.HomeRouteKey, updateActiveWorkspaceTab: false);
        OnLayoutChanged();
        OnPropertyChanged(nameof(IsSidebarCollapsed));
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
        IReadOnlyList<ShellAboutField> aboutFields,
        bool? autoLockEnabled = null,
        int? autoLockMinutes = null)
    {
        userManagementAvailable = settingsCategories.Any(category =>
            category.Key.Equals("user-management", StringComparison.Ordinal) &&
            category.IsAvailable);

        if (autoLockEnabled.HasValue)
        {
            securityAutoLockEnabled = autoLockEnabled.Value;
        }

        if (autoLockMinutes.HasValue)
        {
            securityAutoLockMinutes = autoLockMinutes.Value;
        }

        Layout = Layout.WithSettingsAndAbout(
            settingsCategories.Where(category => category.IsAvailable).ToArray(),
            aboutFields);
        OnLayoutChanged();
        OnPropertyChanged(nameof(SettingsCategories));
        OnPropertyChanged(nameof(AboutFields));
        OnPropertyChanged(nameof(AboutDeviceCode));
        OnPropertyChanged(nameof(AboutDeviceCodeActionsVisibility));
        NotifySettingsSurfaceChanged();
    }

    public void ShowBackgroundOperation(BackgroundOperationSnapshot snapshot)
    {
        Layout = Layout.WithBackgroundOperation(snapshot);
        OnLayoutChanged();
        OnPropertyChanged(nameof(BackgroundOperationText));
        OnPropertyChanged(nameof(BackgroundOperationPercent));
        OnPropertyChanged(nameof(BackgroundOperationIsIndeterminate));
        OnPropertyChanged(nameof(BackgroundOperationCanCancel));
    }

    public void SetMinimizedWorkSessions(IReadOnlyList<WorkSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        minimizedWorkSessions = sessions
            .Where(session => session.IsMinimized)
            .OrderBy(session => session.LabelKey, StringComparer.Ordinal)
            .Select(session => new ShellMinimizedWorkSessionItem(
                session.SessionId,
                ResolveWorkSessionText(session.LabelKey),
                ResolveWorkSessionDetail(session),
                session.Progress.Percent,
                session.Progress.IsIndeterminate))
            .ToArray();
        OnPropertyChanged(nameof(MinimizedWorkSessions));
        OnPropertyChanged(nameof(HasMinimizedWorkSessions));
    }

    public void SetActivePluginPage(IPluginPageModel? model)
    {
        activePluginPageModel = model;
        UpdateActiveWorkspaceRoute(ActiveRouteKey);

        OnPropertyChanged(nameof(ActivePluginPageModel));
        OnPropertyChanged(nameof(HasActivePluginPage));
        NotifyRouteSurfaceChanged();
    }

    public void SetStatus(string statusKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statusKey);

        statusTextKey = statusKey;
        OnPropertyChanged(nameof(StatusText));
    }

    public void SetFeedbackMessage(string? feedbackKey)
    {
        feedbackMessageKey = feedbackKey;
        OnPropertyChanged(nameof(FeedbackMessageText));
        OnPropertyChanged(nameof(HasFeedbackMessage));
    }

    public void SetAccountStatusMessage(string? accountStatusKey)
    {
        accountStatusMessageKey = accountStatusKey;
        OnPropertyChanged(nameof(AccountStatusMessageText));
    }

    public void SetNotificationMessage(string? notificationKey)
    {
        notificationMessageKey = notificationKey;
        OnPropertyChanged(nameof(NotificationMessageText));
        OnPropertyChanged(nameof(HasNotificationMessage));
    }

    public void SetLicenseSurface(
        bool canViewStatus,
        bool canImport,
        PluginRuntimeMode currentMode,
        string? feedbackKey = null,
        string? pluginId = null,
        string? pluginName = null)
    {
        licenseState.SetSurface(
            canViewStatus,
            canImport,
            currentMode,
            feedbackKey,
            pluginId,
            pluginName);

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
        OnPropertyChanged(nameof(PluginsManagerInstallIsEnabled));
        OnPropertyChanged(nameof(PluginsManagerEnableIsEnabled));
        OnPropertyChanged(nameof(PluginsManagerDisableIsEnabled));
        OnPropertyChanged(nameof(PluginsManagerUninstallIsEnabled));
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
        pluginManagerState.SetSurface(
            canView,
            canImport,
            canConfigure,
            canDelete,
            plugins,
            feedbackKey,
            isBusy);

        NotifyPluginsManagerChanged();
        OnPropertyChanged(nameof(PluginsManagerCancelVisibility));
    }

    public void SelectPlugin(string pluginId)
    {
        pluginManagerState.Select(pluginId);
        NotifyPluginsManagerChanged();
    }

    public void SetPluginsManagerBusy(bool isBusy, string? feedbackKey = null)
    {
        pluginManagerState.SetBusy(isBusy, feedbackKey);

        NotifyPluginsManagerChanged();
        OnPropertyChanged(nameof(PluginsManagerCancelVisibility));
    }

    public void SetPluginsManagerFeedback(string? feedbackKey)
    {
        pluginManagerState.SetFeedback(feedbackKey);
        OnPropertyChanged(nameof(PluginsManagerFeedbackText));
    }

    public void SetLicenseImportFeedback(string? feedbackKey)
    {
        licenseState.SetFeedback(feedbackKey);
        OnPropertyChanged(nameof(LicenseFeedbackText));
    }

    public void SetLicenseImportBusy(bool isBusy)
    {
        licenseState.SetImportBusy(isBusy);

        OnPropertyChanged(nameof(LicenseImportIsEnabled));
        OnPropertyChanged(nameof(LicenseImportCancelVisibility));
        OnPropertyChanged(nameof(LicenseFeedbackText));
    }

    public void SetLicenseRuntimeMode(PluginRuntimeMode currentMode, string? feedbackKey = null)
    {
        licenseState.SetRuntimeMode(currentMode, feedbackKey);
        OnPropertyChanged(nameof(LicenseRuntimeModeText));
        OnPropertyChanged(nameof(LicenseDetailText));
        OnPropertyChanged(nameof(LicenseFeedbackText));
        OnPropertyChanged(nameof(PluginsManagerInstallIsEnabled));
        OnPropertyChanged(nameof(PluginsManagerEnableIsEnabled));
        OnPropertyChanged(nameof(PluginsManagerDisableIsEnabled));
        OnPropertyChanged(nameof(PluginsManagerUninstallIsEnabled));
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
            BackgroundOperationCanCancel = Layout.BackgroundOperationCanCancel,
            Theme = Layout.Theme
        };
        RebuildNavigationLevels(requestedRoute);
        OnLayoutChanged();
        OnPropertyChanged(nameof(IsSidebarCollapsed));
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
        OnPropertyChanged(nameof(PluginsManagerSelectedDetailRows));
        OnPropertyChanged(nameof(PluginsManagerFeedbackText));
    }

    private void RebuildNavigationLevels(string? routeKey, bool updateActiveWorkspaceTab = true)
    {
        var path = navigation.ResolvePathOrHome(routeKey);
        var top = path[0];
        var sidebar = path.Count > 1 ? path[1] : null;

        var activeRouteKeys = path.Select(node => node.RouteKey).ToHashSet(StringComparer.Ordinal);
        var openNodeIds = Layout.Preference.SidebarOpenNodeIds.ToHashSet(StringComparer.Ordinal);
        if (sidebar is not null && sidebar.Children.Count > 0)
        {
            openNodeIds.Add(sidebar.Id);
            Layout = Layout with
            {
                Preference = Layout.Preference with
                {
                    SidebarOpenNodeIds = openNodeIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()
                }
            };
        }

        TopNavigation = navigation.TopToolbar
            .Select((node, index) => BuildNavigationItem(node, index, activeRouteKeys, openNodeIds))
            .ToArray();
        SidebarNavigation = top.Children
            .Select((node, index) => BuildNavigationItem(node, index, activeRouteKeys, openNodeIds))
            .ToArray();
        ThirdBarNavigation = (sidebar?.Children ?? [])
            .Select((node, index) => BuildNavigationItem(node, index, activeRouteKeys, openNodeIds))
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
        IReadOnlySet<string> activeRouteKeys,
        IReadOnlySet<string> openNodeIds)
    {
        return new ShellNavigationItem(
            node.Id,
            node.RouteKey,
            strings.GetString(node.LabelKey),
            index,
            NavigationIconPathData(node.RouteKey, index),
            activeRouteKeys.Contains(node.RouteKey),
            node.Children.Count > 0,
            openNodeIds.Contains(node.Id) || activeRouteKeys.Contains(node.RouteKey));
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
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(FeedbackMessageText));
        OnPropertyChanged(nameof(NotificationMessageText));
        OnPropertyChanged(nameof(HasFeedbackMessage));
        OnPropertyChanged(nameof(HasNotificationMessage));
        OnPropertyChanged(nameof(HasMinimizedWorkSessions));
    }

    private void NotifySettingsSurfaceChanged()
    {
        OnPropertyChanged(nameof(SettingsAppearanceLabel));
        OnPropertyChanged(nameof(SettingsAppearanceThemeLabel));
        OnPropertyChanged(nameof(SettingsAccountLabel));
        OnPropertyChanged(nameof(SettingsSecuritySessionLabel));
        OnPropertyChanged(nameof(SettingsSecuritySessionAutoLockEnabledLabel));
        OnPropertyChanged(nameof(SettingsSecuritySessionAutoLockMinutesLabel));
        OnPropertyChanged(nameof(SettingsSecuritySessionSaveLabel));
        OnPropertyChanged(nameof(SettingsUserManagementLabel));
        OnPropertyChanged(nameof(SettingsAboutLabel));
        OnPropertyChanged(nameof(AboutDeviceCodeExplanation));
        OnPropertyChanged(nameof(AboutDeviceCodeCopyLabel));
        OnPropertyChanged(nameof(AppearanceThemeOptions));
        OnPropertyChanged(nameof(AppearanceSelectedTheme));
        OnPropertyChanged(nameof(SecurityAutoLockEnabled));
        OnPropertyChanged(nameof(SecurityAutoLockMinutes));
        OnPropertyChanged(nameof(SecurityAutoLockMinutesText));
        OnPropertyChanged(nameof(UserManagementIsAvailable));
        OnPropertyChanged(nameof(UserManagementSurfaceVisibility));
        OnPropertyChanged(nameof(UserManagementActionsVisibility));
        OnPropertyChanged(nameof(UserManagementDeniedVisibility));
        OnPropertyChanged(nameof(UserManagementActionsEnabled));
        OnPropertyChanged(nameof(UserManagementActionItems));
        OnPropertyChanged(nameof(UserManagementActionLabels));
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

    private static int NextActiveWorkspaceTabIndexBeforeClose(int closingIndex, int tabCount)
    {
        return closingIndex == 0 && tabCount > 1 ? 1 : closingIndex - 1;
    }

    private static int NextActiveWorkspaceTabIndexAfterClose(int closingIndex)
    {
        return closingIndex == 0 ? 0 : closingIndex - 1;
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

    private bool CanUseSelectedPluginAction(bool hasPermission)
    {
        return pluginManagerState.CanUseSelectedAction(
            hasPermission,
            !licenseState.CanViewStatus ||
                licenseState.IsActionAllowed(PluginRuntimeAction.EnableDisableUninstall));
    }

    private string ResolveWorkSessionText(string keyOrText)
    {
        if (keyOrText.StartsWith("Dobby.", StringComparison.Ordinal))
        {
            var localized = strings.GetString(keyOrText);
            if (!localized.StartsWith("[[", StringComparison.Ordinal))
            {
                return localized;
            }
        }

        return keyOrText;
    }

    private string ResolveWorkSessionDetail(WorkSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.ErrorSummaryKey))
        {
            return ResolveWorkSessionText(session.ErrorSummaryKey);
        }

        if (!string.IsNullOrWhiteSpace(session.ResultSummaryKey))
        {
            return ResolveWorkSessionText(session.ResultSummaryKey);
        }

        return session.Progress.IsIndeterminate
            ? BackgroundOperationLabel
            : $"{session.Progress.Percent ?? 0}%";
    }
}

public sealed record ShellNavigationItem(
    string NodeId,
    string RouteKey,
    string Label,
    int Order,
    string IconPathData,
    bool IsActive,
    bool HasChildren,
    bool IsOpen);

public sealed record ShellPresentationItem(string Key, string Label);

public sealed record ShellThemeOptionItem(string Theme, string Label);

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

public enum ShellPluginTrustState
{
    Unknown = 0,
    Verified = 1,
    Rejected = 2
}

public sealed record ShellPluginManagerItem(
    string PluginId,
    string DisplayName,
    string Version,
    string Description,
    bool IsEnabled,
    string StatusSummary,
    bool IsPendingUninstall,
    bool RestartRequired,
    string TrustSummary = "",
    string LicenseSummary = "",
    string FaultSummary = "",
    string SafeRejectionText = "",
    ShellPluginTrustState TrustState = ShellPluginTrustState.Unknown);

public sealed record ShellPluginManagerDetailRow(string Key, string Label, string Value);

public sealed record ShellMinimizedWorkSessionItem(
    Guid SessionId,
    string Title,
    string Detail,
    int? Percent,
    bool IsIndeterminate);
