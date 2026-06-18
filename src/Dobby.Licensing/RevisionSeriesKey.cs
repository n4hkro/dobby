namespace Dobby.Licensing;

public sealed record RevisionSeriesKey(
    string PluginId,
    string CustomerId,
    string DeploymentId);
