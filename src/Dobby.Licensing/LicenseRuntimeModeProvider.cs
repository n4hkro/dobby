using Dobby.Contracts.Licensing;
using Dobby.Core;
using Dobby.Security.CurrentContext;

namespace Dobby.Licensing;

/// <remarks>
/// <paramref name="currentDeviceFingerprintProvider"/> returns a <see cref="Result{T}"/>
/// because the current device's binding digest is not always derivable: `ADR-0063` § 5
/// requires an unavailable hardware signal to fail closed rather than be normalized into a
/// placeholder, and a `Func&lt;string&gt;` seam can only express "unavailable" as a sentinel
/// string or a thrown exception. The sentinel would be compared against the persisted digest
/// (accidentally fail-closed, but by luck), and the exception escaped this call onto the WPF
/// dispatcher with no handler on the path.
/// </remarks>
public sealed class LicenseRuntimeModeProvider(
    ILicenseStateStore stateStore,
    CurrentCustomerDeploymentContextService currentContextService,
    Func<Result<string>> currentDeviceFingerprintProvider,
    TimeProvider timeProvider) : ILicenseRuntimeModeProvider
{
    public LicenseRuntimeModeProvider(
        ILicenseStateStore stateStore,
        CurrentCustomerDeploymentContextService currentContextService,
        Func<Result<string>> currentDeviceFingerprintProvider)
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
        if (!currentDeviceFingerprint.IsSuccess ||
            !LicenseDeviceFingerprintV1.Matches(currentDeviceFingerprint.Value, state.DeviceFingerprint))
        {
            // A non-derivable current digest and a mismatching one are the same answer here,
            // and deliberately the same error code: `ADR-0063` § 4 makes the mismatch message
            // non-disclosing, so this boundary must not let a caller distinguish "your device
            // is not the licensed one" from "this device's hardware signal is unreadable".
            return Result.Failure<PluginRuntimeMode>(ErrorCode.LicenseBindingMismatch);
        }

        var lifecycleState = state.SourceIntent.HasValue &&
            state.NotBeforeUtc.HasValue &&
            state.ExpiresAtUtc.HasValue &&
            state.GracePeriodDays.HasValue
            ? LicenseRuntimeModeService.EvaluateLifecycle(
                state.SourceIntent.Value,
                state.NotBeforeUtc.Value,
                state.ExpiresAtUtc.Value,
                state.GracePeriodDays.Value,
                timeProvider.GetUtcNow(),
                state.TrustedTimeHealth)
            : state.SourceStatus.HasValue &&
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
