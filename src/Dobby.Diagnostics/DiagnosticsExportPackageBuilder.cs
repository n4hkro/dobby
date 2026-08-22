using System.IO.Compression;
using System.Text;

namespace Dobby.Diagnostics;

public sealed class DiagnosticsExportPackageBuilder
{
    private const int MaximumFiles = 128;
    private const long MaximumFileBytes = 4L * 1024 * 1024;
    private const long MaximumTotalBytes = 16L * 1024 * 1024;

    private static readonly IReadOnlySet<string> AllowedExtensions =
        new HashSet<string>([".json", ".log", ".txt"], StringComparer.OrdinalIgnoreCase);

    private static readonly string[] ExcludedFileNameFragments =
    [
        "password",
        "token",
        "privatekey",
        "session",
        "remember",
        "recovery",
        "licensepayload",
        "licenseartifact",
        "licensebinding",
        "pluginruntimediagnostics"
    ];

    public DiagnosticsExportPackage Build(string sourceDirectory, string outputZipPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputZipPath);

        var sourceRoot = Path.GetFullPath(sourceDirectory);
        var outputPath = Path.GetFullPath(outputZipPath);
        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException(sourceRoot);
        }

        if (IsWithinRoot(outputPath, sourceRoot))
        {
            throw new IOException("The diagnostics output must be outside the diagnostics source directory.");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var included = new List<string>();
        var excluded = new List<string>();
        var totalBytes = 0L;
        var temporaryPath = outputPath + "." + Guid.CreateVersion7().ToString("N") + ".tmp";

        try
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                var candidates = Directory
                    .EnumerateFiles(sourceRoot, "*", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = false,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    })
                    .Select(path => new FileInfo(Path.GetFullPath(path)))
                    .Where(file => IsWithinRoot(file.FullName, sourceRoot))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ThenBy(file => file.FullName, PathComparer())
                    .ToArray();

                foreach (var file in candidates)
                {
                    var relativePath = Path.GetRelativePath(sourceRoot, file.FullName).Replace('\\', '/');
                    var budgetedBytes = Math.Min(file.Length, MaximumFileBytes);
                    if (included.Count >= MaximumFiles ||
                        totalBytes + budgetedBytes > MaximumTotalBytes ||
                        ShouldExclude(relativePath))
                    {
                        excluded.Add(relativePath);
                        continue;
                    }

                    string redactedContents;
                    try
                    {
                        redactedContents = ReadRedactedTail(file.FullName, MaximumFileBytes);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        excluded.Add(relativePath);
                        continue;
                    }

                    var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                    writer.Write(redactedContents);
                    included.Add(relativePath);
                    totalBytes += budgetedBytes;
                }
            }

            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }

        return new DiagnosticsExportPackage(outputPath, included, excluded, Uploaded: false);
    }

    private static bool ShouldExclude(string relativePath)
    {
        var normalized = relativePath.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        return !AllowedExtensions.Contains(Path.GetExtension(relativePath)) ||
               ExcludedFileNameFragments.Any(fragment => normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadRedactedTail(string path, long maximumBytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        var truncated = stream.Length > maximumBytes;
        if (truncated)
        {
            stream.Seek(-maximumBytes, SeekOrigin.End);
        }

        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(false, false),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);
        var output = new StringBuilder((int)Math.Min(maximumBytes, int.MaxValue));
        if (truncated)
        {
            _ = reader.ReadLine();
            output.AppendLine("<truncated:tail-only>");
        }

        while (reader.ReadLine() is { } line)
        {
            output.AppendLine(DiagnosticRedactor.Redact(line));
        }

        return output.ToString();
    }

    private static bool IsWithinRoot(string candidatePath, string rootPath)
    {
        var comparison = PathComparison();
        var canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var canonicalCandidate = Path.GetFullPath(candidatePath);
        if (string.Equals(canonicalCandidate, canonicalRoot, comparison))
        {
            return true;
        }

        return canonicalCandidate.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, comparison);
    }

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
