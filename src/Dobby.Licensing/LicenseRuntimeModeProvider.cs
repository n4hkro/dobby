using Dobby.Contracts.Licensing;
using Dobby.Core;
using Dobby.Security.CurrentContext;

namespace Dobby.Licensing;

public sealed class LicenseRuntimeModeProvider(
    ILicenseStateStore stateStore,
    CurrentCustomerDeploymentContextService currentContextService,
    Func<string> currentDeviceFingerprintProvider,
    TimeProvider timeProvider) : ILicenseRuntimeModeProvider
{
    public LicenseRuntimeModeProvider(
        ILicenseStateStore stateStore,
        CurrentCustomerDeploymentContextService currentContextService,
        Func<string> currentDeviceFingerprintProvider)
        : this(stateStore, currentContextService, currentDeviceFingerprintProvider, TimeProvider.System)
    {
    }

    public Result<PluginRuntimeMode> GetCurrentMode(string pluginId, string packageFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFingerprint);

        var context = currentContextService.GetCurrentContext();
        if (!context.IsSuccess)
        {
            return Result.Failure<PluginRuntimeMode>(ErrorCode.LicenseBindingMismatch);
        }

        var revisionKey = new RevisionSeriesKey(
            pluginId,
            context.Context!.CustomerId,
            context.Context.DeploymentId);
        var state = stateStore.Read(revisionKey);
        if (state is null)
        {
            return Result.Success(PluginRuntimeMode.Denied);
        }

        if (!state.PluginId.Equals(pluginId, StringComparison.Ordinal) ||
            !state.CustomerId.Equals(context.Context.CustomerId, StringComparison.Ordinal) ||
            !state.DeploymentId.Equals(context.Context.DeploymentId, StringComparison.Ordinal) ||
            !state.PackageFingerprint.Equals(packageFingerprint, StringComparison.Ordinal))
        {
            return Result.Failure<PluginRuntimeMode>(ErrorCode.LicenseBindingMismatch);
        }

        var currentDeviceFingerprint = currentDeviceFingerprintProvider();
        if (!LicenseDeviceFingerprintV1.Matches(currentDeviceFingerprint, state.DeviceFingerprint))
        {
            return Result.Failure<PluginRuntimeMode>(ErrorCode.LicenseBindingMismatch);
        }

        var lifecycleState = state.SourceStatus.HasValue &&
            state.NotBeforeUtc.HasValue &&
            state.ExpiresAtUtc.HasValue &&
            state.GracePeriodDays.HasValue
            ? LicenseRuntimeModeService.EvaluateLifecycle(
                state.SourceStatus.Value,
                state.NotBeforeUtc.Value,
                state.ExpiresAtUtc.Value,
                state.GracePeriodDays.Value,
                timeProvider.GetUtcNow())
            : state.LifecycleState;

        return Result.Success(LicenseRuntimeModeMapper.Map(lifecycleState));
    }
}
