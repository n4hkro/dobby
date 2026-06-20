using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class AdminToolStaticScanTests
{
    [Theory]
    [InlineData("tools/sign-plugin-package/sign-plugin-package.ps1", "sign-plugin-package")]
    [InlineData("tools/create-plugin-license/create-plugin-license.ps1", "create-plugin-license")]
    [InlineData("tools/create-recovery-response/create-recovery-response.ps1", "create-recovery-response")]
    public void Admin_tool_wrappers_invoke_canonical_cli_commands(string wrapperPath, string command)
    {
        var root = RepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, wrapperPath));

        script.ShouldContain("Dobby.Admin");
        script.ShouldContain("--cli " + command);
        script.ShouldContain("exit $LASTEXITCODE");
        script.ShouldNotContain("private-key", Case.Insensitive);
    }

    [Fact]
    public void Admin_app_routes_cli_before_opening_wpf_shell()
    {
        var root = RepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "src/Dobby.Admin/App.xaml.cs"));

        app.ShouldContain("AdminCommandHost.Run(e.Args)");
        app.ShouldContain("Shutdown(result.ExitCode)");
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
