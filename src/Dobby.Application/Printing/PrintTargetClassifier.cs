using Dobby.Core;

namespace Dobby.Application.Printing;

public interface IPrintTargetClassifier
{
    PrintTargetClass Classify(PrintTargetDescriptor target);
}

public sealed record PrintTargetDescriptor(
    string TargetName,
    string DriverName,
    string PortName,
    bool IsLocalPhysicalPrinter);

public enum PrintTargetClass
{
    PhysicalPrinter,
    VirtualOrFileOutput,
    Unknown
}

public enum PrintOutputDecision
{
    Denied,
    ConfirmationRequired
}

public static class PrintOutputPolicy
{
    public static bool AllowsOutput(PrintTargetClass targetClass)
    {
        return targetClass == PrintTargetClass.PhysicalPrinter;
    }

    public static PrintOutputDecision EvaluateReadOnlyExpiredOutput(
        PrintTargetClass targetClass,
        bool isPrintToFile)
    {
        return !isPrintToFile && AllowsOutput(targetClass)
            ? PrintOutputDecision.ConfirmationRequired
            : PrintOutputDecision.Denied;
    }
}
