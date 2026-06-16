using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.PluginFramework;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.SDK.PluginFramework;

namespace Dobby.Sample.Plugin;

public sealed class SamplePluginModule : PluginBase
{
    public override void RegisterServices(IPluginServiceCollection services)
    {
        services.AddExtensionPoint<IPluginPage, SampleHomePage>();
    }
}
public sealed class SampleHomePage(IBackgroundOperationReporter backgroundOperations) : IPluginPage
{
    public PluginPageDescriptor Descriptor { get; } = new(
        "sample.home",
        "sample.home",
        "sample.home.label");

    public ValueTask<Result<IPluginPageModel>> LoadAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid();
        var correlationId = Guid.TryParse(context.CorrelationId, out var parsedCorrelationId)
            ? parsedCorrelationId
            : Guid.NewGuid();
        backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Plugin,
            context.PluginId,
            "sample.home.load",
            null,
            BackgroundOperationStatus.Running,
            BackgroundOperationProgress.Indeterminate(),
            CanCancel: false,
            correlationId,
            FailureReason: null));

        cancellationToken.ThrowIfCancellationRequested();

        backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Plugin,
            context.PluginId,
            "sample.home.load",
            null,
            BackgroundOperationStatus.Completed,
            BackgroundOperationProgress.Determinate(100),
            CanCancel: false,
            correlationId,
            FailureReason: null));
        return ValueTask.FromResult(Result.Success<IPluginPageModel>(new PluginTextPageModel("sample.home.ready")));
    }

    public ValueTask<Result> ActivateAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Result.Success());
    }

    public ValueTask<Result> DeactivateAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Result.Success());
    }

    public ValueTask<Result> UnloadAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Result.Success());
    }
}
