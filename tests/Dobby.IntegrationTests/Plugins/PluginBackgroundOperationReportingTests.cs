using Dobby.Application;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Host;
using Dobby.Host.Plugins;
using Dobby.Infrastructure.Persistence;
using Dobby.Localization;
using Dobby.Messaging;
using Dobby.Messaging.UiDispatch;
using Dobby.Navigation;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authorization;
using Dobby.Security.Trust;
using Dobby.Shell;
using NSubstitute;
using Shouldly;

namespace Dobby.IntegrationTests.Plugins;

public sealed class PluginBackgroundOperationReportingTests
{
    [Fact]
    public async Task Plugin_background_operation_updates_shell_status_surface_via_shared_reporter()
    {
        using var fixture = SamplePluginPackageFixture.CreateRuntimeDirectory();
        await using var lifetimes = new PluginLifetimeManager();
        var extensionRegistry = new PluginExtensionPointRegistry();
        var archive = PluginPackageReader.Read(fixture.PackagePath);
        archive.IsSuccess.ShouldBeTrue();
        var packageFingerprint = PluginPackageFingerprint.Compute(archive.Value.Manifest).Fingerprint;
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>(
        [
            new InstalledPluginState(
                SamplePluginPackageFixture.PluginId,
                SamplePluginPackageFixture.Version,
                packageFingerprint,
                "Sample Plugin",
                "Sample plugin description",
                IsEnabled: true,
                ValidationSummary: "verified",
                RuntimePath: fixture.PackagePath,
                StatusSummary: "Enabled",
                InstalledAtUtc: DateTimeOffset.UtcNow,
                UpdatedAtUtc: DateTimeOffset.UtcNow)
        ]));
        var reporter = new BackgroundOperationReporter();
        var snapshots = new List<BackgroundOperationSnapshot>();
        reporter.SnapshotReported += snapshots.Add;
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        var dispatcher = new RecordingUiDispatcher();
        var shellBackgrounds = new ShellBackgroundOperationCoordinator(shell, reporter, dispatcher);
        shellBackgrounds.Attach();

        var activationCancellation = new HostPluginActivationCancellation();
        var operationTargets = OperationTargetRegistry.CreateDefault();
        var grants = new MutableGrantReader(new UserPermissionState("managed", IsOwner: false, IsActive: true));
        grants.Grant("managed", new OperationKey("com.dobby.sample.greet", OperationVerb.Execute));
        var operationAuthorization = new OperationAuthorizationPolicy(operationTargets, grants);
        var pluginHostServices = new PluginHostServices(
            new InMemoryEventBus(),
            reporter,
            activationCancellation,
            operationAuthorizationPolicy: operationAuthorization,
            pluginWorkSessions: new NullPluginWorkSessionService());
        await using var runtime = new HostPluginRuntimeCoordinator(
            Substitute.For<IHostPersistenceMigrator>(),
            stateStore,
            Substitute.For<IInstalledPluginMetadataCache>(),
            CreateSuccessfulActivationStatusPersistence(),
            Substitute.For<IPluginInstallStagingArea>(),
            Substitute.For<IPluginDataRetentionStore>(),
            operationTargets,
            extensionRegistry,
            lifetimes,
            activationCancellation,
            new PostLoginPluginActivator(new PluginServiceRegistrationBridge(extensionRegistry, lifetimes, pluginHostServices)),
            Substitute.For<IPluginCurrentProcessActivationTracker>(),
            new AllowAllTrustVerifier(),
            backgroundOperations: reporter);

        var activation = await runtime.BeginPostLoginActivation(new HostVersion(1, 0, 0), "managed");
        activation.IsSuccess.ShouldBeTrue(activation.IsSuccess ? null : activation.ErrorCode.ToString());

        shell.ApplyPluginNavigationContributions(
            PluginNavigationContributionMapper.Map(activation.Value.Run.NavigationContributions));
        var pageCoordinator = new PluginPageRouteCoordinator(extensionRegistry, shell, () => "managed");
        var route = await pageCoordinator.ActivateRouteAsync("plugin/com.dobby.sample/greet", CancellationToken.None);
        route.IsSuccess.ShouldBeTrue(route.IsSuccess ? null : route.ErrorCode.ToString());
        dispatcher.RunPostedActions();

        var pageModel = shell.ActivePluginPageModel.ShouldBeOfType<PluginActionPageModel>();
        var progress = await pageModel.Actions.Single(action => action.ActionId == "sample.progress")
            .ExecuteAsync(pageModel.Context, CancellationToken.None);

