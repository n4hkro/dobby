namespace Dobby.PluginFramework.Packaging;

public sealed record PluginPackageValidationReason(
    string Code,
    string Message,
    string? Path = null);
