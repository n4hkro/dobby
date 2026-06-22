using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Dobby.Localization;
using Shouldly;

namespace Dobby.UnitTests.Localization;

public sealed class ShellLocalizationCoverageTests
{
    [Fact]
    public void Sprint7_visible_host_keys_have_az_az_resx_entries()
    {
        var resourceKeys = ResxKeys();

        HostResourceKeys.Sprint7VisibleKeys.ShouldAllBe(key => resourceKeys.Contains(key));
    }

    [Fact]
    public void Catalog_uses_dobby_owned_localizer_adapter_before_fallback_dictionary()
    {
        var adapter = new DictionaryHostStringLocalizerAdapter(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HostResourceKeys.ShellDiagnosticsExport] = "Adapter dəyəri"
        });
        var missing = new List<string>();
        var catalog = new HostStringCatalog(adapter, missing.Add);

        catalog.GetString(HostResourceKeys.ShellDiagnosticsExport).ShouldBe("Adapter dəyəri");
        catalog.GetString("Dobby.Host.Missing.Key", CultureInfo.GetCultureInfo("az-AZ"))
            .ShouldBe("[[Dobby.Host.Missing.Key]]");
        missing.ShouldBe(["Dobby.Host.Missing.Key"]);
    }

    [Fact]
    public void Resource_backed_catalog_uses_host_resx_as_runtime_authority()
    {
        var missing = new List<string>();
        var catalog = HostStringCatalog.CreateResourceBacked(missing.Add);

        catalog.GetString(HostResourceKeys.ShellDiagnosticsExport, CultureInfo.GetCultureInfo("az-AZ"))
            .ShouldBe("İxrac et");
        catalog.GetString("Dobby.Host.Missing.Resource", CultureInfo.GetCultureInfo("az-AZ"))
            .ShouldBe("[[Dobby.Host.Missing.Resource]]");
        missing.ShouldBe(["Dobby.Host.Missing.Resource"]);
    }

    [Fact]
    public void Work_session_warning_keys_are_registered_and_have_az_az_text()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HostResourceKeys.ShellWorkSessionNavigationWarning] = "Açıq iş sessiyası var",
            [HostResourceKeys.ShellWorkSessionStay] = "Qal",
            [HostResourceKeys.ShellWorkSessionMinimize] = "Yığ",
            [HostResourceKeys.ShellWorkSessionClose] = "Bağla"
        };
        var resourceKeys = ResxKeys();
        var fallbackCatalog = new HostStringCatalog();
        var resourceCatalog = HostStringCatalog.CreateResourceBacked();
        var azAz = CultureInfo.GetCultureInfo("az-AZ");

        foreach (var (key, value) in expected)
        {
            HostResourceKeys.Sprint7VisibleKeys.ShouldContain(key);
            resourceKeys.ShouldContain(key);
            fallbackCatalog.GetString(key, azAz).ShouldBe(value);
            resourceCatalog.GetString(key, azAz).ShouldBe(value);
        }
    }

    [Fact]
    public void Plugins_manager_keys_are_registered_and_have_az_az_text()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HostResourceKeys.PluginsManagerHeading] = "Pluginlər",
            [HostResourceKeys.PluginsManagerInstall] = "Plugin quraşdır",
            [HostResourceKeys.PluginsManagerEnable] = "Aktiv et",
            [HostResourceKeys.PluginsManagerDisable] = "Deaktiv et",
            [HostResourceKeys.PluginsManagerUninstall] = "Sil",
            [HostResourceKeys.PluginsManagerEmpty] = "Quraşdırılmış plugin yoxdur",
            [HostResourceKeys.PluginsManagerRestartRequired] = "Yenidən başlatma tələb olunur",
            [HostResourceKeys.PluginsManagerInstallBusy] = "Plugin quraşdırılır",
            [HostResourceKeys.PluginsManagerOperationDenied] = "Bu plugin əməliyyatı üçün icazə yoxdur",
            [HostResourceKeys.PluginsManagerUninstallConfirmationTitle] = "Plugini sil",
            [HostResourceKeys.PluginsManagerUninstallConfirmationMessage] = "{0} pluginini silmək istəyirsiniz? Bəli plugin datasını silir, Xeyr plugin datasını saxlayır, Ləğv et əməliyyatı dayandırır."
        };
        var resourceKeys = ResxKeys();
        var fallbackCatalog = new HostStringCatalog();
        var resourceCatalog = HostStringCatalog.CreateResourceBacked();
        var azAz = CultureInfo.GetCultureInfo("az-AZ");

        foreach (var (key, value) in expected)
        {
            HostResourceKeys.Sprint7VisibleKeys.ShouldContain(key);
            resourceKeys.ShouldContain(key);
            fallbackCatalog.GetString(key, azAz).ShouldBe(value);
            resourceCatalog.GetString(key, azAz).ShouldBe(value);
        }
    }

    private static HashSet<string> ResxKeys()
    {
        var path = Path.Combine(RepoRoot().FullName, "src", "Dobby.Localization", "Resources", "Host.az-AZ.resx");
        return XDocument.Load(path)
            .Descendants("data")
            .Select(element => element.Attribute("name")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal)!;
    }

    private sealed class DictionaryHostStringLocalizerAdapter(IReadOnlyDictionary<string, string> values)
        : IHostStringLocalizerAdapter
    {
        public LocalizedHostString GetString(string key, CultureInfo culture)
        {
            return values.TryGetValue(key, out var value)
                ? new LocalizedHostString(value, ResourceNotFound: false)
                : new LocalizedHostString(string.Empty, ResourceNotFound: true);
        }
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
