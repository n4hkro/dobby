using System.Windows;
using Dobby.Contracts.Licensing;
using Dobby.Localization;
using Dobby.Shell;
using Shouldly;

namespace Dobby.UnitTests.Shell;

public sealed class ShellViewModelLicenseSurfaceTests
{
    [Fact]
    public void License_surface_shows_status_and_import_independently_by_permission()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());

        shell.SetLicenseSurface(
            canViewStatus: true,
            canImport: false,
            PluginRuntimeMode.ReadOnlyExpired,
            feedbackKey: HostResourceKeys.LicenseImportRejected);

        shell.LicensePanelVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseStatusVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseImportVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseRuntimeModeText.ShouldBe("Yalnız oxuma");
        shell.LicenseFeedbackText.ShouldBe("Lisenziya idxalı qəbul edilmədi");
    }

    [Fact]
    public void License_surface_exposes_safe_list_detail_and_busy_import_state()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());

        shell.SetLicenseSurface(
            canViewStatus: true,
            canImport: true,
            PluginRuntimeMode.FullAccess,
            pluginId: "plugin.sample",
            pluginName: "Sample plugin");

        shell.LicenseListVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseDetailVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseImportIsEnabled.ShouldBeTrue();
        shell.LicenseListText.ShouldBe("Sample plugin (plugin.sample)");
        shell.LicenseDetailText.ShouldBe("Tam giriş");
        shell.LicenseDetailText.ShouldNotContain("2026");
        shell.LicenseDetailText.ShouldNotContain("default");
        shell.LicenseDetailText.ShouldNotContain("revision");
        shell.LicenseDetailText.ShouldNotContain("deployment");

        shell.SetLicenseImportBusy(true);

        shell.LicenseImportIsEnabled.ShouldBeFalse();
        shell.LicenseFeedbackText.ShouldBe("Lisenziya idxal olunur");
    }

    [Fact]
    public void License_surface_collapses_when_user_has_no_license_permissions()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());

        shell.SetLicenseSurface(
            canViewStatus: false,
            canImport: false,
            PluginRuntimeMode.Denied);

        shell.LicensePanelVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseStatusVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseListVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseDetailVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseImportVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseImportIsEnabled.ShouldBeFalse();
    }
}
