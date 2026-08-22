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
    string CorrelationId,
    string? UserId = null);

public interface IPluginPageModel
{
}

public sealed record PluginTextPageModel(string MessageKey) : IPluginPageModel;

public sealed record PluginPageCommandContext(
    string PluginId,
    string RouteKey,
    string? UserId,
    string CorrelationId);

public sealed record PluginWorkSessionHandle(
    Guid SessionId,
    string OwnerId,
    string RouteKey,
    string LabelKey);

public interface IPluginWorkSessionService
{
    Result<PluginWorkSessionHandle> Open(string ownerId, string routeKey, string labelKey);

    Result UpdateProgress(Guid sessionId, int percent);

    Result RecordResult(Guid sessionId, string resultSummaryKey);

    Result RecordError(Guid sessionId, string errorSummaryKey);

    Result Close(Guid sessionId);
}

public interface IPluginWorkSessionCleanup
{
    Result CloseOwnedBy(string ownerId);
}

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
