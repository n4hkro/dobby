using System.Reflection;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Events;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginFramework;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Messaging;
using Dobby.PluginFramework.Activation;
using Dobby.UnitTests.PluginFramework.Packaging;
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

    private sealed record SampleEvent(string Payload) : PublicIntegrationEvent;
}
