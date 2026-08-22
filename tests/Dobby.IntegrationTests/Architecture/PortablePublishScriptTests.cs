using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class PortablePublishScriptTests
{
    [Fact]
    public void Portable_publish_script_creates_unzipped_self_contained_host_folder()
    {
        var root = RepositoryRoot();
        var script = Path.Combine(root, "tools", "publish-portable", "publish-portable.ps1");

        File.Exists(script).ShouldBeTrue($"Expected portable publish script at {script}.");

        var text = File.ReadAllText(script);
        text.ShouldContain("artifacts");
        text.ShouldContain("publish");
        text.ShouldContain("portable");
        text.ShouldContain("Dobby.Host");
        text.ShouldContain("dotnet");
        text.ShouldContain("publish");
        text.ShouldContain("-c");
        text.ShouldContain("Release");
        text.ShouldContain("-r");
        text.ShouldContain("win-x64");
        text.ShouldContain("--self-contained");
        text.ShouldContain("Dobby.Host.exe");
        text.ShouldContain("Dobby.Host.dll");
        text.ShouldContain("appsettings.json");
        text.ShouldContain("native/win-x64/sqlite3mc_x64.dll");
        text.ShouldContain("$missingFiles = @(");
        text.ShouldContain("artifact-manifest.json");
        text.ShouldContain("PortableSupport");
        text.ShouldContain("sourceCommit");
        text.ShouldContain("runtimeIdentifier");
        text.ShouldContain("Get-FileHash");
        text.ShouldContain("DobbyPluginTrustProfile=Production");
        text.ShouldContain("%LocalAppData%/Dobby/PortableSupport");
        text.ShouldContain("sourceTreeDirty");
        text.ShouldContain("latest.json");
        text.ShouldContain("Assert-ChildPath");
        text.ShouldNotContain("KeepExistingOutput");
        text.ShouldNotContain("Compress-Archive", Case.Insensitive);
        text.ShouldNotContain("ZipFile", Case.Insensitive);
        text.ShouldNotContain(".zip", Case.Insensitive);
    }

    private static string RepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "Dobby.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
