using System.IO.Compression;

namespace Dobby.Diagnostics;

public sealed class DiagnosticsExportPackageBuilder
{
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

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(sourceDirectory);
        }

        var outputDirectory = Path.GetDirectoryName(outputZipPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (File.Exists(outputZipPath))
        {
            File.Delete(outputZipPath);
        }

        var included = new List<string>();
        var excluded = new List<string>();

        using var archive = ZipFile.Open(outputZipPath, ZipArchiveMode.Create);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
            if (ShouldExclude(relativePath))
            {
                excluded.Add(relativePath);
                continue;
            }

            var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(DiagnosticRedactor.Redact(File.ReadAllText(file)));
            included.Add(relativePath);
        }

        return new DiagnosticsExportPackage(outputZipPath, included, excluded, Uploaded: false);
    }

    private static bool ShouldExclude(string relativePath)
    {
        var normalized = relativePath.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        return ExcludedFileNameFragments.Any(fragment => normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
