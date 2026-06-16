using System.Windows.Controls;
using Dobby.Contracts.PluginPages;
using Dobby.Shell.PluginPages;
using Shouldly;

namespace Dobby.UnitTests.Shell;

public sealed class PluginPageRenderRegistryTests
{
    [Fact]
    public void Registry_renders_allowlisted_models_to_host_views()
    {
        RunOnSta(() =>
        {
            var registry = new PluginPageRenderRegistry();
            registry.Register<AnalyticsModel>(model => new TextBlock { Text = model.Label });

            var result = registry.Render(new AnalyticsModel("plugin analytics"));

            result.IsSuccess.ShouldBeTrue();
            ((TextBlock)result.Value).Text.ShouldBe("plugin analytics");
        });
    }

    [Fact]
    public void Registry_rejects_unregistered_models()
    {
        var registry = new PluginPageRenderRegistry();

        registry.Render(new UnknownModel()).ErrorCode.ShouldBe(Dobby.Core.ErrorCode.NotFound);
    }

    [Fact]
    public void Default_registry_renders_approved_contract_text_model_and_rejects_unknown_models()
    {
        RunOnSta(() =>
        {
            var registry = PluginPageRenderRegistry.CreateDefault();

            var render = registry.Render(new PluginTextPageModel("sample.home.ready"));

            render.IsSuccess.ShouldBeTrue();
            ((TextBlock)render.Value).Text.ShouldBe("sample.home.ready");
            registry.Render(new UnknownModel()).ErrorCode.ShouldBe(Dobby.Core.ErrorCode.NotFound);
        });
    }

    private sealed class AnalyticsModel(string label) : IPluginPageModel
    {
        public string Label { get; } = label;
    }

    private sealed class UnknownModel : IPluginPageModel
    {
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }
    }
}
