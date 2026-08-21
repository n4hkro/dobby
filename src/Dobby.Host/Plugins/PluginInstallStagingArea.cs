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
    private readonly string root;
    private readonly IHostPersistenceCommandExecutor executor;
    private readonly string stagingRoot;
    private readonly string runtimeRoot;

    public PluginInstallStagingArea(string dataRoot, IHostPersistenceCommandExecutor executor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        this.executor = executor;
        root = Path.Combine(Environment.ExpandEnvironmentVariables(dataRoot), "plugins");
        stagingRoot = Path.Combine(root, "staging");
        runtimeRoot = Path.Combine(root, "installed");
        Directory.CreateDirectory(stagingRoot);
        Directory.CreateDirectory(runtimeRoot);
    }

    public Result<string> Stage(PluginPackageDescriptor package)
    {
        ArgumentNullException.ThrowIfNull(package);

        string? stagedPath = null;
        try
        {
            var stagingId = Guid.NewGuid().ToString("N");
            stagedPath = Path.Combine(stagingRoot, stagingId);
            Directory.CreateDirectory(stagedPath);
            MaterializePackage(stagedPath, package);
            RecordStaging(stagingId, package, stagedPath, state: "staged", runtimePath: null);
            return Result.Success(stagedPath);
        }
        catch
        {
            TryDeleteDirectory(stagedPath);
            return Result.Failure<string>(ErrorCode.IoFailed);
        }
    }

    public Result<string> Promote(string stagedPath, PluginPackageDescriptor package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedPath);
        ArgumentNullException.ThrowIfNull(package);

        string? runtimePath = null;
        try
        {
            runtimePath = Path.Combine(runtimeRoot, package.Manifest.PluginId, package.Manifest.Version, package.Fingerprint);
            Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);
            if (Directory.Exists(runtimePath))
            {
                Directory.Delete(runtimePath, recursive: true);
            }

            Directory.Move(stagedPath, runtimePath);
            UpdateStaging(stagedPath, state: "promoted", runtimePath);
            return Result.Success(runtimePath);
        }
        catch
        {
            TryDeleteDirectory(runtimePath);
            TryRemoveStagingRow(stagedPath, runtimePath);
            return Result.Failure<string>(ErrorCode.IoFailed);
        }
    }

    public Result Cleanup(string stagedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedPath);

        try
        {
            if (Directory.Exists(stagedPath))
            {
                Directory.Delete(stagedPath, recursive: true);
            }

            executor.Execute(
                "DELETE FROM PluginInstallStaging WHERE StagedPath = @StagedPath OR RuntimePath = @StagedPath;",
                new { StagedPath = stagedPath });
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
            var rows = executor.Query<StagingRow>(
                "SELECT StagingId, PluginId, PackageFingerprint, StagedPath, RuntimePath, State FROM PluginInstallStaging ORDER BY CreatedAtUtc;");
            var removed = 0;
            foreach (var row in rows)
            {
                if (row.State.Equals("staged", StringComparison.OrdinalIgnoreCase))
                {
                    if (Directory.Exists(row.StagedPath))
                    {
                        Directory.Delete(row.StagedPath, recursive: true);
                    }

                    executor.Execute("DELETE FROM PluginInstallStaging WHERE StagingId = @StagingId;", new { row.StagingId });
                    removed++;
                    continue;
                }

                var installed = executor.QueryScalar<int>(
                    "SELECT COUNT(*) FROM InstalledPluginState WHERE PluginId = @PluginId AND PackageFingerprint = @PackageFingerprint;",
                    new { row.PluginId, row.PackageFingerprint });
                if (installed == 0)
                {
                    if (!string.IsNullOrWhiteSpace(row.RuntimePath) && Directory.Exists(row.RuntimePath))
                    {
                        Directory.Delete(row.RuntimePath, recursive: true);
                    }

                    executor.Execute("DELETE FROM PluginInstallStaging WHERE StagingId = @StagingId;", new { row.StagingId });
                    removed++;
                }
                else
                {
                    executor.Execute("DELETE FROM PluginInstallStaging WHERE StagingId = @StagingId;", new { row.StagingId });
                    removed++;
                }
            }

            return Result.Success(removed);
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

    private void RecordStaging(string stagingId, PluginPackageDescriptor package, string stagedPath, string state, string? runtimePath)
    {
        executor.Execute(
            """
            INSERT INTO PluginInstallStaging (
                StagingId, PluginId, PackageFingerprint, StagedPath, RuntimePath, State, CreatedAtUtc, UpdatedAtUtc
            ) VALUES (
                @StagingId, @PluginId, @PackageFingerprint, @StagedPath, @RuntimePath, @State, @CreatedAtUtc, @UpdatedAtUtc
            );
            """,
            new
            {
                StagingId = stagingId,
                PluginId = package.Manifest.PluginId,
                PackageFingerprint = package.Fingerprint,
                StagedPath = stagedPath,
                RuntimePath = runtimePath,
                State = state,
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
    }

    private void UpdateStaging(string stagedPath, string state, string runtimePath)
    {
        executor.Execute(
            """
            UPDATE PluginInstallStaging
            SET State = @State,
                RuntimePath = @RuntimePath,
                UpdatedAtUtc = @UpdatedAtUtc
            WHERE StagedPath = @StagedPath;
            """,
            new
            {
                StagedPath = stagedPath,
                RuntimePath = runtimePath,
                State = state,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
    }

    private void TryRemoveStagingRow(string stagedPath, string? runtimePath)
    {
        try
        {
            executor.Execute(
                "DELETE FROM PluginInstallStaging WHERE StagedPath = @StagedPath OR RuntimePath = @RuntimePath;",
                new { StagedPath = stagedPath, RuntimePath = runtimePath });
        }
        catch
        {
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

    private static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed record StagingRow(
        string StagingId,
        string PluginId,
        string PackageFingerprint,
        string StagedPath,
        string? RuntimePath,
        string State);
}
