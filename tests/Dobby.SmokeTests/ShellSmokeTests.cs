using System.Diagnostics;
using System.IO;
using Dobby.Admin.Commands;
using Dobby.Localization;
using Dobby.Shell;
using Shouldly;

namespace Dobby.SmokeTests;

public sealed class ShellSmokeTests
{
    [Fact]
    public void Shell_view_model_exposes_home_plugins_settings_in_stable_order()
    {
        var shell = new ShellViewModel(new HostStringCatalog());

        shell.TopNavigation.Select(x => x.Label).ShouldBe(["Ana səhifə", "Pluginlər", "Tənzimləmələr"]);
    }

    [Fact]
    public void Admin_command_host_rejects_unapproved_option_surfaces()
    {
        var result = AdminCommandHost.Run(["--cli", "create-recovery-response", "--unapproved-option"]);

        result.ExitCode.ShouldBe(64);
        result.StandardError.ShouldContain("option contract is not approved");
    }

    [Fact]
    public void Admin_cli_process_rejects_unapproved_option_without_stack_trace()
    {
        var adminExe = FindBuiltAdminExe();
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(adminExe)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("--cli");
        process.StartInfo.ArgumentList.Add("create-recovery-response");
        process.StartInfo.ArgumentList.Add("--unapproved-option");

        process.Start().ShouldBeTrue();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Admin CLI smoke timed out.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.ExitCode.ShouldBe(64, output + error);
        error.ShouldContain("option contract is not approved");
        error.ShouldNotContain(" at ");
    }

    private static string FindBuiltAdminExe()
    {
        var root = RepoRoot().FullName;
        var candidates = new[]
        {
            Path.Combine(root, "src", "Dobby.Admin", "bin", "Release", "net10.0-windows", "Dobby.Admin.exe"),
            Path.Combine(root, "src", "Dobby.Admin", "bin", "Debug", "net10.0-windows", "Dobby.Admin.exe")
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Smoke requires a built Dobby.Admin.exe artifact.");
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
