using System.Diagnostics;
using System.IO;
using System.Text;
using Dobby.Diagnostics;

namespace Dobby.Host.Plugins;

public sealed class PluginRuntimeDiagnosticsFileSink(string diagnosticsDirectory)
{
    public const string FileName = "plugin-runtime-diagnostics.log";
    private const string WriteFailureMarker = "plugin.runtime.diagnostics.write.failed";
    private readonly object writeLock = new();

    public void Record(PluginRuntimeDiagnosticEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            lock (writeLock)
            {
                Directory.CreateDirectory(diagnosticsDirectory);
                File.AppendAllText(
                    Path.Combine(diagnosticsDirectory, FileName),
                    DiagnosticRedactor.Redact(Format(entry)),
                    Encoding.UTF8);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            TryRecordWriteFailure();
        }
    }

    private static void TryRecordWriteFailure()
    {
        try
        {
            Trace.TraceWarning(WriteFailureMarker);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    }

    private static string Format(PluginRuntimeDiagnosticEntry entry)
    {
        return string.Join(
            '\t',
            DateTimeOffset.UtcNow.ToString("O"),
            $"pluginId={entry.PluginId}",
            $"marker={entry.Marker}",
            $"details={entry.Details}") + Environment.NewLine;
    }
}
