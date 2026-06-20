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
                    RestartRequired: false)
            ]);
        shell.SelectPlugin("com.dobby.sample");

        shell.PluginsManagerPanelVisibility.ShouldBe(Visibility.Visible);
        shell.PluginsManagerListVisibility.ShouldBe(Visibility.Visible);
        shell.PluginsManagerEmptyVisibility.ShouldBe(Visibility.Collapsed);
        shell.PluginsManagerInstallIsEnabled.ShouldBeTrue();
        shell.PluginsManagerDisableIsEnabled.ShouldBeTrue();
        shell.PluginsManagerEnableIsEnabled.ShouldBeFalse();
        shell.PluginsManagerUninstallIsEnabled.ShouldBeFalse();
        shell.PluginsManagerSelectedDetailText.ShouldBe("Sample plugin 1.0.0 - Enabled");
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
        shell.PluginsManagerSelectedDetailText.ShouldBe("Sample plugin 1.0.0 - Pending uninstall - Yenidən başlatma tələb olunur");
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
}
