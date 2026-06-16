namespace Dobby.Contracts.PluginPages;

public sealed record PluginNavigationContribution(
    string PluginId,
    string ExtensionPointId,
    string RouteKey,
    string LabelKey,
    string Target,
    string? ParentId,
    string? Group,
    int? Weight,
    int DeclarationIndex);
