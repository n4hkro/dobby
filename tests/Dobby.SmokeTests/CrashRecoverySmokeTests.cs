using System.IO;
using System.Diagnostics;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Shouldly;

namespace Dobby.SmokeTests;

public sealed class CrashRecoverySmokeTests
{
    [Fact]
    public void Transactional_write_recovers_without_partial_state_after_interrupted_operation()
    {
        var root = Path.Combine(Path.GetTempPath(), "dobby-crash-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();

            var scope = new HostPersistenceTransactionScope(factory);
            Should.Throw<InvalidOperationException>(() =>
                scope.ExecuteInTransaction(executor =>
                {
                    executor.Execute(
                        "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('smoke.partial', 'value', 'string', 'None', 'smoke', @UpdatedAtUtc);",
                        new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
                    throw new InvalidOperationException("simulate interrupted process before commit");
                }));

            var reopenedFactory = new EncryptedSqliteConnectionFactory(options, ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root));
            var executor = new HostPersistenceCommandExecutor(reopenedFactory);
            executor.QueryScalar<int>("SELECT COUNT(*) FROM HostConfiguration WHERE ConfigKey = 'smoke.partial';")
                .ShouldBe(0);
            executor.QueryScalar<string>("PRAGMA integrity_check;").ShouldBe("ok");
        }
        finally
        {
            DeleteDirectoryAfterHandlesClose(root);
        }
    }

    [Fact]
    public void Host_process_restart_recovers_without_partial_state_after_process_kill()
    {
        var root = Path.Combine(Path.GetTempPath(), "dobby-crash-process-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var interrupted = RunHostCrashRecoveryHook(root, "interrupt-before-commit");
            interrupted.ExitCode.ShouldNotBe(0);

            var verify = RunHostCrashRecoveryHook(root, "verify-after-interrupt");
            verify.ExitCode.ShouldBe(0, verify.Output);

            var committed = RunHostCrashRecoveryHook(root, "commit-after-restart");
            committed.ExitCode.ShouldBe(0, committed.Output);

            var verifyCommit = RunHostCrashRecoveryHook(root, "verify-committed-after-restart");
            verifyCommit.ExitCode.ShouldBe(0, verifyCommit.Output);
        }
        finally
        {
            DeleteDirectoryAfterHandlesClose(root);
        }
    }

    private static ProcessResult RunHostCrashRecoveryHook(string root, string phase)
    {
        var hostExe = Path.Combine(RepoRoot().FullName, "src", "Dobby.Host", "bin", "Debug", "net10.0-windows", "Dobby.Host.exe");
        File.Exists(hostExe).ShouldBeTrue($"Smoke requires a built host artifact at {hostExe}.");

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(hostExe)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        process.StartInfo.Environment["DOBBY_CRASH_RECOVERY_SMOKE_ROOT"] = root;
        process.StartInfo.Environment["DOBBY_CRASH_RECOVERY_SMOKE_PHASE"] = phase;

        process.Start().ShouldBeTrue();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException($"Host crash-recovery hook timed out during phase {phase}.");
        }

        process.WaitForExit();

        return new ProcessResult(
            process.ExitCode,
            process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
    }

    private static void DeleteDirectoryAfterHandlesClose(string root)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (Directory.Exists(root))
        {
            try
            {
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                Thread.Yield();
            }
            catch (UnauthorizedAccessException) when (DateTimeOffset.UtcNow < deadline)
            {
                Thread.Yield();
            }
        }
    }

    private sealed record ProcessResult(int ExitCode, string Output);

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
