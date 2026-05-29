using System.Globalization;
using Dobby.Diagnostics;

namespace Dobby.Host;

public sealed class ShellLocalizationDiagnosticsRecorder(
    Action<LocalizationMissingKeyEvent>? sink = null,
    string resourceScope = "Dobby.Host")
{
    private readonly List<LocalizationMissingKeyEvent> events = [];

    public IReadOnlyList<LocalizationMissingKeyEvent> Snapshot()
    {
        lock (events)
        {
            return events.ToArray();
        }
    }

    public void RecordMissingKey(string resourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

        var evt = new LocalizationMissingKeyEvent(
            CultureInfo.CurrentUICulture.Name,
            resourceKey,
            resourceScope,
            "resource-missing;returned-marker");

        lock (events)
        {
            events.Add(evt);
        }

        sink?.Invoke(evt);
    }
}
