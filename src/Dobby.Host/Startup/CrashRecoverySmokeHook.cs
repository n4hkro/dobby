using System.Diagnostics;
using System.IO;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;

namespace Dobby.Host.Startup;

internal static class CrashRecoverySmokeHook
{
    private const string RootVariable = "DOBBY_CRASH_RECOVERY_SMOKE_ROOT";
    private const string PhaseVariable = "DOBBY_CRASH_RECOVERY_SMOKE_PHASE";

    public static bool TryRunFromEnvironment(out int exitCode)
    {
        var root = Environment.GetEnvironmentVariable(RootVariable);
        var phase = Environment.GetEnvironmentVariable(PhaseVariable);
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(phase))
        {
            exitCode = 0;
            return false;
        }

        try
        {
            exitCode = Run(root, phase);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            Console.Error.WriteLine(ex.Message);
            exitCode = 1;
        }

        return true;
    }

    private static int Run(string root, string phase)
    {
        var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
        var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
        if (!probe.IsAvailable())
        {
            Console.Error.WriteLine("Protected material is unavailable.");
            return 2;
        }

        var factory = new EncryptedSqliteConnectionFactory(options, probe);
        var migration = new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations();
        if (!migration.IsSuccess)
        {
            Console.Error.WriteLine(migration.Error.Message);
            return 3;
        }

        var executor = new HostPersistenceCommandExecutor(factory);
        return phase switch
        {
            "interrupt-before-commit" => InterruptBeforeCommit(factory),
            "verify-after-interrupt" => VerifyAfterInterrupt(executor),
            "commit-after-restart" => CommitAfterRestart(factory),
            "verify-committed-after-restart" => VerifyCommittedAfterRestart(executor),
            _ => UnknownPhase(phase)
        };
    }

    private static int InterruptBeforeCommit(IHostPersistenceConnectionFactory factory)
    {
        new HostPersistenceTransactionScope(factory).ExecuteInTransaction(executor =>
        {
            executor.Execute(
                "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('smoke.process.partial', 'value', 'string', 'None', 'smoke', @UpdatedAtUtc);",
                new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
            Process.GetCurrentProcess().Kill(entireProcessTree: true);
            Thread.Sleep(Timeout.Infinite);
        });

        return 4;
    }

    private static int VerifyAfterInterrupt(IHostPersistenceCommandExecutor executor)
    {
        var partialRows = executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostConfiguration WHERE ConfigKey = 'smoke.process.partial';");
        var integrity = executor.QueryScalar<string>("PRAGMA integrity_check;");
        if (partialRows != 0 || integrity != "ok")
        {
            Console.Error.WriteLine($"Partial rows: {partialRows}; integrity: {integrity}.");
            return 5;
        }

        return 0;
    }

    private static int CommitAfterRestart(IHostPersistenceConnectionFactory factory)
    {
        new HostPersistenceTransactionScope(factory).ExecuteInTransaction(executor =>
        {
            executor.Execute(
                "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('smoke.process.committed', 'value', 'string', 'None', 'smoke', @UpdatedAtUtc);",
                new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
        });

        return 0;
    }

    private static int VerifyCommittedAfterRestart(IHostPersistenceCommandExecutor executor)
    {
        var committedRows = executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostConfiguration WHERE ConfigKey = 'smoke.process.committed';");
        var integrity = executor.QueryScalar<string>("PRAGMA integrity_check;");
        if (committedRows != 1 || integrity != "ok")
        {
            Console.Error.WriteLine($"Committed rows: {committedRows}; integrity: {integrity}.");
            return 6;
        }

        return 0;
    }

    private static int UnknownPhase(string phase)
    {
        Console.Error.WriteLine($"Unknown crash-recovery smoke phase: {phase}.");
        return 7;
    }
}
