using Dobby.Host;
using Shouldly;

namespace Dobby.IntegrationTests.Runtime;

public sealed class RuntimeFolderLayoutTests
{
    [Fact]
    public void Runtime_layout_matches_storage_spec_required_tree()
    {
        RuntimeFolderLayout.RequiredFolderNames.ShouldBe(
        [
            "data",
            "config",
            "logs",
            "plugins",
            "licenses",
            "recovery",
            "backup",
            "temp"
        ]);
    }

    [Fact]
    public void Logical_roles_stay_under_canonical_roots()
    {
        var options = new Dobby.Configuration.RuntimeOptions
        {
            DataRoot = Path.Combine(Path.GetTempPath(), "dobby-layout-" + Guid.NewGuid().ToString("N"))
        };

        RuntimeFolderLayout.PluginInstallStaging(options)
            .ShouldBe(Path.Combine(options.DataRoot, "plugins", "staging"));
        RuntimeFolderLayout.DiagnosticsLogs(options)
            .ShouldBe(Path.Combine(options.DataRoot, "logs"));
        RuntimeFolderLayout.TemporaryFiles(options)
            .ShouldBe(Path.Combine(options.DataRoot, "temp"));
        RuntimeFolderLayout.RequiredFolderNames.ShouldNotContain("staging");
        RuntimeFolderLayout.RequiredFolderNames.ShouldNotContain("diagnostics");
    }
}
