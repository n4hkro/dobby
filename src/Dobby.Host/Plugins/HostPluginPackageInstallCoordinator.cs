using Dobby.Contracts.Policies;
using Dobby.Contracts.Licensing;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authorization;
using Dobby.Security.Trust;
using System.IO;

namespace Dobby.Host.Plugins;

public enum PluginPackageInstallStage
{
    Reading,
    Verifying,
    Finishing
}

public sealed record PluginPackageInstallProgress(
    PluginPackageInstallStage Stage,
    int Percent,
    bool CanCancel);

[PluginRuntimeActionOwner(PluginRuntimeAction.InitialPackageInstall, PluginRuntimeAction.PackageUpdate)]
public sealed class HostPluginPackageInstallCoordinator(
    IInstalledPluginStateStore stateStore,
    IInstalledPluginMetadataCache metadataCache,
    IPluginInstallStagingArea stagingArea,
    OperationAuthorizationPolicy authorizationPolicy,
    PluginRuntimeAuditCoordinator auditCoordinator,
    IPluginTrustVerifier pluginTrustVerifier,
    HostRuntimeModeMutationGate mutationGate,
    PluginRuntimeDiagnosticsCoordinator? diagnosticsCoordinator = null,
    PluginPackageLimits? packageLimits = null,
    IPluginInstallStateCommitter? installStateCommitter = null,
    IPluginCurrentProcessActivationTracker? activationTracker = null)
{
    private const int MaximumConcurrentPluginInstalls = 4;
    private readonly PluginInstallConcurrencyGate installConcurrency = new(MaximumConcurrentPluginInstalls);
    private readonly OperationAuthorizationPolicy authorization = authorizationPolicy ?? throw new ArgumentNullException(nameof(authorizationPolicy));
    private readonly IPluginTrustVerifier trustVerifier = pluginTrustVerifier ?? throw new ArgumentNullException(nameof(pluginTrustVerifier));
    private readonly PluginRuntimeDiagnosticsCoordinator? diagnostics = diagnosticsCoordinator;
    private readonly PluginPackageLimits limits = packageLimits ?? PluginPackageLimits.Default;
    private readonly IPluginInstallStateCommitter? stateCommitter = installStateCommitter;
    private readonly IPluginCurrentProcessActivationTracker? currentProcessActivationTracker = activationTracker;
    private readonly HostRuntimeModeMutationGate runtimeModeMutationGate = mutationGate ??
        throw new ArgumentNullException(nameof(mutationGate));

    public Result<InstalledPluginState> InstallPackage(
        string actorId,
        string packagePath,
        HostVersion hostVersion,
        CorrelationId correlationId,
        CancellationToken cancellationToken = default,
        IProgress<PluginPackageReadProgress>? progress = null)
    {
        return InstallPackageCore(
            actorId,
            packagePath,
            hostVersion,
            correlationId,
            cancellationToken,
            progress,
            installProgress: null);
    }

    public Task<Result<InstalledPluginState>> InstallPackageAsync(
        string actorId,
        string packagePath,
        HostVersion hostVersion,
        CorrelationId correlationId,
        CancellationToken cancellationToken = default,
        IProgress<PluginPackageInstallProgress>? progress = null)
    {
        return Task.Factory.StartNew(
            () =>
            {
                try
                {
                    return InstallPackageCore(
                        actorId,
                        packagePath,
                        hostVersion,
                        correlationId,
                        cancellationToken,
                        readProgress: null,
                        progress);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return Result.Cancelled<InstalledPluginState>();
                }
            },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    private Result<InstalledPluginState> InstallPackageCore(
        string actorId,
        string packagePath,
        HostVersion hostVersion,
        CorrelationId correlationId,
        CancellationToken cancellationToken,
        IProgress<PluginPackageReadProgress>? readProgress,
        IProgress<PluginPackageInstallProgress>? installProgress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(auditCoordinator);
        cancellationToken.ThrowIfCancellationRequested();

        var authorized = Authorize(actorId, correlationId);
        if (!authorized.IsSuccess)
        {
            return Result.Failure<InstalledPluginState>(authorized.ErrorCode ?? ErrorCode.Forbidden);
        }

        auditCoordinator.AppendInstallAttempt(actorId, packagePath, correlationId);

        if (!IsSupportedSourcePackage(packagePath))
        {
            var reasons = new[]
            {
                new PluginPackageValidationReason(
                    "unsupported-package-source",
                    "Plugin packages must be installed from a local .dobbyplugin file.",
                    packagePath)
            };
            auditCoordinator.AppendInstallRejected(
                actorId,
                pluginId: null,
                reasons,
                correlationId);
            diagnostics?.RecordInstallRejected(null, reasons);
            return Result.Failure<InstalledPluginState>(ErrorCode.InvalidInput);
        }

        installProgress?.Report(new PluginPackageInstallProgress(
            PluginPackageInstallStage.Reading,
            0,
            CanCancel: true));
        var packageReadProgress = CreatePackageReadProgress(readProgress, installProgress);
        var archive = PluginPackageReader.Read(packagePath, limits, cancellationToken, packageReadProgress);
        if (!archive.IsSuccess)
        {
            var reasons = new[]
            {
                new PluginPackageValidationReason("invalid-manifest", "Plugin package could not be read.", PluginPackageReader.ManifestFileName)
            };
            auditCoordinator.AppendInstallRejected(
                actorId,
                pluginId: null,
                reasons,
                correlationId);
            diagnostics?.RecordInstallRejected(null, reasons);
            return Result.Failure<InstalledPluginState>(archive.ErrorCode ?? ErrorCode.InvalidInput);
        }

        cancellationToken.ThrowIfCancellationRequested();
        installProgress?.Report(new PluginPackageInstallProgress(
            PluginPackageInstallStage.Verifying,
            80,
            CanCancel: true));
        var validation = PluginPackageValidator.Validate(archive.Value, hostVersion, trustVerifier);
        if (!validation.IsSuccess)
        {
            auditCoordinator.AppendInstallRejected(
                actorId,
                archive.Value.Manifest.PluginId,
                validation.Reasons,
                correlationId);
            diagnostics?.RecordInstallRejected(archive.Value.Manifest.PluginId, validation.Reasons);
            return Result.Failure<InstalledPluginState>(ErrorCode.ValidationFailed);
        }

        var descriptor = validation.Descriptor!;
        PluginInstallConcurrencyGate.Lease installLease;
        try
        {
            installLease = installConcurrency.Enter(descriptor.Manifest.PluginId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Cancelled<InstalledPluginState>();
        }

        using (installLease)
        {
        var existing = stateStore.Get(descriptor.Manifest.PluginId);
        if (!existing.IsSuccess)
        {
            var reasons = new[]
            {
                new PluginPackageValidationReason(
                    "install-state-unavailable",
                    "Plugin installation state could not be verified.")
            };
            auditCoordinator.AppendInstallRejected(
                actorId,
                descriptor.Manifest.PluginId,
                reasons,
                correlationId);
            diagnostics?.RecordInstallRejected(descriptor.Manifest.PluginId, reasons);
            return Result.Failure<InstalledPluginState>(existing.ErrorCode ?? ErrorCode.DatabaseUnavailable);
        }

        if (existing.Value is not null &&
            existing.Value.PackageFingerprint.Equals(descriptor.Fingerprint, StringComparison.Ordinal))
        {
            return Result.Success(existing.Value);
        }

        var mutationAllowed = runtimeModeMutationGate.EnsurePackageInstallAllowed(
            descriptor.Manifest.PluginId,
            isUpdate: existing.Value is not null);
        if (!mutationAllowed.IsSuccess)
        {
            var rejection = RuntimeModeFailureRejection(mutationAllowed.ErrorCode);
            var reasons = new[]
            {
                new PluginPackageValidationReason(rejection.ReasonCode, rejection.Message)
            };
            auditCoordinator.AppendInstallRejected(
                actorId,
                descriptor.Manifest.PluginId,
                reasons,
                correlationId);
            diagnostics?.RecordInstallRejected(descriptor.Manifest.PluginId, reasons);
            return Result.Failure<InstalledPluginState>(rejection.ErrorCode);
        }

        cancellationToken.ThrowIfCancellationRequested();
        installProgress?.Report(new PluginPackageInstallProgress(
            PluginPackageInstallStage.Finishing,
            90,
            CanCancel: false));
        var install = new PluginPackageInstaller(
            stagingArea,
            stateStore,
            metadataCache,
            stateCommitter).Install(
                descriptor,
                stateCommitter is null
                    ? null
                    : new PluginInstallCommitContext(actorId, correlationId),
                restartRequired: existing.Value is not null &&
                    currentProcessActivationTracker?.IsActivatedInCurrentProcess(descriptor.Manifest.PluginId) == true);
        if (!install.IsSuccess)
        {
            var reasons = new[]
            {
                new PluginPackageValidationReason("install-failed", "Plugin package installation failed.")
            };
            auditCoordinator.AppendInstallRejected(
                actorId,
                descriptor.Manifest.PluginId,
                reasons,
                correlationId);
            diagnostics?.RecordInstallRejected(descriptor.Manifest.PluginId, reasons);
            return install;
        }

        if (stateCommitter is null)
        {
            auditCoordinator.AppendInstallVerified(actorId, descriptor, correlationId);
        }

        installProgress?.Report(new PluginPackageInstallProgress(
            PluginPackageInstallStage.Finishing,
            100,
            CanCancel: false));
        return install;
        }
    }

    private static IProgress<PluginPackageReadProgress>? CreatePackageReadProgress(
        IProgress<PluginPackageReadProgress>? readProgress,
        IProgress<PluginPackageInstallProgress>? installProgress)
    {
        if (readProgress is null && installProgress is null)
        {
            return null;
        }

        return new PackageReadProgressAdapter(readProgress, installProgress);
    }

    private sealed class PackageReadProgressAdapter(
        IProgress<PluginPackageReadProgress>? readProgress,
        IProgress<PluginPackageInstallProgress>? installProgress)
        : IProgress<PluginPackageReadProgress>
    {
        private int lastPercent;

        public void Report(PluginPackageReadProgress value)
        {
            readProgress?.Report(value);
            var ratio = value.TotalUncompressedBytes > 0
                ? (double)value.UncompressedBytesRead / value.TotalUncompressedBytes
                : value.TotalEntries > 0
                    ? (double)value.EntriesCompleted / value.TotalEntries
                    : 0;
            var percent = Math.Clamp(5 + (int)Math.Floor(ratio * 70), 5, 75);
            percent = Math.Max(percent, Volatile.Read(ref lastPercent));
            Volatile.Write(ref lastPercent, percent);
            installProgress?.Report(new PluginPackageInstallProgress(
                PluginPackageInstallStage.Reading,
                percent,
                CanCancel: true));
        }
    }

    private static bool IsSupportedSourcePackage(string packagePath)
    {
        return File.Exists(packagePath) &&
            Path.GetExtension(packagePath).Equals(".dobbyplugin", StringComparison.OrdinalIgnoreCase);
    }

    private Result Authorize(string actorId, CorrelationId correlationId)
    {
        var operationKey = new OperationKey(BuiltInOperationTargets.PluginManagementTargetId, OperationVerb.Import).ToString();
        var decision = authorization.Evaluate(
            new OperationPermissionCapability(operationKey),
            new PolicyContext(actorId, null, correlationId));
        if (!decision.IsSuccess)
        {
            return Result.Failure(decision.ErrorCode ?? ErrorCode.Forbidden);
        }

        return decision.Value.Allowed
            ? Result.Success()
            : Result.Failure(ErrorCode.Forbidden);
    }

    private static RuntimeModeFailure RuntimeModeFailureRejection(ErrorCode? errorCode)
    {
        if (errorCode == ErrorCode.LicenseReadOnly)
        {
            return new RuntimeModeFailure(
                ErrorCode.LicenseReadOnly,
                "license-read-only",
                "Plugin package installation is unavailable in read-only license mode.");
        }

        return errorCode == ErrorCode.LicenseBindingMismatch
            ? new RuntimeModeFailure(
                ErrorCode.LicenseBindingMismatch,
                "license-binding-mismatch",
                "Plugin package installation is unavailable because license binding could not be verified.")
            : new RuntimeModeFailure(
                ErrorCode.LicenseDenied,
                "license-runtime-unavailable",
                "Plugin package installation is unavailable because license runtime mode could not be verified.");
    }

    private sealed record RuntimeModeFailure(
        ErrorCode ErrorCode,
        string ReasonCode,
        string Message);
}
