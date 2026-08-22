using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
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

        registry.Render(new UnknownModel()).ErrorCode.ShouldBe(ErrorCode.NotFound);
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
            registry.Render(new UnknownModel()).ErrorCode.ShouldBe(ErrorCode.NotFound);
        });
    }

    [Theory]
    [InlineData("az-Latn-AZ", "Sorğu", "Göndər")]
    [InlineData("en-US", "Request", "Submit")]
    [InlineData("ru-RU", "Запрос", "Отправить")]
    public void Component_page_resolves_every_host_rendered_label_for_requested_culture(
        string cultureName,
        string expectedTitle,
        string expectedCommand)
    {
        RunOnSta(() =>
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            var registry = PluginPageRenderRegistry.CreateDefault(
                new RecordingDispatcher(),
                () => culture);

            var render = registry.Render(CreateModel());

            render.IsSuccess.ShouldBeTrue();
            var viewer = render.Value.ShouldBeOfType<ScrollViewer>();
            Descendants<TextBlock>(viewer).First().Text.ShouldBe(expectedTitle);
            Descendants<Button>(viewer).Single().Content.ShouldBe(expectedCommand);
            Descendants<FrameworkElement>(viewer)
                .Where(element => element is TextBox or ComboBox or DatePicker or CheckBox or Button)
                .ShouldAllBe(element => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(element)));
        });
    }

    [Fact]
    public void Component_page_dispatches_opaque_command_with_normalized_host_owned_input_values()
    {
        RunOnSta(() =>
        {
            var dispatcher = new RecordingDispatcher();
            var registry = PluginPageRenderRegistry.CreateDefault(
                dispatcher,
                () => CultureInfo.GetCultureInfo("en-US"));
            var viewer = registry.Render(CreateModel()).Value.ShouldBeOfType<ScrollViewer>();

            Descendants<Button>(viewer).Single().RaiseEvent(
                new RoutedEventArgs(ButtonBase.ClickEvent));

            dispatcher.Requests.Count.ShouldBe(1);
            var request = dispatcher.Requests.Single();
            request.CommandId.ShouldBe("sample.submit");
            request.OperationKey.ShouldBe("com.dobby.sample.greet.execute");
            request.Values["name"].ShouldBe("Ada");
            request.Values["quantity"].ShouldBe("2");
            request.Values["active"].ShouldBe("true");
            Descendants<TextBlock>(viewer).Last().Text.ShouldBe("Accepted Ada");
        });
    }

    [Fact]
    public void Component_page_rejects_missing_localization_key_parity_before_rendering()
    {
        var model = CreateModel();
        var malformed = model with
        {
            Localization = model.Localization with
            {
                Bundles =
                [
                    model.Localization.Bundles[0],
                    model.Localization.Bundles[1] with
                    {
                        Entries = model.Localization.Bundles[1].Entries
                            .Where(entry => !entry.Key.Equals("command", StringComparison.Ordinal))
                            .ToDictionary(StringComparer.Ordinal)
                    }
                ]
            }
        };

        PluginPageRenderRegistry.CreateDefault().Render(malformed).ErrorCode
            .ShouldBe(ErrorCode.ValidationFailed);
    }

    [Fact]
    public void Component_page_maps_dispatch_failure_to_localized_non_disclosing_status()
    {
        RunOnSta(() =>
        {
            var registry = PluginPageRenderRegistry.CreateDefault(
                new RecordingDispatcher(ErrorCode.PluginInternalError),
                () => CultureInfo.GetCultureInfo("en-US"));
            var viewer = registry.Render(CreateModel()).Value.ShouldBeOfType<ScrollViewer>();

            Descendants<Button>(viewer).Single().RaiseEvent(
                new RoutedEventArgs(ButtonBase.ClickEvent));

            Descendants<TextBlock>(viewer).Last().Text.ShouldBe("Command failed");
            Descendants<TextBlock>(viewer).Last().Text.ShouldNotContain("PluginInternalError");
            AutomationProperties.GetLiveSetting(Descendants<TextBlock>(viewer).Last())
                .ShouldBe(AutomationLiveSetting.Assertive);
        });
    }

    [Fact]
    public void Component_page_cancel_button_cancels_inflight_host_command_and_announces_result()
    {
        RunOnSta(() =>
        {
            var dispatcher = new CancelAwareDispatcher();
            var model = CreateModel();
            model = model with
            {
                Commands =
                [
                    model.Commands.Single() with
                    {
                        CanCancel = true,
                        CancelLabel = new PluginLocalizedText("cancel")
                    }
                ]
            };
            var registry = PluginPageRenderRegistry.CreateDefault(
                dispatcher,
                () => CultureInfo.GetCultureInfo("en-US"));
            var viewer = registry.Render(model).Value.ShouldBeOfType<ScrollViewer>();
            var buttons = Descendants<Button>(viewer).ToArray();

            buttons[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            dispatcher.Started.Task.IsCompleted.ShouldBeTrue();
            buttons[1].IsEnabled.ShouldBeTrue();
            buttons[1].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            DrainDispatcher();

            dispatcher.ObservedCancellation.ShouldBeTrue();
            Descendants<TextBlock>(viewer).Last().Text.ShouldBe("Cancelled");
            buttons[0].IsEnabled.ShouldBeTrue();
            buttons[1].IsEnabled.ShouldBeFalse();
        });
    }

    private static PluginComponentPageModel CreateModel()
    {
        var keys = new Dictionary<string, (string Az, string En, string Ru)>(StringComparer.Ordinal)
        {
            ["title"] = ("Sorğu", "Request", "Запрос"),
            ["failure"] = ("Əmr alınmadı", "Command failed", "Команда не выполнена"),
            ["cancelled"] = ("Ləğv edildi", "Cancelled", "Отменено"),
            ["form"] = ("Detallar", "Details", "Детали"),
            ["name"] = ("Ad", "Name", "Имя"),
            ["quantity"] = ("Miqdar", "Quantity", "Количество"),
            ["active"] = ("Aktiv", "Active", "Активный"),
            ["command"] = ("Göndər", "Submit", "Отправить"),
            ["cancel"] = ("Ləğv et", "Cancel", "Отмена"),
            ["success"] = ("{0} qəbul edildi", "Accepted {0}", "Принято: {0}")
        };
        PluginLocalizationBundle Bundle(string culture, Func<(string Az, string En, string Ru), string> select) =>
            new(culture, keys.ToDictionary(entry => entry.Key, entry => select(entry.Value), StringComparer.Ordinal));
        var localization = new PluginLocalizationCatalog(
            "en-US",
            [
                Bundle("az-Latn-AZ", value => value.Az),
                Bundle("en-US", value => value.En),
                Bundle("ru-RU", value => value.Ru)
            ]);
        return new PluginComponentPageModel(
            new PluginPageCommandContext(
                "com.dobby.sample",
                "plugin/com.dobby.sample/greet",
                "managed",
                Guid.Empty.ToString()),
            new PluginLocalizedText("title"),
            new PluginLocalizedText("failure"),
            new PluginLocalizedText("cancelled"),
            localization,
            [
                new PluginFieldGroupComponent(
                    "form",
                    new PluginLocalizedText("form"),
                    [
                        new PluginTextInputComponent("name", new PluginLocalizedText("name"), true, 80, "Ada"),
                        new PluginNumberInputComponent("quantity", new PluginLocalizedText("quantity"), true, 1, 10, 2),
                        new PluginToggleInputComponent("active", new PluginLocalizedText("active"), false, true)
                    ])
            ],
            [
                new PluginCommandDescriptor(
                    "sample.submit",
                    new PluginLocalizedText("command"),
                    "com.dobby.sample.greet.execute",
                    CanCancel: false)
            ]);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        switch (root)
        {
            case ScrollViewer viewer when viewer.Content is DependencyObject content:
                foreach (var child in Descendants<T>(content)) yield return child;
                break;
            case Panel panel:
                foreach (UIElement child in panel.Children)
                foreach (var descendant in Descendants<T>(child))
                    yield return descendant;
                break;
            case ContentControl control when control.Content is DependencyObject content:
                foreach (var child in Descendants<T>(content)) yield return child;
                break;
        }
    }

    private sealed class RecordingDispatcher(ErrorCode? failure = null) : IPluginCommandDispatcher
    {
        public List<PluginCommandRequest> Requests { get; } = [];

        public ValueTask<Result<PluginCommandResult>> DispatchAsync(
            PluginCommandRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(failure is { } error
                ? Result.Failure<PluginCommandResult>(error)
                : Result.Success(new PluginCommandResult(
                    new PluginLocalizedText("success", [request.Values["name"]]))));
        }
    }

    private sealed class CancelAwareDispatcher : IPluginCommandDispatcher
    {
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ObservedCancellation { get; private set; }

        public async ValueTask<Result<PluginCommandResult>> DispatchAsync(
            PluginCommandRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Result.Success(new PluginCommandResult());
            }
            catch (OperationCanceledException)
            {
                ObservedCancellation = true;
                return Result.Failure<PluginCommandResult>(ErrorCode.Cancelled);
            }
        }
    }

    private sealed class AnalyticsModel(string label) : IPluginPageModel
    {
        public string Label { get; } = label;
    }

    private sealed class UnknownModel : IPluginPageModel;

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
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

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