        progress.IsSuccess.ShouldBeTrue(progress.IsSuccess ? null : progress.ErrorCode.ToString());
        snapshots.ShouldContain(snapshot =>
            snapshot.OwnerKind == BackgroundOperationOwnerKind.Plugin &&
            snapshot.OwnerId == SamplePluginPackageFixture.PluginId &&
            snapshot.Status == BackgroundOperationStatus.Running &&
            snapshot.Progress.Percent == 50);
        snapshots.ShouldContain(snapshot =>
            snapshot.OwnerKind == BackgroundOperationOwnerKind.Plugin &&
            snapshot.OwnerId == SamplePluginPackageFixture.PluginId &&
            snapshot.Status == BackgroundOperationStatus.Completed &&
            snapshot.Progress.Percent == 100);

        dispatcher.RunNextPostedAction();
        shell.BackgroundOperationText.ShouldContain("sample.greet.progress");
        shell.BackgroundOperationPercent.ShouldBe(50);
        shell.BackgroundOperationIsIndeterminate.ShouldBeFalse();

        dispatcher.RunNextPostedAction();

        shell.BackgroundOperationText.ShouldContain("sample.greet.progress");
        shell.BackgroundOperationPercent.ShouldBe(100);
        shell.BackgroundOperationIsIndeterminate.ShouldBeFalse();
    }

    private sealed class RecordingUiDispatcher : IUiDispatcher
    {
        public bool IsOnUiThread => false;

        public List<Action> PostedActions { get; } = [];

        public Task InvokeAsync(Func<Task> action)
        {
            Post(() => action().GetAwaiter().GetResult());
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<Task<T>> action)
        {
            Post(() => action().GetAwaiter().GetResult());
            return Task.FromResult(default(T)!);
        }

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            PostedActions.Add(action);
        }

        public void RunPostedActions()
        {
            var actions = PostedActions.ToArray();
            PostedActions.Clear();
            foreach (var action in actions)
            {
                action();
            }
        }

        public void RunNextPostedAction()
        {
            PostedActions.Count.ShouldBeGreaterThan(0);
            var action = PostedActions[0];
            PostedActions.RemoveAt(0);
            action();
        }
    }

    private static IHostPluginActivationStatusPersistence CreateSuccessfulActivationStatusPersistence()
    {
        var persistence = Substitute.For<IHostPluginActivationStatusPersistence>();
        persistence
            .Persist(Arg.Any<IReadOnlyList<InstalledPluginState>>(), Arg.Any<IReadOnlyList<InstalledPluginState>>())
            .Returns(Result.Success());
        return persistence;
    }

    private sealed class AllowAllTrustVerifier : IPluginTrustVerifier
    {
        public PluginTrustVerificationResult Verify(PluginTrustPayload payload) => PluginTrustVerificationResult.Success();
    }

    private sealed class MutableGrantReader(UserPermissionState state) : IPermissionGrantReader
    {
        private readonly Dictionary<string, HashSet<OperationKey>> grants = new(StringComparer.Ordinal)
        {
            [state.UserId] = []
        };

        public void Grant(string userId, OperationKey key)
        {
            if (!grants.TryGetValue(userId, out var entries))
            {
                entries = [];
                grants[userId] = entries;
            }

            entries.Add(key);
        }

        public Result<UserPermissionState> LoadUserState(string userId)
        {
            return Result.Success(new UserPermissionState(
                userId,
                IsOwner: false,
                IsActive: true));
        }

        public Result<bool> HasGrant(string userId, OperationKey key)
        {
            return Result.Success(
                grants.TryGetValue(userId, out var entries) &&
                entries.Contains(key));
        }
    }

    private sealed class NullPluginWorkSessionService : IPluginWorkSessionService
    {
        public Result<PluginWorkSessionHandle> Open(string ownerId, string routeKey, string labelKey)
            => Result.Success(new PluginWorkSessionHandle(Guid.NewGuid(), ownerId, routeKey, labelKey));

        public Result UpdateProgress(Guid sessionId, int percent) => Result.Success();

        public Result RecordResult(Guid sessionId, string resultSummaryKey) => Result.Success();

        public Result RecordError(Guid sessionId, string errorSummaryKey) => Result.Success();

        public Result Close(Guid sessionId) => Result.Success();
    }
}
