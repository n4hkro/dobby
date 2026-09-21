using System.Reflection;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Events;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginFramework;
using Dobby.Contracts.PluginPages;
using Dobby.Contracts.PluginStorage;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Messaging;
using Dobby.PluginFramework.Activation;
using Dobby.Security.Authentication;
using Dobby.Security.Authorization;
using Dobby.UnitTests.PluginFramework.Packaging;
using NSubstitute;
using Shouldly;

namespace Dobby.UnitTests.PluginFramework.Activation;

public sealed class PluginActivationCoreTests
{
    [Fact]
    public void Compatibility_rejects_host_below_manifest_minimum_with_exact_reason()
    {
        var result = PluginCompatibilityEvaluator.Evaluate(
            PluginPackageTestData.CreateManifest() with { MinHostVersion = "2.0.0" },
            new HostVersion(1, 5, 0));

        result.IsCompatible.ShouldBeFalse();
        result.Status.ShouldBe(PluginActivationStatus.Rejected);
        result.Reason.ShouldBe("host-version-below-minimum");
    }

    [Theory]
    [InlineData("3.0", "unsupported-host-api-major")]
    [InlineData("2.1", "unsupported-host-api-minor")]
    public void Compatibility_matrix_rejects_unknown_api_major_and_future_minor(
        string hostApiVersion,
        string expectedReason)
    {
        var result = PluginCompatibilityEvaluator.Evaluate(
            PluginPackageTestData.CreateManifest() with { HostApiVersion = hostApiVersion },
            new HostVersion(2, 0, 0));

        result.IsCompatible.ShouldBeFalse();
        result.Reason.ShouldBe(expectedReason);
    }

    [Fact]
    public void Dependency_graph_orders_dependencies_and_reports_reasoned_static_skips()
    {
        var result = PluginDependencyGraph.Resolve(
        [
            new PluginDependencyCandidate("reports", "1.0.0", true, [new PluginDependency("core", "2.0.0")]),
            new PluginDependencyCandidate("core", "1.0.0", true, []),
            new PluginDependencyCandidate("missing-consumer", "1.0.0", true, [new PluginDependency("missing", "1.0.0")]),
            new PluginDependencyCandidate("independent", "1.0.0", true, [])
        ]);

        result.ActivationOrder.Select(candidate => candidate.PluginId).ShouldBe(["core", "independent"]);
        result.Outcomes.Single(outcome => outcome.PluginId == "reports").Reason
            .ShouldBe("dependency-version-below-min: core:1.0.0:2.0.0");
        result.Outcomes.Single(outcome => outcome.PluginId == "missing-consumer").Reason
            .ShouldBe("missing-dependency: missing");
    }

