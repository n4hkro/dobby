using Dobby.Core;

namespace Dobby.Contracts.PluginPages;

public sealed record PluginPageDescriptor(
    string ExtensionPointId,
    string RouteKey,
    string LabelKey);

public sealed record PluginPageContext(
    string PluginId,
    string ExtensionPointId,
    string RouteKey,
    string CorrelationId);

public interface IPluginPageModel
{
}

public sealed record PluginTextPageModel(string MessageKey) : IPluginPageModel;

public interface IPluginPage
{
    PluginPageDescriptor Descriptor { get; }

    ValueTask<Result<IPluginPageModel>> LoadAsync(
        PluginPageContext context,
        CancellationToken cancellationToken);

    ValueTask<Result> ActivateAsync(
        PluginPageContext context,
        CancellationToken cancellationToken);

    ValueTask<Result> DeactivateAsync(
        PluginPageContext context,
        CancellationToken cancellationToken);

    ValueTask<Result> UnloadAsync(
        PluginPageContext context,
        CancellationToken cancellationToken);
}
