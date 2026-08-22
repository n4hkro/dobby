using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.PluginFramework.Packaging;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Dobby.Host.Plugins;

public sealed class PluginInstallStagingArea : IPluginInstallStagingArea
{
    private readonly IHostPersistenceCommandExecutor executor;
    private readonly string stagingRoot;
    private readonly string runtimeRoot;

    public PluginInstallStagingArea(string dataRoot, IHostPersistenceCommandExecutor executor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        this.executor = executor;
        var root = Path.Combine(Environment.ExpandEnvironmentVariables(dataRoot), "plugins");
        stagingRoot = Path.Combine(root, "staging");
        runtimeRoot = Path.Combine(root, "installed");
        Directory.CreateDirectory(stagingRoot);
        Directory.CreateDirectory(runtimeRoot);
    }

    public Result<string> Stage(PluginPackageDescriptor package)
    {
        ArgumentNullException.ThrowIfNull(package);

        string? stagedPath = null;
        string? operationId = null;
        try
        {
            operationId = Guid.CreateVersion7().ToString("D");
            stagedPath = Path.Combine(stagingRoot, operationId);
            var runtimePath = GetRuntimePath(package);
            RecordOperation(operationId, package, stagedPath, runtimePath, InstallState.Staged);
            Directory.CreateDirectory(stagedPath);
            MaterializePackage(stagedPath, package);
            Transition(operationId, InstallState.Staged, InstallState.Verified);
            return Result.Success(stagedPath);
        }
        catch
        {
            TryDeleteUnderRoot(stagingRoot, stagedPath);
            TryCompleteOperation(operationId);
            return Result.Failure<string>(ErrorCode.IoFailed);
        }
    }

    public Result<string> Promote(string stagedPath, PluginPackageDescriptor package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedPath);
        ArgumentNullException.ThrowIfNull(package);

        try
        {
            var row = GetOperationByStagedPath(stagedPath);
            if (row is null ||
                !row.PluginId.Equals(package.Manifest.PluginId, StringComparison.Ordinal) ||
                !row.PackageFingerprint.Equals(package.Fingerprint, StringComparison.Ordinal) ||
                !row.State.Equals(InstallState.Verified, StringComparison.Ordinal))
            {
                return Result.Failure<string>(ErrorCode.ValidationFailed);
            }

            var runtimePath = NormalizeDirectoryPath(row.RuntimePath);
            EnsureUnderRoot(runtimeRoot, runtimePath);
            EnsureUnderRoot(stagingRoot, stagedPath);
            Transition(row.OperationId, InstallState.Verified, InstallState.ReadyToPromote);
            Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);
            if (Directory.Exists(runtimePath))
            {
                throw new IOException("Immutable plugin runtime path already exists.");
            }

