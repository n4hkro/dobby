using Dobby.Application.Printing;

namespace Dobby.Infrastructure.Printing;

public sealed class WindowsPrintTargetClassifier : IPrintTargetClassifier
{
    public PrintTargetClass Classify(PrintTargetDescriptor target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.IsVirtual ||
            target.PortName?.Contains("FILE", StringComparison.OrdinalIgnoreCase) == true ||
            target.PortName?.Contains("PDF", StringComparison.OrdinalIgnoreCase) == true)
        {
            return PrintTargetClass.FileOutput;
        }

        return string.IsNullOrWhiteSpace(target.PortName)
            ? PrintTargetClass.Unknown
            : PrintTargetClass.PhysicalPrinter;
    }
}
