namespace Dobby.Diagnostics;

public sealed record DiagnosticsExportPackage(
    string Path,
    IReadOnlyList<string> IncludedEntries,
    IReadOnlyList<string> ExcludedEntries,
    bool Uploaded);
