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

public sealed record PluginPageActionContext(
    string PluginId,
    string RouteKey,
    string? UserId,
    string CorrelationId);

public sealed record PluginPageActionResult(string MessageKey);

public sealed record PluginPageAction(
    string ActionId,
    string LabelKey,
    Func<PluginPageActionContext, CancellationToken, ValueTask<Result<PluginPageActionResult>>> ExecuteAsync);

public sealed record PluginActionPageModel(
    string MessageKey,
    PluginPageActionContext Context,
    IReadOnlyList<PluginPageAction> Actions) : IPluginPageModel;

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
