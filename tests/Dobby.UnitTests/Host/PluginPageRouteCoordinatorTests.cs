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

    private sealed class RecordingPage : IPluginPage
    {
        public int ActivateCount { get; private set; }

        public int LoadCount { get; private set; }

        public PluginPageDescriptor Descriptor { get; } = new("sample.home", "sample.home", "sample.home.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            LoadCount++;
            context.PluginId.ShouldBe("com.dobby.sample");
            return ValueTask.FromResult(Result.Success<IPluginPageModel>(new RecordingPageModel("sample.home.ready")));
        }

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken)
        {
            ActivateCount++;
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
    }

    private sealed record RecordingPageModel(string MessageKey) : IPluginPageModel;
}
