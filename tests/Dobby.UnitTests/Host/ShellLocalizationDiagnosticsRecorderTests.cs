using System.Globalization;
using System.IO;
using Dobby.Diagnostics;
using Dobby.Host;
using Dobby.Localization;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class ShellLocalizationDiagnosticsRecorderTests
{
    [Fact]
    public void Missing_resource_key_is_recorded_as_local_diagnostics_event()
    {
        var events = new List<LocalizationMissingKeyEvent>();
        var recorder = new ShellLocalizationDiagnosticsRecorder(events.Add, "Dobby.Host");
        var catalog = HostStringCatalog.CreateResourceBacked(recorder.RecordMissingKey);
        var culture = CultureInfo.GetCultureInfo("az-AZ");

        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = culture;
            catalog.GetString("Dobby.Shell.Unknown.Key", culture).ShouldBe("[[Dobby.Shell.Unknown.Key]]");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }

        var evt = events.Single();
        evt.EventName.ShouldBe("localization.missing-key");
        evt.Culture.ShouldBe("az-AZ");
        evt.ResourceKey.ShouldBe("Dobby.Shell.Unknown.Key");
        evt.ResourceScope.ShouldBe("Dobby.Host");
        evt.FallbackChainResult.ShouldBe("resource-missing;returned-marker");
    }

    [Fact]
    public void Post_login_shell_composes_resource_catalog_with_localization_diagnostics_recorder()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot().FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"));

        source.ShouldContain("ShellLocalizationDiagnosticsRecorder");
        source.ShouldContain("HostStringCatalog.CreateResourceBacked(localizationDiagnostics.RecordMissingKey)");
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
