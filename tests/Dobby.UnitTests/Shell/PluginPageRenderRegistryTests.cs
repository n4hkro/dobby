using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
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

    [Fact]
    public void Default_registry_renders_approved_action_page_model_to_host_buttons()
    {
        RunOnSta(() =>
        {
            var registry = PluginPageRenderRegistry.CreateDefault();
            var model = new PluginActionPageModel(
                "sample.home.ready",
                new PluginPageActionContext("com.dobby.sample", "plugin/com.dobby.sample/greet", "managed", "correlation"),
                [
                    new PluginPageAction(
                        "sample.greet",
                        "sample.greet.label",
                        (_, _) => ValueTask.FromResult(Dobby.Core.Result.Success(new PluginPageActionResult("sample.greet.ok"))))
                ]);

            var render = registry.Render(model);

            render.IsSuccess.ShouldBeTrue();
            var panel = render.Value.ShouldBeOfType<StackPanel>();
            panel.Children.OfType<TextBlock>().First().Text.ShouldBe("sample.home.ready");
            panel.Children.OfType<Button>().Single().Content.ShouldBe("sample.greet.label");
        });
    }

    [Fact]
    public void Action_page_button_isolates_unhandled_action_faults_as_plugin_internal_error()
    {
        RunOnSta(() =>
        {
            var registry = PluginPageRenderRegistry.CreateDefault();
            var model = new PluginActionPageModel(
                "sample.home.ready",
                new PluginPageActionContext("com.dobby.sample", "plugin/com.dobby.sample/greet", "managed", "correlation"),
                [
                    new PluginPageAction(
                        "sample.fault",
                        "sample.fault.label",
                        (_, _) => throw new InvalidOperationException("sample.fault.unhandled"))
                ]);

            var render = registry.Render(model);

            render.IsSuccess.ShouldBeTrue();
            var panel = render.Value.ShouldBeOfType<StackPanel>();
            var button = panel.Children.OfType<Button>().Single();
            button.RaiseEvent(new System.Windows.RoutedEventArgs(ButtonBase.ClickEvent, button));

            panel.Children.OfType<TextBlock>().Last().Text.ShouldBe(ErrorCode.PluginInternalError.ToString());
            button.IsEnabled.ShouldBeTrue();
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
