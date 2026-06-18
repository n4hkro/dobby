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

public static class PrintOutputPolicy
{
    public static bool AllowsOutput(PrintTargetClass targetClass)
    {
        return targetClass == PrintTargetClass.PhysicalPrinter;
    }

    public static Result EnsureReadOnlyExpiredOutputAllowed(PrintTargetClass targetClass)
    {
        return AllowsOutput(targetClass)
            ? Result.Success()
            : Result.Failure(ErrorCode.LicenseReadOnly);
    }
}
