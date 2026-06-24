using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Policies;
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
public sealed class SampleHomePage(
    IBackgroundOperationReporter backgroundOperations,
    ICapabilityPolicy<OperationPermissionCapability> authorizationPolicy,
    IPluginWorkSessionService workSessions) : IPluginPage
{
    private const string GreetOperationKey = "com.dobby.sample.greet.execute";

    public PluginPageDescriptor Descriptor { get; } = new(
        "sample.greet.page",
        "greet",
        "sample.nav.greet");

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
        var actionContext = new PluginPageActionContext(
            context.PluginId,
            context.RouteKey,
            context.UserId,
            context.CorrelationId);
        return ValueTask.FromResult(Result.Success<IPluginPageModel>(new PluginActionPageModel(
            "sample.home.ready",
            actionContext,
            [
                new PluginPageAction("sample.greet", "sample.greet.label", ExecuteGreetAsync),
                new PluginPageAction("sample.invalid-input", "sample.invalid-input.label", ExecuteInvalidInputAsync),
                new PluginPageAction("sample.fault", "sample.fault.label", ExecuteFaultAsync),
                new PluginPageAction("sample.progress", "sample.progress.label", ExecuteProgressAsync),
                new PluginPageAction("sample.work-session", "sample.work-session.label", ExecuteWorkSessionAsync)
            ])));
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

    private ValueTask<Result<PluginPageActionResult>> ExecuteGreetAsync(
        PluginPageActionContext context,
        CancellationToken cancellationToken)
    {
        var decision = authorizationPolicy.Evaluate(
            new OperationPermissionCapability(GreetOperationKey),
            new PolicyContext(context.UserId, context.PluginId, CorrelationIdFrom(context.CorrelationId)));
        if (!decision.IsSuccess || !decision.Value.Allowed)
        {
            return ValueTask.FromResult(Result.Failure<PluginPageActionResult>(ErrorCode.Forbidden));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result.Success(new PluginPageActionResult("sample.greet.success")));
    }

    private static ValueTask<Result<PluginPageActionResult>> ExecuteInvalidInputAsync(
        PluginPageActionContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Result.Failure<PluginPageActionResult>(ErrorCode.InvalidInput));
    }

    private static ValueTask<Result<PluginPageActionResult>> ExecuteFaultAsync(
        PluginPageActionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("sample.fault.unhandled");
    }

    private ValueTask<Result<PluginPageActionResult>> ExecuteProgressAsync(
        PluginPageActionContext context,
        CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid();
        var correlationId = GuidFrom(context.CorrelationId);
        backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Plugin,
            context.PluginId,
            "sample.greet.progress",
            null,
            BackgroundOperationStatus.Running,
            BackgroundOperationProgress.Determinate(50),
            CanCancel: false,
            correlationId,
            FailureReason: null));

        cancellationToken.ThrowIfCancellationRequested();

        backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Plugin,
            context.PluginId,
            "sample.greet.progress",
            null,
            BackgroundOperationStatus.Completed,
            BackgroundOperationProgress.Determinate(100),
            CanCancel: false,
            correlationId,
            FailureReason: null));
        return ValueTask.FromResult(Result.Success(new PluginPageActionResult("sample.greet.progress.completed")));
    }

    private ValueTask<Result<PluginPageActionResult>> ExecuteWorkSessionAsync(
        PluginPageActionContext context,
        CancellationToken cancellationToken)
    {
        var session = workSessions.Open(context.PluginId, context.RouteKey, "sample.work-session");
        if (!session.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<PluginPageActionResult>(session.ErrorCode ?? ErrorCode.PluginInternalError));
        }

        var progress = workSessions.UpdateProgress(session.Value.SessionId, 50);
        if (!progress.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<PluginPageActionResult>(progress.ErrorCode ?? ErrorCode.PluginInternalError));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result = workSessions.RecordResult(session.Value.SessionId, "sample.work-session.completed");
        if (!result.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<PluginPageActionResult>(result.ErrorCode ?? ErrorCode.PluginInternalError));
        }

        return ValueTask.FromResult(Result.Success(new PluginPageActionResult("sample.work-session.completed")));
    }

    private static CorrelationId CorrelationIdFrom(string value)
    {
        return Guid.TryParse(value, out var parsed)
            ? new CorrelationId(parsed)
            : CorrelationId.New();
    }

    private static Guid GuidFrom(string value)
    {
        return Guid.TryParse(value, out var parsed) ? parsed : Guid.NewGuid();
    }
}
