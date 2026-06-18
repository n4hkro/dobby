namespace Dobby.Infrastructure.Printing;

public sealed class WindowsPrintTargetClassifier
{
    public WindowsPrintTargetClass Classify(WindowsPrintTargetDescriptor target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (string.IsNullOrWhiteSpace(target.PortName))
        {
            return WindowsPrintTargetClass.Unknown;
        }

        if (!target.IsLocalPhysicalPrinter ||
            ContainsVirtualMarker(target.TargetName) ||
            ContainsVirtualMarker(target.DriverName) ||
            ContainsVirtualMarker(target.PortName))
        {
            return WindowsPrintTargetClass.VirtualOrFileOutput;
        }

        return WindowsPrintTargetClass.PhysicalPrinter;
    }

    private static bool ContainsVirtualMarker(string value)
    {
        return value.Contains("FILE", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("PDF", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("XPS", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("PORTPROMPT", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record WindowsPrintTargetDescriptor(
    string TargetName,
    string DriverName,
    string PortName,
    bool IsLocalPhysicalPrinter);

public enum WindowsPrintTargetClass
{
    PhysicalPrinter,
    VirtualOrFileOutput,
    Unknown
}
