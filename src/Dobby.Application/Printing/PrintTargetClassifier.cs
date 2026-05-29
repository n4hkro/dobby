namespace Dobby.Application.Printing;

public interface IPrintTargetClassifier
{
    PrintTargetClass Classify(PrintTargetDescriptor target);
}

public sealed record PrintTargetDescriptor(string Name, string? PortName, bool IsVirtual);

public enum PrintTargetClass
{
    PhysicalPrinter,
    FileOutput,
    Unknown
}

public static class PrintOutputPolicy
{
    public static bool AllowsOutput(PrintTargetClass targetClass)
    {
        return targetClass == PrintTargetClass.PhysicalPrinter;
    }
}