    [Fact]
    public void Dependency_graph_marks_cycle_members_skipped_without_blocking_independent_plugins()
    {
        var result = PluginDependencyGraph.Resolve(
        [
            new PluginDependencyCandidate("a", "1.0.0", true, [new PluginDependency("b", "1.0.0")]),
            new PluginDependencyCandidate("b", "1.0.0", true, [new PluginDependency("a", "1.0.0")]),
            new PluginDependencyCandidate("c", "1.0.0", true, [])
        ]);

        result.ActivationOrder.Select(candidate => candidate.PluginId).ShouldBe(["c"]);
        result.Outcomes.Where(outcome => outcome.Reason.StartsWith("circular-dependency:", StringComparison.Ordinal))
            .Select(outcome => outcome.PluginId)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Plugin_runtime_cleanup_closes_work_sessions_owned_by_the_plugin()
    {
        var workSessions = Substitute.For<IPluginWorkSessionService, IPluginWorkSessionCleanup>();
        var cleanup = (IPluginWorkSessionCleanup)workSessions;
        cleanup.CloseOwnedBy("com.dobby.sample").Returns(Result.Success());
        var services = new PluginHostServices(
            new InMemoryEventBus(),
            new RecordingBackgroundOperationReporter(),
            new RecordingBackgroundOperationCancellation(),
            pluginWorkSessions: workSessions);
        services.TryResolve(
            "com.dobby.sample",
            typeof(IPluginWorkSessionService),
            out _).ShouldBeTrue();

        services.CleanupPluginSubscriptions("com.dobby.sample");

        cleanup.Received(1).CloseOwnedBy("com.dobby.sample");
    }

    [Fact]
    public async Task Plugin_event_boundary_awaits_classified_outcome_sink_and_preserves_exception_origin()
    {
        var eventBus = new InMemoryEventBus();
        var outcomes = new List<(string PluginId, ErrorCode ErrorCode, PluginInvocationOutcomeKind Kind)>();
        var services = new PluginHostServices(
            eventBus,
            new RecordingBackgroundOperationReporter(),
            new RecordingBackgroundOperationCancellation(),
            pluginEventHandlerOutcomeSink: async (pluginId, _, errorCode, kind) =>
            {
                await Task.Yield();
                outcomes.Add((pluginId, errorCode, kind));
            });
        var scopedBus = services.ResolveEventBus("com.dobby.sample");
        using var subscription = scopedBus.Subscribe<SampleEvent>((_, _) =>
            throw new ArgumentException("raw plugin detail"));

        await eventBus.PublishAsync(new SampleEvent("payload")
        {
            CorrelationId = CorrelationId.New(),
            OccurredAt = DateTimeOffset.UtcNow
        });

        outcomes.ShouldBe(
        [
            ("com.dobby.sample", ErrorCode.InvalidInput, PluginInvocationOutcomeKind.PluginException)
        ]);
    }

    [Fact]
    public async Task Registration_bridge_keeps_private_services_out_of_host_registry_and_disposes_plugin_scope()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);

        var result = bridge.Register("com.dobby.sample", new TestModule());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Resolve<IPrivateService>().ShouldNotBeNull();
        registry.Get<IPluginPage>().Count.ShouldBe(1);
        registry.Get<IPrivateService>().ShouldBeEmpty();

        var page = (DisposablePage)registry.Get<IPluginPage>().Single();
        page.IsDisposed.ShouldBeFalse();

        await bridge.CleanupPluginAsync("com.dobby.sample");

