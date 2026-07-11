using System.Windows;
using Dobby.Localization;
using Dobby.Shell;
using Shouldly;

namespace Dobby.UnitTests.Shell;

public sealed class ShellViewModelPluginsManagerTests
{
    [Fact]
    public void Plugins_manager_surface_shows_list_detail_and_permission_gated_actions()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());

        shell.SetPluginsManagerSurface(
            canView: true,
            canImport: true,
            canConfigure: true,
            canDelete: false,
            [
                new ShellPluginManagerItem(
                    "com.dobby.sample",
                    "Sample plugin",
                    "1.0.0",
                    "Verification plugin",
                    IsEnabled: true,
                    "Enabled",
                    IsPendingUninstall: false,
                    RestartRequired: false,
                    TrustSummary: "Trusted",
                    LicenseSummary: "Full access",
                    FaultSummary: "No faults",
                    SafeRejectionText: "")
            ]);
        shell.SelectPlugin("com.dobby.sample");

        shell.PluginsManagerPanelVisibility.ShouldBe(Visibility.Collapsed);
        shell.ActivateRoute(Dobby.Navigation.RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();

        shell.PluginsManagerPanelVisibility.ShouldBe(Visibility.Visible);
        shell.PluginsManagerListVisibility.ShouldBe(Visibility.Visible);
        shell.PluginsManagerEmptyVisibility.ShouldBe(Visibility.Collapsed);
        shell.PluginsManagerInstallIsEnabled.ShouldBeTrue();
        shell.PluginsManagerDisableIsEnabled.ShouldBeTrue();
        shell.PluginsManagerEnableIsEnabled.ShouldBeFalse();
        shell.PluginsManagerUninstallIsEnabled.ShouldBeFalse();
        shell.PluginsManagerSelectedDetailText.ShouldBe("Sample plugin 1.0.0 - Plugin aktivdir");
        shell.PluginsManagerSelectedDetailRows.Select(row => row.Value).ShouldBe([
            "Sample plugin",
            "com.dobby.sample",
            "1.0.0",
            "Plugin aktivdir",
            "Etibarlı paket",
            "Tam lisenziya girişi",
            "İcra xətası yoxdur"
        ]);
    }

    [Fact]
    public void Plugins_manager_surface_handles_empty_busy_and_pending_uninstall_states()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());

        shell.SetPluginsManagerSurface(
            canView: true,
            canImport: true,
            canConfigure: true,
            canDelete: true,
            [],
            feedbackKey: HostResourceKeys.PluginsManagerInstallBusy,
            isBusy: true);

        shell.PluginsManagerListVisibility.ShouldBe(Visibility.Collapsed);
        shell.PluginsManagerEmptyVisibility.ShouldBe(Visibility.Visible);
        shell.PluginsManagerInstallIsEnabled.ShouldBeFalse();
        shell.PluginsManagerFeedbackText.ShouldBe("Plugin quraşdırılır");

        shell.SetPluginsManagerSurface(
            canView: true,
            canImport: true,
            canConfigure: true,
            canDelete: true,
            [
                new ShellPluginManagerItem(
                    "com.dobby.sample",
                    "Sample plugin",
                    "1.0.0",
                    "Verification plugin",
                    IsEnabled: false,
                    "Pending uninstall",
                    IsPendingUninstall: true,
                    RestartRequired: true)
            ]);
        shell.SelectPlugin("com.dobby.sample");

        shell.PluginsManagerEnableIsEnabled.ShouldBeFalse();
        shell.PluginsManagerDisableIsEnabled.ShouldBeFalse();
        shell.PluginsManagerUninstallIsEnabled.ShouldBeFalse();
        shell.PluginsManagerSelectedDetailText.ShouldBe("Sample plugin 1.0.0 - Silinmə gözlənilir - Yenidən başlatma tələb olunur");
    }

    [Fact]
    public void Plugins_manager_busy_feedback_uses_supplied_lifecycle_key_and_clears_it()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());

        shell.SetPluginsManagerBusy(true, HostResourceKeys.PluginsManagerEnableBusy);

        shell.PluginsManagerFeedbackText.ShouldBe("Plugin aktiv edilir");

        shell.SetPluginsManagerBusy(false, HostResourceKeys.PluginsManagerEnableBusy);

        shell.PluginsManagerFeedbackText.ShouldBeEmpty();
    }

    [Fact]
    public void Plugins_manager_surface_collapses_without_view_or_import_permissions()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());

        shell.SetPluginsManagerSurface(
            canView: false,
            canImport: false,
            canConfigure: false,
            canDelete: false,
            []);

        shell.PluginsManagerPanelVisibility.ShouldBe(Visibility.Collapsed);
        shell.PluginsManagerInstallIsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Plugins_manager_structured_detail_includes_safe_rejection_and_restart_state()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());

        shell.SetPluginsManagerSurface(
            canView: true,
            canImport: true,
            canConfigure: true,
            canDelete: true,
            [
                new ShellPluginManagerItem(
                    "com.dobby.rejected",
                    "Rejected plugin",
                    "2.0.0",
                    "Rejected plugin entry",
                    IsEnabled: false,
                    "Rejected",
                    IsPendingUninstall: false,
                    RestartRequired: true,
                    TrustSummary: "Trust rejected",
                    LicenseSummary: "Read-only license mode",
                    FaultSummary: "No runtime fault",
                    SafeRejectionText: "Package failed trust verification")
            ]);

        shell.ActivateRoute(Dobby.Navigation.RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();
        shell.SelectPlugin("com.dobby.rejected");

        shell.PluginsManagerSelectedDetailRows.Select(row => row.Value).ShouldContain("Yalnız oxuma lisenziya rejimi");
        shell.PluginsManagerSelectedDetailRows.Select(row => row.Value).ShouldContain("Plugin paketi rədd edildi");
        shell.PluginsManagerSelectedDetailRows.Select(row => row.Value).ShouldContain("Yenidən başlatma tələb olunur");
    }

    [Fact]
    public void Plugins_manager_disables_lifecycle_actions_when_the_visible_license_mode_is_read_only()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        shell.SetPluginsManagerSurface(
            canView: true,
            canImport: true,
            canConfigure: true,
            canDelete: true,
            [
                new ShellPluginManagerItem(
                    "com.dobby.sample",
                    "Sample plugin",
                    "1.0.0",
                    "Verification plugin",
                    IsEnabled: true,
                    "Enabled",
                    IsPendingUninstall: false,
                    RestartRequired: false,
                    TrustSummary: "Trusted",
                    LicenseSummary: "Read-only license mode")
            ]);
        shell.SetLicenseSurface(
            canViewStatus: true,
            canImport: false,
            Dobby.Contracts.Licensing.PluginRuntimeMode.ReadOnlyExpired);
        shell.ActivateRoute(Dobby.Navigation.RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();
        shell.SelectPlugin("com.dobby.sample");

        shell.PluginsManagerInstallIsEnabled.ShouldBeFalse();
        shell.PluginsManagerEnableIsEnabled.ShouldBeFalse();
        shell.PluginsManagerDisableIsEnabled.ShouldBeFalse();
        shell.PluginsManagerUninstallIsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Plugins_manager_replaces_raw_rejection_details_with_localized_safe_summaries()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        const string rawValidationDetail = "signature failed at C:/private/plugin-key.pem";
        const string rawFaultDetail = "loader exception at C:/private/plugin.dll";

        shell.SetPluginsManagerSurface(
            canView: true,
            canImport: true,
            canConfigure: true,
            canDelete: true,
            [
                new ShellPluginManagerItem(
                    "com.dobby.rejected",
                    "Rejected plugin",
                    "2.0.0",
                    "Rejected plugin entry",
                    IsEnabled: false,
                    $"Rejected: {rawValidationDetail}",
                    IsPendingUninstall: false,
                    RestartRequired: false,
                    TrustSummary: rawValidationDetail,
                    LicenseSummary: "Read-only license mode",
                    FaultSummary: rawFaultDetail,
                    SafeRejectionText: rawValidationDetail)
            ]);
        shell.ActivateRoute(Dobby.Navigation.RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();
        shell.SelectPlugin("com.dobby.rejected");

        shell.PluginsManagerSelectedDetailRows.Select(row => row.Value).ShouldNotContain(rawValidationDetail);
        shell.PluginsManagerSelectedDetailRows.Select(row => row.Value).ShouldNotContain(rawFaultDetail);
        shell.PluginsManagerSelectedDetailRows.Select(row => row.Value)
            .ShouldContain("Plugin rədd edildi");
        shell.PluginsManagerSelectedDetailRows.Select(row => row.Value)
            .ShouldContain("Plugin paketi rədd edildi");
    }
}
