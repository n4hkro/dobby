using Dobby.Core;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginFramework;
using Dobby.Contracts.PluginPages;
using Dobby.Core.Versioning;
using Dobby.Infrastructure.Persistence;
using Dobby.Licensing;
using Dobby.Navigation;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authorization;
using Dobby.Security.Trust;
using System.Reflection;
using System.IO;

namespace Dobby.Host.Plugins;

public sealed class HostPluginRuntimeCoordinator(
    IHostPersistenceMigrator migrator,
    IInstalledPluginStateStore stateStore,
    IInstalledPluginMetadataCache metadataCache,
    IPluginInstallStagingArea stagingArea,
    OperationTargetRegistry operationTargetRegistry,
    PluginExtensionPointRegistry extensionPointRegistry,
    PluginLifetimeManager lifetimeManager,
    HostPluginActivationCancellation activationCancellation,
    PostLoginPluginActivator postLoginPluginActivator,
    IPluginTrustVerifier pluginTrustVerifier,
    PluginRuntimeAuditCoordinator? auditCoordinator = null,
    IBackgroundOperationReporter? backgroundOperations = null,
    PluginRuntimeDiagnosticsCoordinator? diagnosticsCoordinator = null,
    ILicenseRuntimeModeProvider? licenseRuntimeModeProvider = null)
{
    private readonly object activationGate = new();
    private Task<Result<PluginActivationExecutionOutcome>>? activationTask;
    private bool disposing;

    public IInstalledPluginStateStore StateStore { get; } = stateStore;

    public IInstalledPluginMetadataCache MetadataCache { get; } = metadataCache;

    public IPluginInstallStagingArea StagingArea { get; } = stagingArea;

    public OperationTargetRegistry OperationTargetRegistry { get; } = operationTargetRegistry;

    public PluginExtensionPointRegistry ExtensionPointRegistry { get; } = extensionPointRegistry;

    public PluginLifetimeManager LifetimeManager { get; } = lifetimeManager;

    public HostPluginActivationCancellation ActivationCancellation { get; } = activationCancellation;

    public PostLoginPluginActivator PostLoginPluginActivator { get; } = postLoginPluginActivator;

    public PluginRuntimeAuditCoordinator? AuditCoordinator { get; } = auditCoordinator;

    public IBackgroundOperationReporter? BackgroundOperations { get; } = backgroundOperations;

    public PluginRuntimeDiagnosticsCoordinator? DiagnosticsCoordinator { get; } = diagnosticsCoordinator;

    public IPluginTrustVerifier TrustVerifier { get; } = pluginTrustVerifier ?? throw new ArgumentNullException(nameof(pluginTrustVerifier));

    public ILicenseRuntimeModeProvider LicenseRuntimeModeProvider { get; } =
        licenseRuntimeModeProvider ?? new AllowAllLicenseRuntimeModeProvider();

    public PersistenceResult ApplyPostLoginMigrations()
    {
        var migration = migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All);
        if (!migration.IsSuccess)
        {
            return migration;
        }

        var recovery = StagingArea.Recover();
        if (!recovery.IsSuccess)
        {
            return PersistenceResult.Failure(PersistenceError.Create(
                PersistenceError.IoFailed,
                "Post-login plugin staging recovery failed."));
        }

        var snapshot = StateStore.Snapshot();
        if (!snapshot.IsSuccess)
        {
            return PersistenceResult.Failure(ToPersistenceError(
                snapshot.ErrorCode,
                "Installed plugin state snapshot could not be read during post-login recovery."));
        }

        var pendingStates = snapshot.Value.Where(state => state.IsPendingUninstall).ToArray();
        foreach (var state in pendingStates)
        {
            var pendingCleanup = StagingArea.Cleanup(state.RuntimePath);
            if (!pendingCleanup.IsSuccess)
            {
                return PersistenceResult.Failure(ToPersistenceError(
                    pendingCleanup.ErrorCode,
                    "Pending uninstall plugin runtime cleanup failed during post-login recovery."));
            }

            var stateRemove = StateStore.Remove(state.PluginId);
            if (!stateRemove.IsSuccess)
            {
                return PersistenceResult.Failure(ToPersistenceError(
                    stateRemove.ErrorCode,
                    "Pending uninstall plugin state could not be removed during post-login recovery."));
            }

            var cacheRemove = MetadataCache.Remove(state.PluginId);
            if (!cacheRemove.IsSuccess)
            {
                return PersistenceResult.Failure(ToPersistenceError(
                    cacheRemove.ErrorCode,
                "Pending uninstall plugin metadata cache could not be removed during post-login recovery."));
            }
        }

        var activeStates = pendingStates.Length == 0
            ? snapshot.Value
            : snapshot.Value.Where(state => !state.IsPendingUninstall).ToArray();

        var orphanCleanup = StagingArea.CleanupRuntimeOrphans(
            activeStates.Select(state => state.RuntimePath).ToArray());
        if (!orphanCleanup.IsSuccess)
        {
            return PersistenceResult.Failure(ToPersistenceError(
                orphanCleanup.ErrorCode,
                "Installed plugin runtime orphan cleanup failed during post-login recovery."));
        }

        var cacheRebuild = MetadataCache.RebuildFrom(activeStates);
        if (!cacheRebuild.IsSuccess)
        {
            return PersistenceResult.Failure(ToPersistenceError(
                cacheRebuild.ErrorCode,
                "Installed plugin metadata cache rebuild failed during post-login recovery."));
        }

        ActivationCancellation.Reset();
        return PersistenceResult.Success();
    }

    public Task<Result<PluginActivationExecutionOutcome>> BeginPostLoginActivation(
        HostVersion hostVersion,
        string actorId)
    {
        var operationId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        lock (activationGate)
        {
            if (disposing)
            {
                return Task.FromResult(Result.Failure<PluginActivationExecutionOutcome>(ErrorCode.Cancelled));
            }

            if (activationTask is not null)
            {
                return activationTask;
            }

            ReportActivationProgress(
                operationId,
                correlationId,
                BackgroundOperationStatus.Running,
                BackgroundOperationProgress.Indeterminate(),
                null);

            activationTask = Task.Run(async () => await ActivatePostLoginAsync(
                hostVersion,
                actorId,
                operationId,
                correlationId).ConfigureAwait(false));
            return activationTask;
        }
    }

    private async Task<Result<PluginActivationExecutionOutcome>> ActivatePostLoginAsync(
        HostVersion hostVersion,
        string actorId,
        Guid operationId,
        Guid correlationId)
    {
        var result = await ActivateInstalledPluginsAsync(hostVersion, ActivationCancellation.Token).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            DiagnosticsCoordinator?.RecordActivationOutcomes(result.Value.Run.Outcomes);
            if (result.Value.Completion == PluginActivationCompletion.Completed)
            {
                AuditCoordinator?.AppendActivationOutcomes(actorId, result.Value.Run.Outcomes, CorrelationId.New());
                ReportActivationProgress(
                    operationId,
                    correlationId,
                    BackgroundOperationStatus.Completed,
                    BackgroundOperationProgress.Determinate(100),
                    null);
            }
            else
            {
                DiagnosticsCoordinator?.RecordActivationCancelled(result.Value.Run);
                ReportActivationProgress(
                    operationId,
                    correlationId,
                    BackgroundOperationStatus.Cancelled,
                    BackgroundOperationProgress.Indeterminate(),
                    ErrorCode.Cancelled);
            }
        }
        else if (result.ErrorCode == ErrorCode.Cancelled)
        {
            DiagnosticsCoordinator?.RecordActivationCancelled(new PluginActivationRun([], [], []));
            ReportActivationProgress(
                operationId,
                correlationId,
                BackgroundOperationStatus.Cancelled,
                BackgroundOperationProgress.Indeterminate(),
                result.ErrorCode);
        }
        else
        {
            DiagnosticsCoordinator?.RecordRuntimeFailure(result.ErrorCode?.ToString() ?? "unknown");
            ReportActivationProgress(
                operationId,
                correlationId,
                BackgroundOperationStatus.Failed,
                BackgroundOperationProgress.Indeterminate(),
                result.ErrorCode);
        }

        return result;
    }

    public Result<PluginActivationRun> ActivateInstalledPlugins(
        HostVersion hostVersion,
        CancellationToken cancellationToken)
    {
        var result = ActivateInstalledPluginsAsync(hostVersion, cancellationToken)
            .GetAwaiter()
            .GetResult();

        if (!result.IsSuccess)
        {
            return Result.Failure<PluginActivationRun>(result.ErrorCode ?? ErrorCode.DatabaseUnavailable);
        }

        return result.Value.Completion == PluginActivationCompletion.Completed
            ? Result.Success(result.Value.Run)
            : Result.Failure<PluginActivationRun>(ErrorCode.Cancelled);
    }

    public async Task<Result<PluginActivationExecutionOutcome>> ActivateInstalledPluginsAsync(
        HostVersion hostVersion,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Success(new PluginActivationExecutionOutcome(
                PluginActivationCompletion.Cancelled,
                new PluginActivationRun([], [], [])));
        }

        var snapshot = StateStore.Snapshot();
        if (!snapshot.IsSuccess)
        {
            return Result.Failure<PluginActivationExecutionOutcome>(snapshot.ErrorCode ?? ErrorCode.DatabaseUnavailable);
        }

        var candidates = new List<PluginActivationCandidate>();
        var candidateManifests = new Dictionary<string, PluginManifest>(StringComparer.Ordinal);
        var preActivationOutcomes = new Dictionary<string, PluginActivationOutcome>(StringComparer.Ordinal);
        foreach (var state in snapshot.Value.OrderBy(item => item.PluginId, StringComparer.Ordinal))
        {
            if (state.IsPendingUninstall)
            {
                continue;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Result.Success(new PluginActivationExecutionOutcome(
                    PluginActivationCompletion.Cancelled,
                    BuildRun(
                        preActivationOutcomes,
                        [],
                        candidateManifests)));
            }

            var archive = PluginPackageReader.Read(state.RuntimePath);
            if (!archive.IsSuccess)
            {
                preActivationOutcomes[state.PluginId] = new PluginActivationOutcome(
                    state.PluginId,
                    PluginActivationStatus.Faulted,
                    "installed-manifest-unreadable");
                continue;
            }

            var verification = VerifyInstalledPackage(archive.Value, state, hostVersion);
            if (!verification.IsSuccess)
            {
                preActivationOutcomes[state.PluginId] = new PluginActivationOutcome(
                    state.PluginId,
                    PluginActivationStatus.Rejected,
                    verification.Reason);
                continue;
            }

            var descriptor = verification.Descriptor!;
            var compatibility = PluginCompatibilityEvaluator.Evaluate(descriptor.Manifest, hostVersion);
            if (!compatibility.IsCompatible)
            {
                preActivationOutcomes[state.PluginId] = new PluginActivationOutcome(
                    state.PluginId,
                    compatibility.Status,
                    compatibility.Reason);
                continue;
            }

            var licenseMode = LicenseRuntimeModeProvider.GetCurrentMode(state.PluginId, descriptor.Fingerprint);
            if (!licenseMode.IsSuccess)
            {
                preActivationOutcomes[state.PluginId] = new PluginActivationOutcome(
                    state.PluginId,
                    PluginActivationStatus.Rejected,
                    ToLicenseRejectionReason(licenseMode.ErrorCode));
                continue;
            }

            if (licenseMode.Value == PluginRuntimeMode.Denied)
            {
                preActivationOutcomes[state.PluginId] = new PluginActivationOutcome(
                    state.PluginId,
                    PluginActivationStatus.Rejected,
                    "license-denied");
                continue;
            }

            if (state.IsEnabled)
            {
                var contributions = PluginOperationTargetContributionBuilder.Build(descriptor.Manifest, hostVersion);
                if (!contributions.IsSuccess)
                {
                    preActivationOutcomes[state.PluginId] = new PluginActivationOutcome(
                        state.PluginId,
                        PluginActivationStatus.Rejected,
                        contributions.Reasons.FirstOrDefault()?.Code ?? "invalid-manifest");
                    continue;
                }

                var seed = SeedOperationTargets(contributions.Contributions);
                if (!seed.IsSuccess)
                {
                    preActivationOutcomes[state.PluginId] = new PluginActivationOutcome(
                        state.PluginId,
                        PluginActivationStatus.Rejected,
                        $"operation-target-seeding-failed: {seed.ErrorCode}");
                    continue;
                }
            }

            var manifest = descriptor.Manifest;
            candidateManifests[state.PluginId] = manifest;
            candidates.Add(new PluginActivationCandidate(
                state.PluginId,
                state.Version,
                state.IsEnabled,
                manifest.DependsOn
                    .Select(dependency => new PluginDependency(dependency.PluginId, dependency.MinVersion))
                    .ToArray(),
                token => CreateModule(state.PluginId, state.RuntimePath, manifest.EntryAssembly, token)));
        }

        var activation = await PostLoginPluginActivator.ActivateAsync(candidates, hostVersion, cancellationToken).ConfigureAwait(false);
        if (!activation.IsSuccess)
        {
            return Result.Failure<PluginActivationExecutionOutcome>(activation.ErrorCode ?? ErrorCode.PluginInternalError);
        }

        var outcomes = preActivationOutcomes.Values
            .Concat(activation.Value.Run.Outcomes)
            .OrderBy(outcome => outcome.PluginId, StringComparer.Ordinal)
            .ToArray();
        var navigationContributions = BuildNavigationContributions(candidateManifests, activation.Value.Run.ActivatedPluginIds);
        return Result.Success(new PluginActivationExecutionOutcome(
            activation.Value.Completion,
            new PluginActivationRun(outcomes, activation.Value.Run.ActivatedPluginIds, navigationContributions)));
    }

    public async ValueTask DisposeAsync()
    {
        Task<Result<PluginActivationExecutionOutcome>>? activation;
        lock (activationGate)
        {
            disposing = true;
            activation = activationTask;
        }

        ActivationCancellation.CancelPostLoginActivation();

        if (activation is not null)
        {
            try
            {
                await activation.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        await PostLoginPluginActivator.DisposeAsync().ConfigureAwait(false);
        await LifetimeManager.DisposeAsync().ConfigureAwait(false);
        ActivationCancellation.Dispose();
    }

    private InstalledPackageVerification VerifyInstalledPackage(
        PluginPackageArchive archive,
        InstalledPluginState state,
        HostVersion hostVersion)
    {
        var validation = PluginPackageValidator.Validate(archive, hostVersion, TrustVerifier);
        if (!validation.IsSuccess)
        {
            return InstalledPackageVerification.Failure(
                validation.Reasons.FirstOrDefault()?.Code ?? "installed-package-validation-failed");
        }

        var descriptor = validation.Descriptor!;
        if (!descriptor.Manifest.PluginId.Equals(state.PluginId, StringComparison.Ordinal))
        {
            return InstalledPackageVerification.Failure("package-plugin-id-mismatch");
        }

        if (!descriptor.Fingerprint.Equals(state.PackageFingerprint, StringComparison.Ordinal))
        {
            return InstalledPackageVerification.Failure("package-fingerprint-mismatch");
        }

        return InstalledPackageVerification.Success(descriptor);
    }

    public Result SeedOperationTargets(IEnumerable<PluginOperationTargetContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        try
        {
            foreach (var contribution in contributions)
            {
                var register = OperationTargetRegistry.RegisterPluginTarget(
                    contribution.TargetId,
                    contribution.Verbs.ToArray(),
                    contribution.LabelKey);
                if (!register.IsSuccess)
                {
                    return register;
                }
            }

            return Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.PluginInternalError);
        }
    }

    private void ReportActivationProgress(
        Guid operationId,
        Guid correlationId,
        BackgroundOperationStatus status,
        BackgroundOperationProgress progress,
        ErrorCode? failureReason)
    {
        BackgroundOperations?.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Host,
            OwnerId: null,
            "Dobby.Plugin.Activation",
            ShortDetailKey: null,
            status,
            progress,
            CanCancel: false,
            correlationId,
            failureReason));
    }

    private static Result<IPluginModule> CreateModule(
        string pluginId,
        string runtimePath,
        string entryAssembly,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Cancelled<IPluginModule>();
        }

        try
        {
            var runtimeRoot = Path.GetFullPath(runtimePath);
            var entryPath = Path.GetFullPath(Path.Combine(
                runtimeRoot,
                entryAssembly.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsUnderRoot(runtimeRoot, entryPath) || !File.Exists(entryPath))
            {
                return Result.Failure<IPluginModule>(ErrorCode.NotFound);
            }

            var context = new PluginLoadContext(pluginId, entryPath);
            var assembly = context.LoadFromAssemblyPath(entryPath);
            var moduleTypes = assembly
                .GetTypes()
                .Where(type =>
                    typeof(IPluginModule).IsAssignableFrom(type) &&
                    type is { IsClass: true, IsAbstract: false } &&
                    type.GetConstructor(Type.EmptyTypes) is not null)
                .ToArray();
            if (moduleTypes.Length != 1)
            {
                return Result.Failure<IPluginModule>(ErrorCode.ValidationFailed);
            }

            var module = Activator.CreateInstance(moduleTypes[0]) as IPluginModule;
            return module is null
                ? Result.Failure<IPluginModule>(ErrorCode.PluginInternalError)
                : Result.Success(module);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure<IPluginModule>(ErrorCode.PluginInternalError);
        }
    }

    private static IReadOnlyList<PluginNavigationContribution> BuildNavigationContributions(
        IReadOnlyDictionary<string, PluginManifest> manifests,
        IReadOnlyList<string> activatedPluginIds)
    {
        var activated = activatedPluginIds.ToHashSet(StringComparer.Ordinal);
        var contributions = new List<PluginNavigationContribution>();
        foreach (var pluginId in activatedPluginIds.Order(StringComparer.Ordinal))
        {
            if (!activated.Contains(pluginId) || !manifests.TryGetValue(pluginId, out var manifest))
            {
                continue;
            }

            for (var index = 0; index < manifest.ExtensionPoints.Count; index++)
            {
                var extension = manifest.ExtensionPoints[index];
                if (extension.Target.Equals("plugin-page-region", StringComparison.Ordinal))
                {
                    continue;
                }

                contributions.Add(new PluginNavigationContribution(
                    pluginId,
                    extension.Id,
                    PluginRouteNormalizer.Normalize(pluginId, extension.RouteKey),
                    extension.LabelKey ?? (extension.Target.Equals("top-toolbar", StringComparison.Ordinal) && extension.ParentId is null
                        ? manifest.DisplayName
                        : extension.RouteKey),
                    extension.Target,
                    extension.ParentId,
                    extension.Group,
                    extension.Weight,
                    index));
            }
        }

        return contributions;
    }

    private static PluginActivationRun BuildRun(
        IReadOnlyDictionary<string, PluginActivationOutcome> outcomes,
        IReadOnlyList<string> activatedPluginIds,
        IReadOnlyDictionary<string, PluginManifest> manifests)
    {
        return new PluginActivationRun(
            outcomes.Values.OrderBy(outcome => outcome.PluginId, StringComparer.Ordinal).ToArray(),
            activatedPluginIds.ToArray(),
            BuildNavigationContributions(manifests, activatedPluginIds));
    }

    private static bool IsUnderRoot(string root, string candidate)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static PersistenceError ToPersistenceError(ErrorCode? errorCode, string message)
    {
        var code = errorCode switch
        {
            { } value when value == ErrorCode.IoFailed => PersistenceError.IoFailed,
            { } value when value == ErrorCode.SerializationFailed => PersistenceError.SerializationFailed,
            { } value when value == ErrorCode.DatabaseUnavailable => PersistenceError.QueryFailed,
            _ => PersistenceError.Unknown
        };

        return PersistenceError.Create(code, message);
    }

    private static string ToLicenseRejectionReason(ErrorCode? errorCode)
    {
        return errorCode == ErrorCode.LicenseBindingMismatch
            ? "license-binding-mismatch"
            : "license-denied";
    }

    private sealed class AllowAllLicenseRuntimeModeProvider : ILicenseRuntimeModeProvider
    {
        public Result<PluginRuntimeMode> GetCurrentMode(string pluginId, string packageFingerprint)
        {
            return Result.Success(PluginRuntimeMode.FullAccess);
        }
    }

    private sealed record InstalledPackageVerification(
        bool IsSuccess,
        PluginPackageDescriptor? Descriptor,
        string Reason)
    {
        public static InstalledPackageVerification Success(PluginPackageDescriptor descriptor)
        {
            return new InstalledPackageVerification(true, descriptor, string.Empty);
        }

        public static InstalledPackageVerification Failure(string reason)
        {
            return new InstalledPackageVerification(false, null, reason);
        }
    }
}