        page.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Prepared_registration_is_not_visible_until_committed()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);

        var prepared = bridge.PrepareRegistration("com.dobby.sample", new TestModule());

        prepared.IsSuccess.ShouldBeTrue();
        registry.GetRegistrationsSnapshot<IPluginPage>().ShouldBeEmpty();

        var committed = await bridge.CommitPreparedAsync(prepared.Value, CancellationToken.None);

        committed.IsSuccess.ShouldBeTrue();
        registry.GetRegistrationsSnapshot<IPluginPage>().Count.ShouldBe(1);
    }

    [Fact]
    public async Task Cleanup_removes_published_registration_and_disposes_scope_once()
    {
        DisposablePage.ResetDisposeCount();
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        var prepared = bridge.PrepareRegistration("com.dobby.sample", new TestModule()).Value;
        var committed = await bridge.CommitPreparedAsync(prepared, CancellationToken.None);
        committed.IsSuccess.ShouldBeTrue();
        var page = (DisposablePage)registry.GetRegistrationsSnapshot<IPluginPage>().Single().Instance;

        await bridge.CleanupPluginAsync("com.dobby.sample");
        await bridge.CleanupPluginAsync("com.dobby.sample");

        registry.GetRegistrationsSnapshot<IPluginPage>().ShouldBeEmpty();
        page.IsDisposed.ShouldBeTrue();
        DisposablePage.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task Lifetime_manager_disposes_tracked_plugin_instances_once_in_reverse_creation_order()
    {
        var disposalOrder = new List<string>();
        await using var lifetimes = new PluginLifetimeManager();

        lifetimes.TrackSingleton("com.dobby.sample", new RecordingDisposable("first", disposalOrder));
        lifetimes.TrackSingleton("com.dobby.sample", new RecordingDisposable("second", disposalOrder));
        lifetimes.TrackSingleton("com.dobby.other", new RecordingDisposable("other", disposalOrder));

        await lifetimes.DisposePluginAsync("com.dobby.sample");
        await lifetimes.DisposePluginAsync("com.dobby.sample");

        disposalOrder.ShouldBe(["second", "first"]);

        await lifetimes.DisposeAsync();

        disposalOrder.ShouldBe(["second", "first", "other"]);
    }

    [Fact]
    public async Task Registration_bridge_resolves_only_approved_host_contract_services_for_plugin_extension_points()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var eventBus = new InMemoryEventBus();
        var reporter = new RecordingBackgroundOperationReporter();
        var cancellation = new RecordingBackgroundOperationCancellation();
        var licensing = new StaticCapabilityLicensing(PluginRuntimeMode.ReadOnlyExpired);
        var bridge = new PluginServiceRegistrationBridge(
            registry,
            lifetimes,
            new PluginHostServices(eventBus, reporter, cancellation, _ => licensing));

        var result = bridge.Register("com.dobby.sample", new HostAwareModule());

        result.IsSuccess.ShouldBeTrue();
        var page = (HostAwarePage)registry.Get<IPluginPage>().Single();
        page.EventBus.ShouldNotBeNull();
        page.EventBus.ShouldNotBeSameAs(eventBus);
        page.BackgroundOperations.ShouldBeSameAs(reporter);
        page.BackgroundOperationCancellation.ShouldBeSameAs(cancellation);
        page.Licensing.ShouldBeSameAs(licensing);
        page.Licensing.GetCurrentMode().Value.ShouldBe(PluginRuntimeMode.ReadOnlyExpired);
        var payloads = new List<string>();
        using var subscription = page.EventBus.Subscribe<SampleEvent>((evt, _) =>
        {
            payloads.Add(evt.Payload);
            return Task.CompletedTask;
        });
        await eventBus.PublishAsync(new SampleEvent("hello")
        {
            CorrelationId = CorrelationId.New(),
            OccurredAt = DateTimeOffset.UtcNow
        });
        payloads.ShouldBe(["hello"]);
    }

    [Fact]
    public async Task Registration_bridge_scopes_work_session_owner_to_resolving_plugin()
    {
        var workSessions = new RecordingPluginWorkSessionService();
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(
            new PluginExtensionPointRegistry(),
            lifetimes,
            new PluginHostServices(
                new InMemoryEventBus(),
                new RecordingBackgroundOperationReporter(),
                new RecordingBackgroundOperationCancellation(),
                pluginWorkSessions: workSessions));
        var registration = bridge.Register("com.dobby.sample", new EmptyModule());
        registration.IsSuccess.ShouldBeTrue();
        var scoped = registration.Value.Resolve<IPluginWorkSessionService>();

        var accepted = scoped.Open("com.dobby.sample", "plugin/com.dobby.sample/work", "sample.work");
        var rejected = scoped.Open("com.dobby.other", "plugin/com.dobby.other/work", "other.work");
        var missingOwner = scoped.Open(null!, "plugin/com.dobby.sample/work", "missing-owner.work");

        accepted.IsSuccess.ShouldBeTrue();
        accepted.Value.OwnerId.ShouldBe("com.dobby.sample");
        rejected.IsSuccess.ShouldBeFalse();
        rejected.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        missingOwner.IsSuccess.ShouldBeFalse();
        missingOwner.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        workSessions.OpenedOwners.ShouldBe(["com.dobby.sample"]);
    }

    [Fact]
    public async Task Registration_bridge_denies_work_session_mutation_across_plugins()
    {
        var workSessions = new TrackingPluginWorkSessionService();
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(
            new PluginExtensionPointRegistry(),
            lifetimes,
            new PluginHostServices(
                new InMemoryEventBus(),
                new RecordingBackgroundOperationReporter(),
                new RecordingBackgroundOperationCancellation(),
                pluginWorkSessions: workSessions));
        var first = bridge.Register("com.dobby.first", new EmptyModule());
        var second = bridge.Register("com.dobby.second", new EmptyModule());
        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        var firstSessions = first.Value.Resolve<IPluginWorkSessionService>();
        var secondSessions = second.Value.Resolve<IPluginWorkSessionService>();

        var opened = firstSessions.Open(
            "com.dobby.first",
            "plugin/com.dobby.first/work",
            "first.work");
        opened.IsSuccess.ShouldBeTrue();
        var victimSessionId = opened.Value.SessionId;

        secondSessions.UpdateProgress(victimSessionId, 50).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        secondSessions.RecordResult(victimSessionId, "attacker.result").ErrorCode.ShouldBe(ErrorCode.Forbidden);
        secondSessions.RecordError(victimSessionId, "attacker.error").ErrorCode.ShouldBe(ErrorCode.Forbidden);
        secondSessions.Close(victimSessionId).ErrorCode.ShouldBe(ErrorCode.Forbidden);

        workSessions.Mutations.ShouldBeEmpty();
        firstSessions.UpdateProgress(victimSessionId, 50).IsSuccess.ShouldBeTrue();
        firstSessions.Close(victimSessionId).IsSuccess.ShouldBeTrue();
        workSessions.Mutations.ShouldBe([$"progress:{victimSessionId}:50", $"close:{victimSessionId}"]);

        firstSessions.UpdateProgress(victimSessionId, 60).ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public async Task Plugin_cleanup_revokes_work_session_grants_for_that_plugin_only()
    {
        var workSessions = new TrackingPluginWorkSessionService();
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(
            new PluginExtensionPointRegistry(),
            lifetimes,
            new PluginHostServices(
                new InMemoryEventBus(),
                new RecordingBackgroundOperationReporter(),
                new RecordingBackgroundOperationCancellation(),
                pluginWorkSessions: workSessions));
        var first = bridge.Register("com.dobby.first", new EmptyModule());
        var second = bridge.Register("com.dobby.second", new EmptyModule());
        var firstSessions = first.Value.Resolve<IPluginWorkSessionService>();
        var secondSessions = second.Value.Resolve<IPluginWorkSessionService>();
        var firstSession = firstSessions.Open(
            "com.dobby.first",
            "plugin/com.dobby.first/work",
            "first.work");
        var secondSession = secondSessions.Open(
            "com.dobby.second",
            "plugin/com.dobby.second/work",
            "second.work");
        firstSession.IsSuccess.ShouldBeTrue();
        secondSession.IsSuccess.ShouldBeTrue();

        await bridge.CleanupPluginAsync("com.dobby.first");

        firstSessions.UpdateProgress(firstSession.Value.SessionId, 10)
            .ErrorCode.ShouldBe(ErrorCode.Forbidden);
        firstSessions.Close(firstSession.Value.SessionId).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        secondSessions.UpdateProgress(secondSession.Value.SessionId, 10).IsSuccess.ShouldBeTrue();

        await bridge.CleanupAllAsync();

        secondSessions.UpdateProgress(secondSession.Value.SessionId, 20)
            .ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    // ADR-0012 §2a closes the P1 host-injectable capability set. Evidence for closedness has to
    // come from the plugin-facing resolution path itself: reading the host's service dictionary or
    // grepping for `TryResolve` would restate the implementation instead of proving what a real
    // registered plugin module can obtain (NFR-027, ADR-0061). Both halves are load-bearing —
    // every listed type resolves, and a type the host merely happens to own does not, including
    // ones declared in the plugin-visible Dobby.Contracts assembly.
    [Fact]
    public async Task Plugin_scope_resolves_the_documented_capability_set_and_refuses_other_host_types()
    {
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(
            new PluginExtensionPointRegistry(),
            lifetimes,
            new PluginHostServices(
                new InMemoryEventBus(),
                new RecordingBackgroundOperationReporter(),
                new RecordingBackgroundOperationCancellation(),
                _ => new StaticCapabilityLicensing(PluginRuntimeMode.FullAccess),
                pluginWorkSessions: new RecordingPluginWorkSessionService(),
                pluginStorageFactory: _ => Substitute.For<IPluginStorage>()));
        var registration = bridge.Register("com.dobby.sample", new EmptyModule());
        registration.IsSuccess.ShouldBeTrue();
        var scope = registration.Value;

        scope.Resolve<IEventBus>().ShouldNotBeNull();
        scope.Resolve<IPluginStorage>().ShouldNotBeNull();
        scope.Resolve<ICapabilityLicensing>().ShouldNotBeNull();
        scope.Resolve<IPluginWorkSessionService>().ShouldNotBeNull();
        scope.Resolve<IBackgroundOperationReporter>().ShouldNotBeNull();
        scope.Resolve<IBackgroundOperationCancellation>().ShouldNotBeNull();
        scope.Resolve<ICapabilityPolicy<OperationPermissionCapability>>().ShouldNotBeNull();

        // Host-owned privilege surfaces, including the work-session cleanup contract that can close
        // another plugin's sessions and the trust/session/permission stores behind login.
        Should.Throw<InvalidOperationException>(() => scope.Resolve<IPluginWorkSessionCleanup>());
        Should.Throw<InvalidOperationException>(() => scope.Resolve<IPermissionGrantReader>());
        Should.Throw<InvalidOperationException>(() => scope.Resolve<ISessionStore>());
        Should.Throw<InvalidOperationException>(() => scope.Resolve<IVendorRecoveryPublicKey>());
        Should.Throw<InvalidOperationException>(() => scope.Resolve<IServiceProvider>());
        Should.Throw<InvalidOperationException>(() => scope.Resolve<PluginHostServices>());
        Should.Throw<InvalidOperationException>(() => scope.Resolve<PluginExtensionPointRegistry>());
        Should.Throw<InvalidOperationException>(() => scope.Resolve<PluginServiceRegistrationBridge>());
    }

    // §2a: the storage and licensing capabilities exist only when the host supplies their
    // per-plugin factory. Without it the type is not resolvable at all rather than resolving to a
    // host-wide or null instance, so a deployment that never wires the factory cannot leak one.
    [Fact]
    public async Task Capability_without_host_factory_is_not_resolvable_at_all()
    {
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(
            new PluginExtensionPointRegistry(),
            lifetimes,
            new PluginHostServices(
                new InMemoryEventBus(),
                new RecordingBackgroundOperationReporter(),
                new RecordingBackgroundOperationCancellation()));
        var registration = bridge.Register("com.dobby.sample", new EmptyModule());
        registration.IsSuccess.ShouldBeTrue();

        Should.Throw<InvalidOperationException>(() => registration.Value.Resolve<IPluginStorage>());
        Should.Throw<InvalidOperationException>(() => registration.Value.Resolve<ICapabilityLicensing>());
        registration.Value.Resolve<ICapabilityPolicy<OperationPermissionCapability>>()
            .Evaluate(
                new OperationPermissionCapability("users.create"),
                new PolicyContext("user-1", "com.dobby.sample", CorrelationId.New()))
            .Value.Allowed.ShouldBeFalse();
    }

    // §2: a plugin service that asks for an unlisted host type in its constructor is not created,
    // and an extension point that does so keeps the whole registration from being published — the
    // plugin does not get a partially activated surface with the privileged dependency omitted.
    [Fact]
    public async Task Plugin_service_requiring_an_unlisted_host_type_is_never_constructed()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);

        var privateRegistration = bridge.Register("com.dobby.first", new EscalatingPrivateServiceModule());
        privateRegistration.IsSuccess.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(
            () => privateRegistration.Value.Resolve<IEscalatingService>());

        var extensionRegistration = bridge.Register("com.dobby.second", new EscalatingExtensionPointModule());

        extensionRegistration.IsSuccess.ShouldBeFalse();
        extensionRegistration.ErrorCode.ShouldBe(ErrorCode.PluginInternalError);
        registry.GetRegistrationsSnapshot<IPluginPage>().ShouldBeEmpty();
    }

    [Fact]
    public void Registration_bridge_public_surface_does_not_expose_service_provider()
    {
        var exposedTypes = typeof(PluginServiceRegistrationBridge)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(MemberType)
            .Where(type => type is not null)
            .ToArray();

        exposedTypes.ShouldNotContain(typeof(IServiceProvider));
    }

    [Fact]
    public void Plugin_load_context_is_distinct_non_collectible_and_pins_contracts_to_host()
    {
        var entryAssemblyPath = typeof(PluginActivationCoreTests).Assembly.Location;
        var first = new PluginLoadContext("plugin-a", entryAssemblyPath);
        var second = new PluginLoadContext("plugin-b", entryAssemblyPath);

        first.ShouldNotBeSameAs(second);
        first.IsCollectible.ShouldBeFalse();
        second.IsCollectible.ShouldBeFalse();

        var contracts = first.LoadFromAssemblyName(typeof(IPluginModule).Assembly.GetName());
        contracts.ShouldBeSameAs(typeof(IPluginModule).Assembly);
    }

    [Fact]
    public void Plugin_load_context_resolves_runtime_specific_unmanaged_assets_from_plugin_deps()
    {
        using var directory = new TempDirectory();
        var entryPath = Path.Combine(directory.Path, "NativeProbe.dll");
        File.Copy(typeof(PluginActivationCoreTests).Assembly.Location, entryPath);
        File.WriteAllText(
            Path.Combine(directory.Path, "NativeProbe.deps.json"),
            NativeProbeDepsJson());
        var nativeDirectory = Path.Combine(directory.Path, "runtimes", "win-x64", "native");
        Directory.CreateDirectory(nativeDirectory);
        File.Copy(
            FindApprovedNativeDll(),
            Path.Combine(nativeDirectory, "dobby_test_native.dll"));
        var context = new PluginLoadContext("native-probe", entryPath);
        var loadUnmanaged = typeof(PluginLoadContext).GetMethod(
            "LoadUnmanagedDll",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var handle = (nint)loadUnmanaged!.Invoke(context, ["dobby_test_native"])!;

        handle.ShouldNotBe(nint.Zero);
    }

    // ADR-0007 §2 pins shared dependencies to the host by *name*, including everything under the
    // `System.` and `Microsoft.Extensions.` prefixes, and is fail-closed: a prefixed name the host
    // does not have is not served from the plugin directory even when the plugin ships a
    // resolvable copy. Reading `SharedAssemblyNames` would only restate the field, so the rule is
    // proven through the load path with plugin-local decoys present on disk (NFR-027).
    [Fact]
    public void Plugin_load_context_pins_prefixed_names_to_host_and_never_falls_back_to_plugin_copy()
    {
        _ = System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault;

        using var directory = new TempDirectory();
        var entryPath = Path.Combine(directory.Path, "PrefixProbe.dll");
        var decoySource = typeof(Result).Assembly.Location;
        File.Copy(decoySource, entryPath);
        File.Copy(decoySource, Path.Combine(directory.Path, "System.Text.Json.dll"));
        File.Copy(decoySource, Path.Combine(directory.Path, "System.Dobby.FakeShared.dll"));
        File.Copy(decoySource, Path.Combine(directory.Path, "Dobby.Plugin.PrivateProbe.dll"));
        File.WriteAllText(
            Path.Combine(directory.Path, "PrefixProbe.deps.json"),
            PrefixProbeDepsJson());
        var context = new PluginLoadContext("prefix-probe", entryPath);
        var load = typeof(PluginLoadContext).GetMethod(
            "Load",
            BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(AssemblyName)]);

        // Exact-name shared entry: the host instance wins over the plugin-local file, so the
        // returned assembly is the host's System.Text.Json and not the decoy bytes.
        var sharedByName = (Assembly?)load!.Invoke(context, [new AssemblyName("System.Text.Json")]);
        sharedByName.ShouldBeSameAs(typeof(System.Text.Json.JsonSerializer).Assembly);

        // Prefix rule, fail-closed: the host has no `System.Dobby.FakeShared`, and the plugin copy
        // is not used as a fallback.
        var failClosed = Should.Throw<TargetInvocationException>(
            () => load.Invoke(context, [new AssemblyName("System.Dobby.FakeShared")]));
        failClosed.InnerException.ShouldBeOfType<FileNotFoundException>();

        // Control: a non-prefixed plugin-private name does resolve from the plugin directory, so
        // the failure above comes from the prefix rule and not from an unusable deps file.
        var pluginPrivate = (Assembly?)load.Invoke(
            context,
            [new AssemblyName("Dobby.Plugin.PrivateProbe")]);
        pluginPrivate.ShouldNotBeNull();
        pluginPrivate.ShouldNotBeSameAs(typeof(Result).Assembly);
    }

    private static Type? MemberType(MemberInfo member)
    {
        return member switch
        {
            MethodInfo method => method.ReturnType,
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => null
        };
    }

    private static string NativeProbeDepsJson()
    {
        return """
            {
              "runtimeTarget": {
                "name": ".NETCoreApp,Version=v10.0/win-x64",
                "signature": ""
              },
              "compilationOptions": {},
              "targets": {
                ".NETCoreApp,Version=v10.0/win-x64": {
                  "NativeProbe/1.0.0": {
                    "dependencies": {
                      "DobbyTestNative": "1.0.0"
                    },
                    "runtime": {
                      "NativeProbe.dll": {}
                    }
                  },
                  "DobbyTestNative/1.0.0": {
                    "runtimeTargets": {
                      "runtimes/win-x64/native/dobby_test_native.dll": {
                        "rid": "win-x64",
                        "assetType": "native"
                      }
                    }
                  }
                }
              },
              "libraries": {
                "NativeProbe/1.0.0": {
                  "type": "project",
                  "serviceable": false,
                  "sha512": ""
                },
                "DobbyTestNative/1.0.0": {
                  "type": "package",
                  "serviceable": false,
                  "sha512": ""
                }
              }
            }
            """;
    }

    private static string PrefixProbeDepsJson()
    {
        return """
            {
              "runtimeTarget": {
                "name": ".NETCoreApp,Version=v10.0/win-x64",
                "signature": ""
              },
              "compilationOptions": {},
              "targets": {
                ".NETCoreApp,Version=v10.0/win-x64": {
                  "PrefixProbe/1.0.0": {
                    "runtime": {
                      "PrefixProbe.dll": {},
                      "System.Text.Json.dll": {},
                      "System.Dobby.FakeShared.dll": {},
                      "Dobby.Plugin.PrivateProbe.dll": {}
                    }
                  }
                }
              },
              "libraries": {
                "PrefixProbe/1.0.0": {
                  "type": "project",
                  "serviceable": false,
                  "sha512": ""
                }
              }
            }
            """;
    }

    private static string FindApprovedNativeDll()
    {
        var root = FindRepositoryRoot();
        return Path.Combine(
            root,
            "src",
            "Dobby.Infrastructure",
            "native",
            "win-x64",
            "sqlite3mc_x64.dll");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dobby.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "dobby-plugin-load-context-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class RecordingDisposable(string name, IList<string> disposalOrder) : IDisposable
    {
        public void Dispose()
        {
            disposalOrder.Add(name);
        }
    }

    private interface IPrivateService
    {
    }

    private interface IEscalatingService
    {
    }

    private sealed class EscalatingService(IPluginWorkSessionCleanup cleanup) : IEscalatingService
    {
        public IPluginWorkSessionCleanup Cleanup { get; } = cleanup;
    }

    private sealed class EscalatingPrivateServiceModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddInternal<IEscalatingService, EscalatingService>(PluginLifetime.Singleton);
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class EscalatingExtensionPointModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, EscalatingPage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class EscalatingPage(IPluginWorkSessionCleanup cleanup) : IPluginPage
    {
        public IPluginWorkSessionCleanup Cleanup { get; } = cleanup;

        public PluginPageDescriptor Descriptor { get; } = new("sample.escalating", "sample.escalating", "sample.escalating.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Failure<IPluginPageModel>(ErrorCode.NotFound));
        }

        public ValueTask<Result> ActivateAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> DeactivateAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> UnloadAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }
    }

    private sealed class PrivateService : IPrivateService
    {
    }

    private sealed class TestModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddInternal<IPrivateService, PrivateService>(PluginLifetime.Singleton);
            services.AddExtensionPoint<IPluginPage, DisposablePage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class DisposablePage(IPrivateService privateService) : IPluginPage, IDisposable
    {
        public static int DisposeCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public PluginPageDescriptor Descriptor { get; } = new("sample.page", "sample.page", "sample.page.label");

        public static void ResetDisposeCount()
        {
            DisposeCount = 0;
        }

        public ValueTask<Result<IPluginPageModel>> LoadAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            _ = privateService;
            return ValueTask.FromResult(Result.Failure<IPluginPageModel>(ErrorCode.NotFound));
        }

        public ValueTask<Result> ActivateAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> DeactivateAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> UnloadAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            DisposeCount++;
        }
    }

    private sealed class HostAwareModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, HostAwarePage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class EmptyModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class HostAwarePage(
        IEventBus eventBus,
        IBackgroundOperationReporter backgroundOperations,
        IBackgroundOperationCancellation backgroundOperationCancellation,
        ICapabilityLicensing licensing) : IPluginPage
    {
        public IEventBus EventBus { get; } = eventBus;

        public IBackgroundOperationReporter BackgroundOperations { get; } = backgroundOperations;

        public IBackgroundOperationCancellation BackgroundOperationCancellation { get; } = backgroundOperationCancellation;

        public ICapabilityLicensing Licensing { get; } = licensing;

        public PluginPageDescriptor Descriptor { get; } = new("sample.host", "sample.host", "sample.host.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Failure<IPluginPageModel>(ErrorCode.NotFound));
        }

        public ValueTask<Result> ActivateAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> DeactivateAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> UnloadAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }
    }

    private sealed class StaticCapabilityLicensing(PluginRuntimeMode mode) : ICapabilityLicensing
    {
        public Result<PluginRuntimeMode> GetCurrentMode()
        {
            return Result.Success(mode);
        }
    }

    private sealed class RecordingEventBus : IEventBus
    {
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : PublicIntegrationEvent
        {
            return Task.CompletedTask;
        }

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : PublicIntegrationEvent
        {
            return new Subscription();
        }

        private sealed class Subscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class RecordingBackgroundOperationReporter : IBackgroundOperationReporter
    {
        public IProgress<BackgroundOperationSnapshot> Progress { get; } = new Progress<BackgroundOperationSnapshot>();
    }

    private sealed class RecordingBackgroundOperationCancellation : IBackgroundOperationCancellation
    {
        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }

    private sealed class RecordingPluginWorkSessionService : IPluginWorkSessionService
    {
        public List<string> OpenedOwners { get; } = [];

        public Result<PluginWorkSessionHandle> Open(string ownerId, string routeKey, string labelKey)
        {
            OpenedOwners.Add(ownerId);
            return Result.Success(new PluginWorkSessionHandle(Guid.NewGuid(), ownerId, routeKey, labelKey));
        }

        public Result UpdateProgress(Guid sessionId, int percent) => Result.Success();

        public Result RecordResult(Guid sessionId, string resultSummaryKey) => Result.Success();

        public Result RecordError(Guid sessionId, string errorSummaryKey) => Result.Success();

        public Result Close(Guid sessionId) => Result.Success();
    }

    private sealed class TrackingPluginWorkSessionService : IPluginWorkSessionService
    {
        public List<string> Mutations { get; } = [];

        public Result<PluginWorkSessionHandle> Open(string ownerId, string routeKey, string labelKey)
        {
            return Result.Success(
                new PluginWorkSessionHandle(Guid.CreateVersion7(), ownerId, routeKey, labelKey));
        }

        public Result UpdateProgress(Guid sessionId, int percent)
        {
            Mutations.Add($"progress:{sessionId}:{percent}");
            return Result.Success();
        }

        public Result RecordResult(Guid sessionId, string resultSummaryKey)
        {
            Mutations.Add($"result:{sessionId}:{resultSummaryKey}");
            return Result.Success();
        }

        public Result RecordError(Guid sessionId, string errorSummaryKey)
        {
            Mutations.Add($"error:{sessionId}:{errorSummaryKey}");
            return Result.Success();
        }

        public Result Close(Guid sessionId)
        {
            Mutations.Add($"close:{sessionId}");
            return Result.Success();
        }
    }

    private sealed record SampleEvent(string Payload) : PublicIntegrationEvent;
}
