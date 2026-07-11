using Dobby.Contracts.PluginFramework;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.PluginFramework.Activation;
using Dobby.Shell;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class PluginPageRouteCoordinatorTests
{
    [Fact]
    public async Task Activate_route_loads_registered_plugin_page_model_into_shell()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.sample", new TestPluginModule()).IsSuccess.ShouldBeTrue();
        var shell = new ShellViewModel(
            new HostStringCatalog(),
            [
                NavigationContribution.Create(
                    "sample.home",
                    "plugin/com.dobby.sample/sample.home",
                    "sample.home.label",
                    NavigationTarget.TopToolbar,
                    new Dobby.Contracts.Ordering.UiOrderingKey("default", 100, "com.dobby.sample", 0))
            ]);
        var page = (RecordingPage)registry.GetRegistrations<IPluginPage>().Single().Instance;
        var coordinator = new PluginPageRouteCoordinator(registry, shell);

        var result = await coordinator.ActivateRouteAsync("plugin/com.dobby.sample/sample.home", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        shell.HasActivePluginPage.ShouldBeTrue();
        ((RecordingPageModel)shell.ActivePluginPageModel!).MessageKey.ShouldBe("sample.home.ready");
        page.ActivateCount.ShouldBe(1);
        page.LoadCount.ShouldBe(1);
        page.LifecycleEvents.ShouldBe(["load", "activate"]);
    }

    [Fact]
    public async Task ActivateRouteAsync_matches_plugin_page_by_normalized_route()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.sample", new TestPluginModule()).IsSuccess.ShouldBeTrue();
        var shell = new ShellViewModel(
            new HostStringCatalog(),
            [
                NavigationContribution.Create(
                    "sample.home",
                    "plugin/com.dobby.sample/sample.home",
                    "sample.home.label",
                    NavigationTarget.TopToolbar,
                    new Dobby.Contracts.Ordering.UiOrderingKey("default", 100, "com.dobby.sample", 0))
            ]);
        var page = (RecordingPage)registry.GetRegistrationsSnapshot<IPluginPage>().Single().Instance;
        var coordinator = new PluginPageRouteCoordinator(registry, shell);

        var result = await coordinator.ActivateRouteAsync(
            "plugin/com.dobby.sample/sample.home",
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        shell.HasActivePluginPage.ShouldBeTrue();
        ((RecordingPageModel)shell.ActivePluginPageModel!).MessageKey.ShouldBe("sample.home.ready");
        page.ActivateCount.ShouldBe(1);
        page.LoadCount.ShouldBe(1);
    }

    [Fact]
    public async Task Reactivating_same_plugin_route_keeps_current_page_without_reloading_lifecycle()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.sample", new TestPluginModule()).IsSuccess.ShouldBeTrue();
        var shell = CreateShellWithSampleContribution();
        var page = (RecordingPage)registry.GetRegistrationsSnapshot<IPluginPage>().Single().Instance;
        var coordinator = new PluginPageRouteCoordinator(registry, shell);
        (await coordinator.ActivateRouteAsync("plugin/com.dobby.sample/sample.home", CancellationToken.None))
            .IsSuccess.ShouldBeTrue();
        var activePage = shell.ActivePluginPageModel;

        var result = await coordinator.ActivateRouteAsync("plugin/com.dobby.sample/sample.home", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        shell.ActiveRouteKey.ShouldBe("plugin/com.dobby.sample/sample.home");
        shell.ActivePluginPageModel.ShouldBe(activePage);
        page.ActivateCount.ShouldBe(1);
        page.LoadCount.ShouldBe(1);
        page.DeactivateCount.ShouldBe(0);
        page.UnloadCount.ShouldBe(0);
    }

    [Fact]
    public async Task Same_route_retry_reactivates_deactivated_loaded_page_after_unload_failure()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.sample", new FirstUnloadFailsPluginModule()).IsSuccess.ShouldBeTrue();
        bridge.Register("com.dobby.next", new NextPluginModule()).IsSuccess.ShouldBeTrue();
        var shell = CreateShellWithSampleAndNextContributions();
        var currentPage = (FirstUnloadFailsPage)registry
            .GetRegistrationsSnapshot<IPluginPage>()
            .Single(registration => registration.PluginId == "com.dobby.sample")
            .Instance;
        var coordinator = new PluginPageRouteCoordinator(registry, shell);

        (await coordinator.ActivateRouteAsync("plugin/com.dobby.sample/sample.home", CancellationToken.None))
            .IsSuccess.ShouldBeTrue();
        var firstModel = shell.ActivePluginPageModel;

        var firstSwitch = await coordinator.ActivateRouteAsync(
            "plugin/com.dobby.next/sample.detail",
            CancellationToken.None);

        firstSwitch.IsSuccess.ShouldBeFalse();
        firstSwitch.ErrorCode.ShouldBe(ErrorCode.PluginInternalError);
        currentPage.LoadCount.ShouldBe(1);
        currentPage.ActivateCount.ShouldBe(1);
        currentPage.DeactivateCount.ShouldBe(1);
        currentPage.UnloadCount.ShouldBe(1);
        currentPage.LifecycleEvents.ShouldBe(["load", "activate", "deactivate", "unload"]);
        shell.ActiveRouteKey.ShouldBe("plugin/com.dobby.sample/sample.home");
        shell.HasActivePluginPage.ShouldBeFalse();
        shell.ActivePluginPageModel.ShouldBeNull();

        var sameRouteRetry = await coordinator.ActivateRouteAsync(
            "plugin/com.dobby.sample/sample.home",
            CancellationToken.None);

        sameRouteRetry.IsSuccess.ShouldBeTrue();
        shell.ActiveRouteKey.ShouldBe("plugin/com.dobby.sample/sample.home");
        shell.HasActivePluginPage.ShouldBeTrue();
        shell.ActivePluginPageModel.ShouldBeSameAs(firstModel);
        currentPage.LoadCount.ShouldBe(1);
        currentPage.ActivateCount.ShouldBe(2);
        currentPage.DeactivateCount.ShouldBe(1);
        currentPage.UnloadCount.ShouldBe(1);

        var laterSwitch = await coordinator.ActivateRouteAsync(
            "plugin/com.dobby.next/sample.detail",
            CancellationToken.None);

        laterSwitch.IsSuccess.ShouldBeTrue();
        shell.ActiveRouteKey.ShouldBe("plugin/com.dobby.next/sample.detail");
        shell.HasActivePluginPage.ShouldBeTrue();
        ((RecordingPageModel)shell.ActivePluginPageModel!).MessageKey.ShouldBe("sample.detail.ready");
        currentPage.LoadCount.ShouldBe(1);
        currentPage.ActivateCount.ShouldBe(2);
        currentPage.DeactivateCount.ShouldBe(2);
        currentPage.UnloadCount.ShouldBe(2);
    }

    [Fact]
    public async Task ActivateRouteAsync_for_builtin_route_does_not_scan_plugin_page_descriptors()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.guard", new DescriptorGuardPluginModule()).IsSuccess.ShouldBeTrue();
        var shell = ShellViewModel.CreateDefault(new HostStringCatalog());
        var coordinator = new PluginPageRouteCoordinator(registry, shell);

        var result = await coordinator.ActivateRouteAsync(RouteRegistry.SettingsRouteKey, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        shell.HasActivePluginPage.ShouldBeFalse();
    }

    [Fact]
    public async Task Throwing_plugin_page_descriptor_does_not_block_unrelated_plugin_route()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.guard", new DescriptorGuardPluginModule()).IsSuccess.ShouldBeTrue();
        bridge.Register("com.dobby.next", new NextPluginModule()).IsSuccess.ShouldBeTrue();
        var shell = CreateShellWithSampleAndNextContributions();
        var coordinator = new PluginPageRouteCoordinator(registry, shell);

        var result = await coordinator.ActivateRouteAsync(
            "plugin/com.dobby.next/sample.detail",
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        shell.ActiveRouteKey.ShouldBe("plugin/com.dobby.next/sample.detail");
        shell.HasActivePluginPage.ShouldBeTrue();
        ((RecordingPageModel)shell.ActivePluginPageModel!).MessageKey.ShouldBe("sample.detail.ready");
    }

    [Theory]
    [InlineData(typeof(FailingActivatePluginModule))]
    [InlineData(typeof(FailingLoadPluginModule))]
    [InlineData(typeof(ThrowingActivatePluginModule))]
    [InlineData(typeof(ThrowingLoadPluginModule))]
    public async Task Failed_plugin_page_activation_does_not_mutate_shell_route_tab_or_page(Type moduleType)
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.sample", (IPluginModule)Activator.CreateInstance(moduleType)!).IsSuccess.ShouldBeTrue();
        var shell = CreateShellWithSampleContribution();
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var activeTabBefore = shell.ActiveWorkspaceTab;
        var coordinator = new PluginPageRouteCoordinator(registry, shell);

        var result = await coordinator.ActivateRouteAsync(
            "plugin/com.dobby.sample/sample.home",
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.PluginInternalError);
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        shell.ActiveWorkspaceTab.TabId.ShouldBe(activeTabBefore.TabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        shell.HasActivePluginPage.ShouldBeFalse();
    }

    [Theory]
    [InlineData(typeof(FailingLoadPluginModule))]
    [InlineData(typeof(ThrowingLoadPluginModule))]
    public async Task Failed_plugin_page_load_does_not_activate_unloaded_page(Type moduleType)
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.sample", (IPluginModule)Activator.CreateInstance(moduleType)!).IsSuccess.ShouldBeTrue();
        var shell = CreateShellWithSampleContribution();
        var page = (ILifecycleProbe)registry.GetRegistrationsSnapshot<IPluginPage>().Single().Instance;
        var coordinator = new PluginPageRouteCoordinator(registry, shell);

        var result = await coordinator.ActivateRouteAsync(
            "plugin/com.dobby.sample/sample.home",
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.PluginInternalError);
        page.LoadCount.ShouldBe(1);
        page.ActivateCount.ShouldBe(0);
        page.DeactivateCount.ShouldBe(0);
        page.UnloadCount.ShouldBe(0);
        shell.HasActivePluginPage.ShouldBeFalse();
    }

    [Fact]
    public async Task Plugin_page_load_fault_records_safe_diagnostics()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.sample", new ThrowingLoadPluginModule()).IsSuccess.ShouldBeTrue();
        var shell = CreateShellWithSampleContribution();
        var faults = new List<(string PluginId, string RouteKey, string Operation, ErrorCode ErrorCode)>();
        var coordinator = new PluginPageRouteCoordinator(
            registry,
            shell,
            pageFaultSink: (pluginId, routeKey, operation, errorCode) =>
                faults.Add((pluginId, routeKey, operation, errorCode)));

        var result = await coordinator.ActivateRouteAsync(
            "plugin/com.dobby.sample/sample.home",
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        faults.ShouldBe(
        [
            ("com.dobby.sample",
                "plugin/com.dobby.sample/sample.home",
                "load",
                ErrorCode.PluginInternalError)
        ]);
    }

    [Theory]
    [InlineData(typeof(FailingActivatePluginModule))]
    [InlineData(typeof(ThrowingActivatePluginModule))]
    public async Task Failed_plugin_page_activation_unloads_loaded_uncommitted_page(Type moduleType)
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.sample", (IPluginModule)Activator.CreateInstance(moduleType)!).IsSuccess.ShouldBeTrue();
        var shell = CreateShellWithSampleContribution();
        var page = (ILifecycleProbe)registry.GetRegistrationsSnapshot<IPluginPage>().Single().Instance;
        var coordinator = new PluginPageRouteCoordinator(registry, shell);

        var result = await coordinator.ActivateRouteAsync(
            "plugin/com.dobby.sample/sample.home",
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.PluginInternalError);
        page.LoadCount.ShouldBe(1);
        page.ActivateCount.ShouldBe(1);
        page.DeactivateCount.ShouldBe(0);
        page.UnloadCount.ShouldBe(1);
        shell.HasActivePluginPage.ShouldBeFalse();
    }

    [Fact]
    public async Task Missing_plugin_page_registration_fails_without_mutating_shell_route()
    {
        var registry = new PluginExtensionPointRegistry();
        var shell = CreateShellWithSampleContribution();
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var activeTabBefore = shell.ActiveWorkspaceTab;
        var coordinator = new PluginPageRouteCoordinator(registry, shell);

        var result = await coordinator.ActivateRouteAsync(
            "plugin/com.dobby.sample/sample.home",
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        shell.ActiveWorkspaceTab.TabId.ShouldBe(activeTabBefore.TabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        shell.HasActivePluginPage.ShouldBeFalse();
    }

    [Theory]
    [InlineData(typeof(FailingDeactivatePluginModule), RouteRegistry.SettingsRouteKey)]
    [InlineData(typeof(FailingDeactivatePluginModule), "plugin/com.dobby.next/sample.detail")]
    [InlineData(typeof(ThrowingDeactivatePluginModule), RouteRegistry.SettingsRouteKey)]
    public async Task Deactivate_failure_blocks_route_switch_without_mutating_shell_state(
        Type currentModuleType,
        string nextRouteKey)
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.sample", (IPluginModule)Activator.CreateInstance(currentModuleType)!).IsSuccess.ShouldBeTrue();
        bridge.Register("com.dobby.next", new NextPluginModule()).IsSuccess.ShouldBeTrue();
        var shell = CreateShellWithSampleAndNextContributions();
        var coordinator = new PluginPageRouteCoordinator(registry, shell);
        (await coordinator.ActivateRouteAsync("plugin/com.dobby.sample/sample.home", CancellationToken.None))
            .IsSuccess.ShouldBeTrue();
        var activeTabBefore = shell.ActiveWorkspaceTab;
        var activePageBefore = shell.ActivePluginPageModel;

        var result = await coordinator.ActivateRouteAsync(nextRouteKey, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.PluginInternalError);
        shell.ActiveRouteKey.ShouldBe("plugin/com.dobby.sample/sample.home");
        shell.ActiveWorkspaceTab.TabId.ShouldBe(activeTabBefore.TabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe("plugin/com.dobby.sample/sample.home");
        shell.ActivePluginPageModel.ShouldBe(activePageBefore);
    }

    [Theory]
    [InlineData(typeof(FailingUnloadPluginModule), RouteRegistry.SettingsRouteKey)]
    [InlineData(typeof(FailingUnloadPluginModule), "plugin/com.dobby.next/sample.detail")]
    [InlineData(typeof(ThrowingUnloadPluginModule), "plugin/com.dobby.next/sample.detail")]
    public async Task Unload_failure_blocks_route_switch_and_clears_interactive_plugin_page_model(
        Type currentModuleType,
        string nextRouteKey)
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        bridge.Register("com.dobby.sample", (IPluginModule)Activator.CreateInstance(currentModuleType)!).IsSuccess.ShouldBeTrue();
        bridge.Register("com.dobby.next", new NextPluginModule()).IsSuccess.ShouldBeTrue();
        var shell = CreateShellWithSampleAndNextContributions();
        var coordinator = new PluginPageRouteCoordinator(registry, shell);
        (await coordinator.ActivateRouteAsync("plugin/com.dobby.sample/sample.home", CancellationToken.None))
            .IsSuccess.ShouldBeTrue();
        var activeTabBefore = shell.ActiveWorkspaceTab;

        var result = await coordinator.ActivateRouteAsync(nextRouteKey, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.PluginInternalError);
        shell.ActiveRouteKey.ShouldBe("plugin/com.dobby.sample/sample.home");
        shell.ActiveWorkspaceTab.TabId.ShouldBe(activeTabBefore.TabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe("plugin/com.dobby.sample/sample.home");
        shell.HasActivePluginPage.ShouldBeFalse();
        shell.ActivePluginPageModel.ShouldBeNull();
    }

    private static ShellViewModel CreateShellWithSampleContribution()
    {
        return new ShellViewModel(
            new HostStringCatalog(),
            [
                NavigationContribution.Create(
                    "sample.home",
                    "plugin/com.dobby.sample/sample.home",
                    "sample.home.label",
                    NavigationTarget.TopToolbar,
                    new Dobby.Contracts.Ordering.UiOrderingKey("default", 100, "com.dobby.sample", 0))
            ]);
    }

    private static ShellViewModel CreateShellWithSampleAndNextContributions()
    {
        return new ShellViewModel(
            new HostStringCatalog(),
            [
                NavigationContribution.Create(
                    "sample.home",
                    "plugin/com.dobby.sample/sample.home",
                    "sample.home.label",
                    NavigationTarget.TopToolbar,
                    new Dobby.Contracts.Ordering.UiOrderingKey("default", 100, "com.dobby.sample", 0)),
                NavigationContribution.Create(
                    "sample.detail",
                    "plugin/com.dobby.next/sample.detail",
                    "sample.detail.label",
                    NavigationTarget.TopToolbar,
                    new Dobby.Contracts.Ordering.UiOrderingKey("default", 101, "com.dobby.next", 0))
            ]);
    }

    private sealed class TestPluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, RecordingPage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class DescriptorGuardPluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, DescriptorGuardPage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class FailingActivatePluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, FailingActivatePage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class FailingLoadPluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, FailingLoadPage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class ThrowingActivatePluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, ThrowingActivatePage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class ThrowingLoadPluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, ThrowingLoadPage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class FailingDeactivatePluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, FailingDeactivatePage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class FailingUnloadPluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, FailingUnloadPage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class ThrowingDeactivatePluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, ThrowingDeactivatePage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class ThrowingUnloadPluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, ThrowingUnloadPage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class FirstUnloadFailsPluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, FirstUnloadFailsPage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class NextPluginModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, NextPage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private interface ILifecycleProbe
    {
        int LoadCount { get; }

        int ActivateCount { get; }

        int DeactivateCount { get; }

        int UnloadCount { get; }
    }

    private sealed class RecordingPage : IPluginPage
    {
        public int ActivateCount { get; private set; }

        public int LoadCount { get; private set; }

        public int DeactivateCount { get; private set; }

        public int UnloadCount { get; private set; }

        public List<string> LifecycleEvents { get; } = [];

        public PluginPageDescriptor Descriptor { get; } = new("sample.home", "sample.home", "sample.home.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            LoadCount++;
            LifecycleEvents.Add("load");
            context.PluginId.ShouldBe("com.dobby.sample");
            return ValueTask.FromResult(Result.Success<IPluginPageModel>(new RecordingPageModel("sample.home.ready")));
        }

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            ActivateCount++;
            LifecycleEvents.Add("activate");
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            DeactivateCount++;
            LifecycleEvents.Add("deactivate");
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            UnloadCount++;
            LifecycleEvents.Add("unload");
            return ValueTask.FromResult(Result.Success());
        }
    }

    private sealed class DescriptorGuardPage : IPluginPage
    {
        public PluginPageDescriptor Descriptor =>
            throw new InvalidOperationException("Built-in routes must not scan plugin page descriptors.");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Built-in routes must not load plugin pages.");

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Built-in routes must not activate plugin pages.");

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());
    }

    private sealed class FailingActivatePage : IPluginPage, ILifecycleProbe
    {
        public int LoadCount { get; private set; }

        public int ActivateCount { get; private set; }

        public int DeactivateCount { get; private set; }

        public int UnloadCount { get; private set; }

        public PluginPageDescriptor Descriptor { get; } = new("sample.home", "sample.home", "sample.home.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordLoad());

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordActivate());

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordDeactivate());

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordUnload());

        private Result<IPluginPageModel> RecordLoad()
        {
            LoadCount++;
            return Result.Success<IPluginPageModel>(new RecordingPageModel("sample.home.ready"));
        }

        private Result RecordActivate()
        {
            ActivateCount++;
            return Result.Failure(ErrorCode.PluginInternalError);
        }

        private Result RecordDeactivate()
        {
            DeactivateCount++;
            return Result.Success();
        }

        private Result RecordUnload()
        {
            UnloadCount++;
            return Result.Success();
        }
    }

    private sealed class FailingLoadPage : IPluginPage, ILifecycleProbe
    {
        public int LoadCount { get; private set; }

        public int ActivateCount { get; private set; }

        public int DeactivateCount { get; private set; }

        public int UnloadCount { get; private set; }

        public PluginPageDescriptor Descriptor { get; } = new("sample.home", "sample.home", "sample.home.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordLoad());

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordActivate());

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordDeactivate());

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordUnload());

        private Result RecordActivate()
        {
            ActivateCount++;
            return Result.Success();
        }

        private Result<IPluginPageModel> RecordLoad()
        {
            LoadCount++;
            return Result.Failure<IPluginPageModel>(ErrorCode.PluginInternalError);
        }

        private Result RecordDeactivate()
        {
            DeactivateCount++;
            return Result.Success();
        }

        private Result RecordUnload()
        {
            UnloadCount++;
            return Result.Success();
        }
    }

    private sealed class ThrowingActivatePage : IPluginPage, ILifecycleProbe
    {
        public int LoadCount { get; private set; }

        public int ActivateCount { get; private set; }

        public int DeactivateCount { get; private set; }

        public int UnloadCount { get; private set; }

        public PluginPageDescriptor Descriptor { get; } = new("sample.home", "sample.home", "sample.home.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordLoad());

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            ActivateCount++;
            throw new InvalidOperationException("raw activate detail");
        }

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordDeactivate());

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordUnload());

        private Result<IPluginPageModel> RecordLoad()
        {
            LoadCount++;
            return Result.Success<IPluginPageModel>(new RecordingPageModel("sample.home.ready"));
        }

        private Result RecordDeactivate()
        {
            DeactivateCount++;
            return Result.Success();
        }

        private Result RecordUnload()
        {
            UnloadCount++;
            return Result.Success();
        }
    }

    private sealed class ThrowingLoadPage : IPluginPage, ILifecycleProbe
    {
        public int LoadCount { get; private set; }

        public int ActivateCount { get; private set; }

        public int DeactivateCount { get; private set; }

        public int UnloadCount { get; private set; }

        public PluginPageDescriptor Descriptor { get; } = new("sample.home", "sample.home", "sample.home.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            LoadCount++;
            throw new InvalidOperationException("raw load detail");
        }

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordActivate());

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordDeactivate());

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RecordUnload());

        private Result RecordActivate()
        {
            ActivateCount++;
            return Result.Success();
        }

        private Result RecordDeactivate()
        {
            DeactivateCount++;
            return Result.Success();
        }

        private Result RecordUnload()
        {
            UnloadCount++;
            return Result.Success();
        }
    }

    private sealed class FailingDeactivatePage : IPluginPage
    {
        public PluginPageDescriptor Descriptor { get; } = new("sample.home", "sample.home", "sample.home.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success<IPluginPageModel>(new RecordingPageModel("sample.home.ready")));

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Failure(ErrorCode.PluginInternalError));

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());
    }

    private sealed class FailingUnloadPage : IPluginPage
    {
        public PluginPageDescriptor Descriptor { get; } = new("sample.home", "sample.home", "sample.home.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success<IPluginPageModel>(new RecordingPageModel("sample.home.ready")));

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Failure(ErrorCode.PluginInternalError));
    }

    private sealed class ThrowingDeactivatePage : IPluginPage
    {
        public PluginPageDescriptor Descriptor { get; } = new("sample.home", "sample.home", "sample.home.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success<IPluginPageModel>(new RecordingPageModel("sample.home.ready")));

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("raw deactivate detail");

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());
    }

    private sealed class ThrowingUnloadPage : IPluginPage
    {
        public PluginPageDescriptor Descriptor { get; } = new("sample.home", "sample.home", "sample.home.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success<IPluginPageModel>(new RecordingPageModel("sample.home.ready")));

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("raw unload detail");
    }

    private sealed class FirstUnloadFailsPage : IPluginPage, ILifecycleProbe
    {
        private int remainingUnloadFailures = 1;

        public int LoadCount { get; private set; }

        public int ActivateCount { get; private set; }

        public int DeactivateCount { get; private set; }

        public int UnloadCount { get; private set; }

        public List<string> LifecycleEvents { get; } = [];

        public PluginPageDescriptor Descriptor { get; } = new("sample.home", "sample.home", "sample.home.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            LoadCount++;
            LifecycleEvents.Add("load");
            return ValueTask.FromResult(Result.Success<IPluginPageModel>(
                new RecordingPageModel($"sample.home.ready.{LoadCount}")));
        }

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            ActivateCount++;
            LifecycleEvents.Add("activate");
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            DeactivateCount++;
            LifecycleEvents.Add("deactivate");
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            UnloadCount++;
            LifecycleEvents.Add("unload");
            if (remainingUnloadFailures-- > 0)
            {
                return ValueTask.FromResult(Result.Failure(ErrorCode.PluginInternalError));
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    private sealed class NextPage : IPluginPage
    {
        public PluginPageDescriptor Descriptor { get; } = new("sample.detail", "sample.detail", "sample.detail.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success<IPluginPageModel>(new RecordingPageModel("sample.detail.ready")));

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());
    }

    private sealed record RecordingPageModel(string MessageKey) : IPluginPageModel;
}
