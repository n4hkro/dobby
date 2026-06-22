using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Dobby.Admin.Commands;
using Dobby.Localization;
using Dobby.Shell;
using Shouldly;

namespace Dobby.SmokeTests;

public sealed class ShellSmokeTests
{
    private const string DevQaSigningKeyPathVariable = "DOBBY_ADMIN_SIGNING_DEV_QA_KEY_PATH";

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
        var result = RunAdminProcess(adminExe, ["--cli", "create-recovery-response", "--unapproved-option"]);

        result.ExitCode.ShouldBe(64, result.Output + result.Error);
        result.Error.ShouldContain("option contract is not approved");
        result.Error.ShouldNotContain(" at ");
    }

    [Fact]
    public void Admin_cli_process_returns_validation_conflict_without_secret_paths_or_stack_trace()
    {
        var adminExe = FindBuiltAdminExe();
        using var fixture = TempFixture.Create();
        var outputPath = fixture.Path("signed.dobbyplugin");
        File.WriteAllText(outputPath, "already exists", new UTF8Encoding(false));

        var result = RunAdminProcess(adminExe, [
            "--cli",
            "sign-plugin-package",
            "--input-path",
            fixture.Root,
            "--signing-profile",
            "dev-qa",
            "--output-path",
            outputPath
        ]);

        result.ExitCode.ShouldBe(65, result.Output + result.Error);
        result.Error.ShouldContain("output-conflict");
        result.Error.ShouldNotContain(fixture.Root);
        result.Error.ShouldNotContain(" at ");
    }

    [Fact]
    public void Admin_cli_process_returns_unavailable_signing_material_without_secret_paths_or_stack_trace()
    {
        var adminExe = FindBuiltAdminExe();
        using var fixture = TempFixture.Create();
        var requestPath = fixture.Path("request.dobbyrecoveryrequest");
        var outputPath = fixture.Path("response.dobbyrecovery");
        File.WriteAllText(requestPath, RecoveryRequestJson(), new UTF8Encoding(false));

        var result = RunAdminProcess(
            adminExe,
            [
                "--cli",
                "create-recovery-response",
                "--request-path",
                requestPath,
                "--signing-profile",
                "dev-qa",
                "--output-path",
                outputPath
            ],
            startInfo => startInfo.Environment.Remove(DevQaSigningKeyPathVariable));

        result.ExitCode.ShouldBe(66, result.Output + result.Error);
        result.Error.ShouldContain("signing-material-unavailable");
        result.Error.ShouldNotContain(fixture.Root);
        result.Error.ShouldNotContain(" at ");
    }

    [Fact]
    public void Admin_cli_process_creates_recovery_response_with_environment_signing_material()
    {
        var adminExe = FindBuiltAdminExe();
        using var fixture = TempFixture.Create();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyPath = fixture.Path("dev-qa-private-key.pem");
        var requestPath = fixture.Path("request.dobbyrecoveryrequest");
        var outputPath = fixture.Path("response.dobbyrecovery");
        File.WriteAllText(keyPath, key.ExportPkcs8PrivateKeyPem(), new UTF8Encoding(false));
        File.WriteAllText(requestPath, RecoveryRequestJson(), new UTF8Encoding(false));

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(adminExe)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        process.StartInfo.Environment[DevQaSigningKeyPathVariable] = keyPath;
        process.StartInfo.ArgumentList.Add("--cli");
        process.StartInfo.ArgumentList.Add("create-recovery-response");
        process.StartInfo.ArgumentList.Add("--request-path");
        process.StartInfo.ArgumentList.Add(requestPath);
        process.StartInfo.ArgumentList.Add("--signing-profile");
        process.StartInfo.ArgumentList.Add("dev-qa");
        process.StartInfo.ArgumentList.Add("--output-path");
        process.StartInfo.ArgumentList.Add(outputPath);

        process.Start().ShouldBeTrue();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Admin CLI smoke timed out.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.ExitCode.ShouldBe(0, output + error);
        File.Exists(outputPath).ShouldBeTrue();
        output.ShouldContain("create-recovery-response");
        output.ShouldNotContain(fixture.Root);
        error.ShouldBeEmpty();
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

    private static AdminProcessResult RunAdminProcess(
        string adminExe,
        IReadOnlyList<string> arguments,
        Action<ProcessStartInfo>? configure = null)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(adminExe)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        configure?.Invoke(process.StartInfo);
        process.Start().ShouldBeTrue();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Admin CLI smoke timed out.");
        }

        return new AdminProcessResult(
            process.ExitCode,
            process.StandardOutput.ReadToEnd(),
            process.StandardError.ReadToEnd());
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

    private static string RecoveryRequestJson()
    {
        return """
            {
              "type": "dobby.owner-recovery-request.v1",
              "dobbyVersion": "1.0.0",
              "requestedAt": "2026-06-20T09:00:00Z",
              "deviceIdentity": {
                "machineGuid": "machine",
                "cpuId": "cpu",
                "motherboardSerial": "board",
                "diskSerial": "disk",
                "macAddress": "mac"
              },
              "deviceDisplayName": "Owner laptop",
              "requestId": "request-1"
            }
            """;
    }

    private sealed record AdminProcessResult(
        int ExitCode,
        string Output,
        string Error);

    private sealed class TempFixture : IDisposable
    {
        private TempFixture(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TempFixture Create()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dobby-admin-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempFixture(root);
        }

        public string Path(string fileName)
        {
            return System.IO.Path.Combine(Root, fileName);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