            Directory.Move(stagedPath, runtimePath);
            Transition(row.OperationId, InstallState.ReadyToPromote, InstallState.Promoted);
            return Result.Success(runtimePath);
        }
        catch
        {
            return Result.Failure<string>(ErrorCode.IoFailed);
        }
    }

    public Result Cleanup(string stagedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedPath);

        try
        {
            var row = GetOperationByPath(stagedPath);
            if (row is not null)
            {
                TryDeleteUnderRoot(stagingRoot, row.StagedPath);
                if (!row.State.Equals(InstallState.StateCommitted, StringComparison.Ordinal) &&
                    !row.State.Equals(InstallState.Completed, StringComparison.Ordinal) &&
                    !IsRuntimeAuthoritative(row.RuntimePath))
                {
                    TryDeleteUnderRoot(runtimeRoot, row.RuntimePath);
                }

                MarkCompleted(row.OperationId);
                return Result.Success();
            }

            if (IsSameOrUnder(NormalizeDirectoryPath(stagingRoot), NormalizeDirectoryPath(stagedPath)))
            {
                TryDeleteUnderRoot(stagingRoot, stagedPath);
            }
            else
            {
                EnsureUnderRoot(runtimeRoot, stagedPath);
                TryDeleteUnderRoot(runtimeRoot, stagedPath);
            }

            return Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.IoFailed);
        }
    }

    public Result<int> Recover()
    {
        try
        {
            var rows = executor.Query<InstallOperationRow>(
                "SELECT OperationId, PluginId, PackageFingerprint, StagedPath, RuntimePath, State FROM PluginInstallOperation ORDER BY CreatedAtUtc, OperationId;");
            var recovered = 0;
            foreach (var row in rows)
            {
                EnsureUnderRoot(stagingRoot, row.StagedPath);
                EnsureUnderRoot(runtimeRoot, row.RuntimePath);

                if (row.State.Equals(InstallState.Completed, StringComparison.Ordinal))
                {
                    TryDeleteUnderRoot(stagingRoot, row.StagedPath);
                    if (!IsRuntimeAuthoritative(row.RuntimePath))
                    {
                        TryDeleteUnderRoot(runtimeRoot, row.RuntimePath);
                    }

                    continue;
                }

                if (row.State.Equals(InstallState.StateCommitted, StringComparison.Ordinal))
                {
                    if (!IsRuntimeAuthoritative(row.RuntimePath))
                    {
                        return Result.Failure<int>(ErrorCode.DatabaseUnavailable);
                    }

                    TryDeleteUnderRoot(stagingRoot, row.StagedPath);
                    MarkCompleted(row.OperationId);
                    recovered++;
                    continue;
                }

                if (row.State.Equals(InstallState.Promoted, StringComparison.Ordinal) &&
                    IsRuntimeAuthoritative(row.RuntimePath))
                {
                    Transition(row.OperationId, InstallState.Promoted, InstallState.StateCommitted);
                    MarkCompleted(row.OperationId);
                    recovered++;
                    continue;
                }

                TryDeleteUnderRoot(stagingRoot, row.StagedPath);
                if (!IsRuntimeAuthoritative(row.RuntimePath))
                {
                    TryDeleteUnderRoot(runtimeRoot, row.RuntimePath);
                }

                MarkCompleted(row.OperationId);
                recovered++;
            }

            PruneCompletedOperationHistory();
            return Result.Success(recovered);
        }
        catch
        {
            return Result.Failure<int>(ErrorCode.IoFailed);
        }
    }

    public Result<int> CleanupRuntimeOrphans(IReadOnlyCollection<string> authoritativeRuntimePaths)
    {
        ArgumentNullException.ThrowIfNull(authoritativeRuntimePaths);

        try
        {
            Directory.CreateDirectory(runtimeRoot);
            var runtimeRootPath = NormalizeDirectoryPath(runtimeRoot);
            var authoritative = authoritativeRuntimePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizeDirectoryPath)
                .Where(path => IsSameOrUnder(runtimeRootPath, path))
                .ToHashSet(PathComparer);
            foreach (var path in executor.Query<string>(
                         """
                         SELECT version.RuntimePath
                         FROM InstalledPluginVersionPointer pointer
                         JOIN InstalledPluginVersion version
                           ON version.PluginId = pointer.PluginId
                          AND version.PackageFingerprint IN (
                              pointer.ActiveFingerprint,
                              pointer.LastKnownGoodFingerprint,
                              pointer.PendingActivationFingerprint)
                         ORDER BY version.RuntimePath;
                         """))
            {
                var normalized = NormalizeDirectoryPath(path);
                if (IsSameOrUnder(runtimeRootPath, normalized))
                {
                    authoritative.Add(normalized);
                }
            }
            var removed = 0;

            foreach (var pluginDirectory in Directory.EnumerateDirectories(runtimeRoot))
            {
                foreach (var versionDirectory in Directory.EnumerateDirectories(pluginDirectory))
                {
                    foreach (var runtimeDirectory in Directory.EnumerateDirectories(versionDirectory))
                    {
                        var runtimeDirectoryPath = NormalizeDirectoryPath(runtimeDirectory);
                        if (!IsSameOrUnder(runtimeRootPath, runtimeDirectoryPath))
                        {
                            continue;
                        }

                        if (authoritative.Contains(runtimeDirectoryPath))
                        {
                            continue;
                        }

                        EnsurePathHasNoReparsePoints(runtimeDirectoryPath);
                        Directory.Delete(runtimeDirectoryPath, recursive: true);
                        removed++;
                    }

                    DeleteIfEmpty(versionDirectory, runtimeRootPath);
                }

                DeleteIfEmpty(pluginDirectory, runtimeRootPath);
            }

            return Result.Success(removed);
        }
        catch
        {
            return Result.Failure<int>(ErrorCode.IoFailed);
        }
    }

    private void RecordOperation(
        string operationId,
        PluginPackageDescriptor package,
        string stagedPath,
        string runtimePath,
        string state)
    {
        executor.Execute(
            """
            INSERT INTO PluginInstallOperation (
                OperationId, PluginId, Version, PackageFingerprint, DisplayName,
                Description, StagedPath, RuntimePath, State, CreatedAtUtc, UpdatedAtUtc
            ) VALUES (
                @OperationId, @PluginId, @Version, @PackageFingerprint, @DisplayName,
                @Description, @StagedPath, @RuntimePath, @State, @CreatedAtUtc, @UpdatedAtUtc
            );
            """,
            new
            {
                OperationId = operationId,
                PluginId = package.Manifest.PluginId,
                package.Manifest.Version,
                PackageFingerprint = package.Fingerprint,
                package.Manifest.DisplayName,
                package.Manifest.Description,
                StagedPath = stagedPath,
                RuntimePath = runtimePath,
                State = state,
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
    }

    private void Transition(string operationId, string expectedState, string nextState)
    {
        var affected = executor.Execute(
            """
            UPDATE PluginInstallOperation
            SET State = @NextState,
                UpdatedAtUtc = @UpdatedAtUtc
            WHERE OperationId = @OperationId AND State = @ExpectedState;
            """,
            new
            {
                OperationId = operationId,
                ExpectedState = expectedState,
                NextState = nextState,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
        if (affected != 1)
        {
            throw new InvalidOperationException("Plugin install operation state changed unexpectedly.");
        }
    }

    private static void MaterializePackage(string stagedPath, PluginPackageDescriptor package)
    {
        var stagedRoot = NormalizeDirectoryPath(stagedPath);
        EnsurePathHasNoReparsePoints(stagedRoot);
        var paths = package.Entries.Select(entry => entry.Path).ToArray();
        if (PluginPackagePathPolicy.HasCaseOrUnicodeCollision(paths) ||
            PluginPackagePathPolicy.HasFileDirectoryPrefixCollision(paths))
        {
            throw new InvalidDataException("Plugin package contains colliding paths.");
        }

        var manifestEntries = package.Entries
            .Where(entry => entry.Path.Equals(PluginPackageReader.ManifestFileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (manifestEntries.Length > 1 ||
            (manifestEntries.Length == 1 &&
             !manifestEntries[0].Path.Equals(PluginPackageReader.ManifestFileName, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Plugin package manifest path is not canonical.");
        }

        var manifestPath = ResolveDestinationPath(stagedRoot, PluginPackageReader.ManifestFileName);
        using (var manifestStream = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(manifestStream, new UTF8Encoding(false)))
        {
            writer.Write(package.ManifestJson);
        }

        foreach (var entry in package.Entries)
        {
            if (entry.Path.Equals(PluginPackageReader.ManifestFileName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!PluginPackagePathPolicy.TryNormalize(entry.Path, out var normalizedPath) ||
                !normalizedPath.Equals(entry.Path, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Plugin package entry path is not canonical.");
            }

            var fullPath = ResolveDestinationPath(stagedRoot, normalizedPath);
            EnsureSafeParentDirectories(stagedRoot, normalizedPath);
            CopyAndVerifyEntry(entry, fullPath);
        }
    }

    public Result MarkStateCommitted(string runtimePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimePath);

        try
        {
            var row = GetOperationByPath(runtimePath);
            if (row is null)
            {
                return Result.Failure(ErrorCode.NotFound);
            }

            if (row.State.Equals(InstallState.StateCommitted, StringComparison.Ordinal) ||
                row.State.Equals(InstallState.Completed, StringComparison.Ordinal))
            {
                return Result.Success();
            }

            Transition(row.OperationId, InstallState.Promoted, InstallState.StateCommitted);
            return Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    public Result RemoveRuntime(string runtimePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimePath);

        try
        {
            EnsureUnderRoot(runtimeRoot, runtimePath);
            var pluginId = executor.Query<string>(
                "SELECT PluginId FROM InstalledPluginState WHERE RuntimePath = @RuntimePath;",
                new { RuntimePath = NormalizeDirectoryPath(runtimePath) }).SingleOrDefault();
            IReadOnlyList<string> paths = string.IsNullOrWhiteSpace(pluginId)
                ? [NormalizeDirectoryPath(runtimePath)]
                : executor.Query<string>(
                    "SELECT RuntimePath FROM InstalledPluginVersion WHERE PluginId = @PluginId ORDER BY RuntimePath;",
                    new { PluginId = pluginId });
            foreach (var path in paths.Distinct(PathComparer))
            {
                TryDeleteUnderRoot(runtimeRoot, path);
            }

            return Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.IoFailed);
        }
    }

    public Result Complete(string runtimePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimePath);

        try
        {
            var row = GetOperationByPath(runtimePath);
            if (row is null)
            {
                return Result.Failure(ErrorCode.NotFound);
            }

            if (!row.State.Equals(InstallState.Completed, StringComparison.Ordinal))
            {
                Transition(row.OperationId, InstallState.StateCommitted, InstallState.Completed);
            }

            TryDeleteUnderRoot(stagingRoot, row.StagedPath);
            PruneCompletedOperationHistory();
            return Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    private static void CopyAndVerifyEntry(PluginPackageEntry entry, string fullPath)
    {
        using var source = entry.OpenRead();
        using var destination = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        long written = 0;
        try
        {
            while (true)
            {
                var read = source.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                written = checked(written + read);
                if (written > entry.UncompressedLength)
                {
                    throw new InvalidDataException("Plugin entry exceeds its verified length.");
                }

                hash.AppendData(buffer, 0, read);
                destination.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (written != entry.UncompressedLength ||
            !actualHash.Equals(entry.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Plugin entry changed after package validation.");
        }
    }

    private static string ResolveDestinationPath(string stagedRoot, string normalizedPath)
    {
        var platformPath = normalizedPath.Replace('/', Path.DirectorySeparatorChar);
        var destination = Path.GetFullPath(Path.Combine(stagedRoot, platformPath));
        if (!IsSameOrUnder(stagedRoot, destination) || destination.Equals(stagedRoot, PathComparison))
        {
            throw new InvalidDataException("Plugin package entry escapes the staging root.");
        }

        return destination;
    }

    private static void EnsureSafeParentDirectories(string stagedRoot, string normalizedPath)
    {
        var segments = normalizedPath.Split('/');
        var current = stagedRoot;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            current = Path.Combine(current, segments[index]);
            Directory.CreateDirectory(current);
            EnsureDirectoryIsNotReparsePoint(current);
        }
    }

    private static void EnsureDirectoryIsNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Plugin staging directories cannot be reparse points.");
        }
    }

    private static void EnsurePathHasNoReparsePoints(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var pathRoot = Path.GetPathRoot(fullPath) ??
            throw new InvalidDataException("Plugin staging root is malformed.");
        var relative = Path.GetRelativePath(pathRoot, fullPath);
        var current = pathRoot;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current))
            {
                EnsureDirectoryIsNotReparsePoint(current);
            }
        }
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string NormalizeDirectoryPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsSameOrUnder(string rootPath, string candidatePath)
    {
        return candidatePath.Equals(rootPath, PathComparison) ||
            candidatePath.StartsWith(
                rootPath + Path.DirectorySeparatorChar,
                PathComparison);
    }

    private static void DeleteIfEmpty(string path, string runtimeRootPath)
    {
        var normalized = NormalizeDirectoryPath(path);
        if (normalized.Equals(runtimeRootPath, PathComparison) ||
            !IsSameOrUnder(runtimeRootPath, normalized) ||
            !Directory.Exists(normalized) ||
            Directory.EnumerateFileSystemEntries(normalized).Any())
        {
            return;
        }

        Directory.Delete(normalized);
    }

    private string GetRuntimePath(PluginPackageDescriptor package)
    {
        var path = NormalizeDirectoryPath(Path.Combine(
            runtimeRoot,
            package.Manifest.PluginId,
            package.Manifest.Version,
            package.Fingerprint));
        EnsureUnderRoot(runtimeRoot, path);
        return path;
    }

    private InstallOperationRow? GetOperationByStagedPath(string stagedPath)
    {
        return executor.Query<InstallOperationRow>(
            """
            SELECT OperationId, PluginId, PackageFingerprint, StagedPath, RuntimePath, State
            FROM PluginInstallOperation
            WHERE StagedPath = @Path;
            """,
            new { Path = NormalizeDirectoryPath(stagedPath) }).SingleOrDefault();
    }

    private InstallOperationRow? GetOperationByPath(string path)
    {
        var normalized = NormalizeDirectoryPath(path);
        return executor.Query<InstallOperationRow>(
            """
            SELECT OperationId, PluginId, PackageFingerprint, StagedPath, RuntimePath, State
            FROM PluginInstallOperation
            WHERE StagedPath = @Path OR RuntimePath = @Path
            ORDER BY CreatedAtUtc DESC
            LIMIT 1;
            """,
            new { Path = normalized }).SingleOrDefault();
    }

    private bool IsRuntimeAuthoritative(string runtimePath)
    {
        return executor.QueryScalar<int>(
            """
            SELECT COUNT(*)
            FROM InstalledPluginVersionPointer pointer
            JOIN InstalledPluginVersion version
              ON version.PluginId = pointer.PluginId
             AND version.PackageFingerprint IN (
                 pointer.ActiveFingerprint,
                 pointer.LastKnownGoodFingerprint,
                 pointer.PendingActivationFingerprint)
            WHERE version.RuntimePath = @RuntimePath;
            """,
            new { RuntimePath = NormalizeDirectoryPath(runtimePath) }) > 0;
    }

    private void MarkCompleted(string operationId)
    {
        executor.Execute(
            """
            UPDATE PluginInstallOperation
            SET State = 'Completed', UpdatedAtUtc = @UpdatedAtUtc
            WHERE OperationId = @OperationId AND State <> 'Completed';
            """,
            new
            {
                OperationId = operationId,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
    }

    private void TryCompleteOperation(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return;
        }

        try
        {
            MarkCompleted(operationId);
        }
        catch
        {
        }
    }

    private void PruneCompletedOperationHistory()
    {
        executor.Execute(
            """
            DELETE FROM PluginInstallOperation
            WHERE State = 'Completed'
              AND OperationId NOT IN (
                  SELECT OperationId
                  FROM PluginInstallOperation
                  WHERE State = 'Completed'
                  ORDER BY UpdatedAtUtc DESC, OperationId DESC
                  LIMIT 256);
            """);
    }

    private static void EnsureUnderRoot(string expectedRoot, string candidate)
    {
        var normalizedRoot = NormalizeDirectoryPath(expectedRoot);
        var normalizedCandidate = NormalizeDirectoryPath(candidate);
        if (normalizedCandidate.Equals(normalizedRoot, PathComparison) ||
            !IsSameOrUnder(normalizedRoot, normalizedCandidate))
        {
            throw new InvalidDataException("Plugin operation path is outside its canonical root.");
        }
    }

    private static void TryDeleteUnderRoot(string expectedRoot, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalized = NormalizeDirectoryPath(path);
        EnsureUnderRoot(expectedRoot, normalized);
        if (!Directory.Exists(normalized))
        {
            return;
        }

        EnsurePathHasNoReparsePoints(normalized);
        Directory.Delete(normalized, recursive: true);
    }

    private static class InstallState
    {
        public const string Staged = "Staged";
        public const string Verified = "Verified";
        public const string ReadyToPromote = "ReadyToPromote";
        public const string Promoted = "Promoted";
        public const string StateCommitted = "StateCommitted";
        public const string Completed = "Completed";
    }

    private sealed record InstallOperationRow(
        string OperationId,
        string PluginId,
        string PackageFingerprint,
        string StagedPath,
        string RuntimePath,
        string State);
}
