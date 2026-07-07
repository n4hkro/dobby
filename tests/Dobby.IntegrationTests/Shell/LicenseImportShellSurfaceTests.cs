using System.Windows;
using Dobby.Contracts.Licensing;
using Dobby.Localization;
using Dobby.Shell;
using Shouldly;

namespace Dobby.IntegrationTests.Shell;

public sealed class LicenseImportShellSurfaceTests
{
    [Fact]
    public void Plugins_license_surface_separates_status_view_from_import_action()
    {
        var strings = HostStringCatalog.CreateResourceBacked();
        var shell = ShellViewModel.CreateDefault(strings);

        shell.SetLicenseSurface(
            canViewStatus: true,
            canImport: false,
            PluginRuntimeMode.ReadOnlyExpired,
            feedbackKey: HostResourceKeys.LicenseImportRejected,
            pluginId: "plugin.sample",
            pluginName: "Sample plugin");

        shell.LicensePanelVisibility.ShouldBe(Visibility.Collapsed);
        shell.ActivateRoute(Dobby.Navigation.RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();

        shell.LicensePanelVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseStatusVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseListVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseDetailVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseImportVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseRuntimeModeText.ShouldBe(strings.GetString(HostResourceKeys.LicenseModeReadOnlyExpired));
        shell.LicenseListText.ShouldBe("Sample plugin (plugin.sample)");
        shell.LicenseDetailText.ShouldNotContain("default");
        shell.LicenseDetailText.ShouldNotContain("revision");
        shell.LicenseFeedbackText.ShouldBe(strings.GetString(HostResourceKeys.LicenseImportRejected));

        shell.SetLicenseSurface(
            canViewStatus: false,
            canImport: true,
            PluginRuntimeMode.Denied);

        shell.LicensePanelVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseStatusVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseListVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseDetailVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseImportVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseImportIsEnabled.ShouldBeTrue();
        shell.LicenseImportLabel.ShouldBe(strings.GetString(HostResourceKeys.LicenseImportAction));
    }

    [Fact]
    public void Host_shell_does_not_render_removed_license_import_action_on_plugins_surface_and_keeps_about_clean()
    {
        var root = RepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));
        var windowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var aboutModel = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Settings", "AboutSettingsModel.cs"));

        xaml.ShouldNotContain("x:Name=\"LicensePanel\"");
        xaml.ShouldNotContain("x:Name=\"LicenseImportButton\"");
        xaml.ShouldNotContain("Click=\"LicenseImport_Click\"");
        xaml.ShouldNotContain("Visibility=\"{Binding LicenseImportVisibility}\"");
        xaml.ShouldNotContain("IsEnabled=\"{Binding LicenseImportIsEnabled}\"");
        windowCode.ShouldNotContain("licenseImportCoordinator.ImportFileAsync");
        windowCode.ShouldNotContain("licenseFilePicker.PickLicenseFile");
        aboutModel.ShouldContain("HasLicenseImportAction => false");
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
