using System.Diagnostics;
using Dobby.Audit;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.PluginFramework.Activation;
using Dobby.Security.Authorization;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class HostPluginCommandDispatcherTests
{
    [Fact]
    public void Fault_policy_counts_only_plugin_defects_and_success_resets_the_streak()
    {
        var policy = new PluginRuntimeFaultPolicy(quarantineThreshold: 3);

        policy.Observe("com.dobby.sample", PluginInvocationOutcomeKind.PluginException);
        policy.Observe("com.dobby.sample", PluginInvocationOutcomeKind.ExpectedRejection);
        policy.Observe("com.dobby.sample", PluginInvocationOutcomeKind.HostInfrastructureFailure);
        policy.Snapshot("com.dobby.sample").DefectCount.ShouldBe(1);

        policy.Observe("com.dobby.sample", PluginInvocationOutcomeKind.Success);

        policy.Snapshot("com.dobby.sample").DefectCount.ShouldBe(0);
        policy.IsQuarantined("com.dobby.sample").ShouldBeFalse();
    }

    [Fact]
    public async Task Runtime_license_policy_runs_before_authorization_and_plugin_code()
    {
        var page = new RecordingPage((_, _) =>
            ValueTask.FromResult(Result.Success(new PluginCommandResult())));
        var grants = new RecordingGrantReader(
            new UserPermissionState("managed", IsOwner: false, IsActive: true));
        var dispatcher = CreateDispatcher(
            page,
            grants,
            _ => Result.Success(PluginRuntimeMode.ReadOnlyExpired),
            out _);

        var result = await dispatcher.DispatchAsync(Request(), TimeSpan.FromSeconds(2), CancellationToken.None);

        result.ErrorCode.ShouldBe(ErrorCode.LicenseReadOnly);
        grants.LoadCalls.ShouldBe(0);
        page.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Operation_permission_policy_runs_before_plugin_code()
    {
        var page = new RecordingPage((_, _) =>
            ValueTask.FromResult(Result.Success(new PluginCommandResult())));
        var dispatcher = CreateDispatcher(
            page,
            new RecordingGrantReader(
                new UserPermissionState("managed", IsOwner: false, IsActive: true)),
            _ => Result.Success(PluginRuntimeMode.FullAccess),
            out _);

        var result = await dispatcher.DispatchAsync(Request(), TimeSpan.FromSeconds(2), CancellationToken.None);

        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        page.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Authorized_command_receives_bounded_cancellable_token_and_reports_progress()
    {
        var page = new RecordingPage((request, token) =>
        {
            token.CanBeCanceled.ShouldBeTrue();
            request.Values["name"].ShouldBe("Ada");
            return ValueTask.FromResult(Result.Success(new PluginCommandResult(
                new PluginLocalizedText("success"))));
        });
        var grants = new RecordingGrantReader(
            new UserPermissionState("managed", IsOwner: false, IsActive: true));
        grants.Grant("managed", new OperationKey("com.dobby.sample.greet", OperationVerb.Execute));
        var dispatcher = CreateDispatcher(
            page,
            grants,
            _ => Result.Success(PluginRuntimeMode.FullAccess),
            out var reporter);

        var result = await dispatcher.DispatchAsync(
            Request(new Dictionary<string, string> { ["name"] = "Ada" }),
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        page.Calls.ShouldBe(1);
        reporter.Snapshots.Select(snapshot => snapshot.Status)
            .ShouldBe([BackgroundOperationStatus.Running, BackgroundOperationStatus.Completed]);
    }

    [Fact]
    public async Task Command_timeout_is_enforced_when_plugin_ignores_cancellation()
    {
        var completion = new TaskCompletionSource<Result<PluginCommandResult>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new RecordingPage((_, _) => new ValueTask<Result<PluginCommandResult>>(completion.Task));
        var grants = OwnerGrants();
        var dispatcher = CreateDispatcher(
            page,
            grants,
            _ => Result.Success(PluginRuntimeMode.FullAccess),
            out var reporter);
        var stopwatch = Stopwatch.StartNew();

        var result = await dispatcher.DispatchAsync(Request(), TimeSpan.FromSeconds(1), CancellationToken.None);
        completion.TrySetResult(Result.Success(new PluginCommandResult()));

        result.ErrorCode.ShouldBe(ErrorCode.Timeout);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
        reporter.Snapshots.Last().FailureReason.ShouldBe(ErrorCode.Timeout);
    }

    [Fact]
    public async Task Caller_cancellation_prevents_plugin_invocation()
    {
        var page = new RecordingPage((_, _) =>
            ValueTask.FromResult(Result.Success(new PluginCommandResult())));
        var dispatcher = CreateDispatcher(
            page,
            OwnerGrants(),
            _ => Result.Success(PluginRuntimeMode.FullAccess),
            out _);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await dispatcher.DispatchAsync(
            Request(),
            TimeSpan.FromSeconds(2),
            cancellation.Token);

        result.ErrorCode.ShouldBe(ErrorCode.Cancelled);
        page.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Repeated_expected_rejections_never_increment_fault_count_or_quarantine()
    {
        var page = new RecordingPage((_, _) =>
            ValueTask.FromResult(Result.Failure<PluginCommandResult>(ErrorCode.InvalidInput)));
        var policy = new PluginRuntimeFaultPolicy(quarantineThreshold: 3);
        var lifecycle = new RecordingLifecycleControl();
        var dispatcher = CreateDispatcher(
            page,
            OwnerGrants(),
            _ => Result.Success(PluginRuntimeMode.FullAccess),
            out _,
            policy,
            lifecycle);

        for (var index = 0; index < 10; index++)
        {
            var result = await dispatcher.DispatchAsync(
                Request(),
                TimeSpan.FromSeconds(2),
                CancellationToken.None);
            result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        }

        policy.Snapshot("com.dobby.sample").DefectCount.ShouldBe(0);
        policy.IsQuarantined("com.dobby.sample").ShouldBeFalse();
        lifecycle.Calls.ShouldBe(0);
        page.Calls.ShouldBe(10);
    }

    [Fact]
    public async Task Repeated_plugin_exceptions_reach_threshold_and_quarantine_with_cleanup_once()
    {
        var page = new RecordingPage((_, _) =>
            throw new InvalidOperationException("raw plugin failure"));
        var policy = new PluginRuntimeFaultPolicy(quarantineThreshold: 3);
        var lifecycle = new RecordingLifecycleControl();
        var dispatcher = CreateDispatcher(
            page,
            OwnerGrants(),
            _ => Result.Success(PluginRuntimeMode.FullAccess),
            out _,
            policy,
            lifecycle);

        for (var index = 0; index < 3; index++)
        {
            var result = await dispatcher.DispatchAsync(
                Request(),
                TimeSpan.FromSeconds(2),
                CancellationToken.None);
            result.ErrorCode.ShouldBe(ErrorCode.PluginInternalError);
        }

        var quarantined = policy.Snapshot("com.dobby.sample");
        quarantined.DefectCount.ShouldBe(3);
        quarantined.IsQuarantined.ShouldBeTrue();
        lifecycle.Calls.ShouldBe(1);
        lifecycle.LastTokenCanBeCancelled.ShouldBeTrue();

        var blocked = await dispatcher.DispatchAsync(
            Request(),
            TimeSpan.FromSeconds(2),
            CancellationToken.None);
        blocked.ErrorCode.ShouldBe(ErrorCode.PluginInternalError);
        page.Calls.ShouldBe(3);
        lifecycle.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Malformed_success_result_is_a_contract_breach_and_counts_as_a_plugin_defect()
    {
        var page = new RecordingPage((_, _) =>
            ValueTask.FromResult(Result.Success(new PluginCommandResult(
                new PluginLocalizedText("invalid key with spaces")))));
        var policy = new PluginRuntimeFaultPolicy(quarantineThreshold: 3);
        var dispatcher = CreateDispatcher(
            page,
            OwnerGrants(),
            _ => Result.Success(PluginRuntimeMode.FullAccess),
            out _,
            policy);

        var result = await dispatcher.DispatchAsync(
            Request(),
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        result.ErrorCode.ShouldBe(ErrorCode.SerializationFailed);
        policy.Snapshot("com.dobby.sample").DefectCount.ShouldBe(1);
    }

    [Fact]
    public async Task Quarantine_transition_is_written_to_the_audit_trail_exactly_once()
    {
        // Behavior proof for the closed-registry event `plugin.runtime.quarantined`
        // (docs/specs/plugins-manager.md § audit, docs/specs/audit-and-diagnostics.md).
        // Drives a real plugin across the fault threshold through the real dispatcher.
        var page = new RecordingPage((_, _) =>
            throw new InvalidOperationException("raw plugin failure"));
        var policy = new PluginRuntimeFaultPolicy(quarantineThreshold: 3);
        var trail = new InMemoryAuditTrail();
        var dispatcher = CreateDispatcher(
            page,
            OwnerGrants(),
            _ => Result.Success(PluginRuntimeMode.FullAccess),
            out _,
            policy,
            new RecordingLifecycleControl(),
            new PluginRuntimeAuditCoordinator(trail, TimeProvider.System));

        for (var index = 0; index < 4; index++)
        {
            await dispatcher.DispatchAsync(Request(), TimeSpan.FromSeconds(2), CancellationToken.None);
        }

        var quarantineEvents = trail.Snapshot()
            .Where(entry => entry.EventName == AuditEventName.PluginRuntimeQuarantined)
            .ToList();

        // Emitted at the QuarantineStarted edge only: the fourth dispatch is rejected up front
        // and must not append a second record.
        quarantineEvents.Count.ShouldBe(1);

        var quarantine = quarantineEvents[0];
        quarantine.Result.ShouldBe(AuditResult.Failed);
        quarantine.Details.ShouldNotBeNull();
        quarantine.Details!["pluginId"].ShouldBe("com.dobby.sample");
        quarantine.Details["status"].ShouldBe("Quarantined");
        quarantine.Details["reasonCode"].ShouldBe("runtime-exception-threshold");
        quarantine.Details["defectCount"].ShouldBe("3");

        // No raw plugin exception text may leak into the audit trail (NFR-026).
        quarantine.Details.Values.ShouldAllBe(value => !value.Contains("raw plugin failure"));
    }

    [Fact]
    public async Task Quarantine_is_audited_even_when_lifecycle_cleanup_is_not_wired()
    {
        // The plugin is quarantined regardless of whether lifecycle control exists, so the
        // evidence must not depend on cleanup being wired. Guards the ordering inside
        // ObserveFaultPolicyAsync, where the cleanup path returns early when unwired.
        var page = new RecordingPage((_, _) =>
            throw new InvalidOperationException("raw plugin failure"));
        var policy = new PluginRuntimeFaultPolicy(quarantineThreshold: 3);
        var trail = new InMemoryAuditTrail();
        var dispatcher = CreateDispatcher(
            page,
            OwnerGrants(),
            _ => Result.Success(PluginRuntimeMode.FullAccess),
            out _,
            policy,
            lifecycleControl: null,
            new PluginRuntimeAuditCoordinator(trail, TimeProvider.System));

        for (var index = 0; index < 3; index++)
        {
            await dispatcher.DispatchAsync(Request(), TimeSpan.FromSeconds(2), CancellationToken.None);
        }

        policy.Snapshot("com.dobby.sample").IsQuarantined.ShouldBeTrue();
        trail.Snapshot()
            .Count(entry => entry.EventName == AuditEventName.PluginRuntimeQuarantined)
            .ShouldBe(1);
    }

    private static HostPluginCommandDispatcher CreateDispatcher(
        RecordingPage page,
        RecordingGrantReader grants,
        Func<string, Result<PluginRuntimeMode>> runtimeMode,
        out RecordingBackgroundReporter reporter,
        PluginRuntimeFaultPolicy? faultPolicy = null,
        IPluginRuntimeLifecycleControl? lifecycleControl = null,
        PluginRuntimeAuditCoordinator? auditCoordinator = null)
    {
        var extensions = new PluginExtensionPointRegistry();
        extensions.Publish(
            "com.dobby.sample",
            [new PluginPreparedExtensionPoint(typeof(IPluginPage), page)]);
        var targets = OperationTargetRegistry.CreateDefault();
        targets.RegisterPluginTarget(
            "com.dobby.sample.greet",
            [OperationVerb.Execute],
            "sample.greet").IsSuccess.ShouldBeTrue();
        reporter = new RecordingBackgroundReporter();
        return new HostPluginCommandDispatcher(
            extensions,
            new OperationAuthorizationPolicy(targets, grants),
            new HostRuntimeModeMutationGate(runtimeMode),
            reporter,
            new CancellationTokenSource().Token,
            diagnostics: null,
            faultPolicy: faultPolicy,
            lifecycleControl: lifecycleControl,
            auditCoordinator: auditCoordinator);
    }

    private static RecordingGrantReader OwnerGrants() =>
        new(new UserPermissionState("managed", IsOwner: true, IsActive: true));

    private static PluginCommandRequest Request(IReadOnlyDictionary<string, string>? values = null) =>
        new(
            "com.dobby.sample",
            "plugin/com.dobby.sample/greet",
            "managed",
            Guid.Empty.ToString(),
            "sample.submit",
            "com.dobby.sample.greet.execute",
            values ?? new Dictionary<string, string>());

    private sealed class RecordingPage(
        Func<PluginCommandRequest, CancellationToken, ValueTask<Result<PluginCommandResult>>> execute)
        : IPluginPage, IPluginPageCommandHandler
    {
        public int Calls { get; private set; }

        public PluginPageDescriptor Descriptor { get; } = new("sample.page", "greet", "sample.page");

        public ValueTask<Result<PluginCommandResult>> ExecuteCommandAsync(
            PluginCommandRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return execute(request, cancellationToken);
        }

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success<IPluginPageModel>(SamplePageModel()));

        private static PluginComponentPageModel SamplePageModel()
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Sample page",
                ["failure"] = "Sample command failed",
                ["cancelled"] = "Sample command cancelled",
                ["body"] = "Sample page body"
            };

            return new PluginComponentPageModel(
                new PluginPageCommandContext(
                    "com.dobby.sample",
                    "plugin/com.dobby.sample/greet",
                    "managed",
                    Guid.Empty.ToString()),
                new PluginLocalizedText("title"),
                new PluginLocalizedText("failure"),
                new PluginLocalizedText("cancelled"),
                new PluginLocalizationCatalog(
                    "en-US",
                    [new PluginLocalizationBundle("en-US", entries)]),
                [new PluginTextComponent("body", new PluginLocalizedText("body"))],
                []);
        }

        public ValueTask<Result> ActivateAsync(PluginPageContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> DeactivateAsync(PluginPageContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> UnloadAsync(PluginPageContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());
    }

    private sealed class RecordingGrantReader(UserPermissionState state) : IPermissionGrantReader
    {
        private readonly HashSet<string> grants = new(StringComparer.Ordinal);

        public int LoadCalls { get; private set; }

        public void Grant(string userId, OperationKey key) => grants.Add($"{userId}:{key}");

        public Result<UserPermissionState> LoadUserState(string userId)
        {
            LoadCalls++;
            return userId.Equals(state.UserId, StringComparison.Ordinal)
                ? Result.Success(state)
                : Result.Failure<UserPermissionState>(ErrorCode.NotFound);
        }

        public Result<bool> HasGrant(string userId, OperationKey key) =>
            Result.Success(grants.Contains($"{userId}:{key}"));
    }

    private sealed class RecordingBackgroundReporter : IBackgroundOperationReporter
    {
        public List<BackgroundOperationSnapshot> Snapshots { get; } = [];

        public IProgress<BackgroundOperationSnapshot> Progress => new SyncProgress(Snapshots);

        private sealed class SyncProgress(List<BackgroundOperationSnapshot> snapshots)
            : IProgress<BackgroundOperationSnapshot>
        {
            public void Report(BackgroundOperationSnapshot value) => snapshots.Add(value);
        }
    }

    private sealed class RecordingLifecycleControl : IPluginRuntimeLifecycleControl
    {
        public int Calls { get; private set; }

        public bool LastTokenCanBeCancelled { get; private set; }

        public ValueTask<Result> DeactivateAndUnloadAsync(
            string pluginId,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastTokenCanBeCancelled = cancellationToken.CanBeCanceled;
            return ValueTask.FromResult(Result.Success());
        }
    }
}
