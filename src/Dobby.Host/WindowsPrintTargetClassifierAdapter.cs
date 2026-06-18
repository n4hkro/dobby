using Dobby.Application.Printing;
using Dobby.Infrastructure.Printing;

namespace Dobby.Host;

public sealed class WindowsPrintTargetClassifierAdapter(
    WindowsPrintTargetClassifier classifier) : IPrintTargetClassifier
{
    public PrintTargetClass Classify(PrintTargetDescriptor target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var result = classifier.Classify(new WindowsPrintTargetDescriptor(
            target.TargetName,
            target.DriverName,
            target.PortName,
            target.IsLocalPhysicalPrinter));

        return result switch
        {
            WindowsPrintTargetClass.PhysicalPrinter => PrintTargetClass.PhysicalPrinter,
            WindowsPrintTargetClass.VirtualOrFileOutput => PrintTargetClass.VirtualOrFileOutput,
            _ => PrintTargetClass.Unknown
        };
    }
}
