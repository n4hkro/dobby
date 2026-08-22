using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Core;

namespace Dobby.Shell.PluginPages;

[PluginRuntimeActionOwner(PluginRuntimeAction.PluginAction)]
public sealed class PluginPageRenderRegistry
{
    private readonly IPluginCommandDispatcher commandDispatcher;
    private readonly Func<CultureInfo> cultureProvider;
    private readonly Dictionary<Type, Func<IPluginPageModel, FrameworkElement>> renderers = new();

    public PluginPageRenderRegistry(
        IPluginCommandDispatcher? commandDispatcher = null,
        Func<CultureInfo>? cultureProvider = null)
    {
        this.commandDispatcher = commandDispatcher ?? RejectingPluginCommandDispatcher.Instance;
        this.cultureProvider = cultureProvider ?? (() => CultureInfo.CurrentUICulture);
    }

    public static PluginPageRenderRegistry CreateDefault(
        IPluginCommandDispatcher? commandDispatcher = null,
        Func<CultureInfo>? cultureProvider = null)
    {
        var registry = new PluginPageRenderRegistry(commandDispatcher, cultureProvider);
        registry.Register<PluginTextPageModel>(model => new TextBlock
        {
            Text = model.MessageKey,
            TextWrapping = TextWrapping.Wrap
        });
        registry.Register<PluginComponentPageModel>(registry.RenderComponentPage);
        return registry;
    }

    public void Register<TModel>(Func<TModel, FrameworkElement> factory)
        where TModel : class, IPluginPageModel
    {
        ArgumentNullException.ThrowIfNull(factory);
        renderers[typeof(TModel)] = model => factory((TModel)model);
    }

    public Result<FrameworkElement> Render(IPluginPageModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model is PluginComponentPageModel componentModel)
        {
            var validation = PluginComponentModelValidator.Validate(componentModel);
            if (!validation.IsSuccess)
            {
                return Result.Failure<FrameworkElement>(validation.ErrorCode ?? ErrorCode.ValidationFailed);
            }
        }

        var modelType = model.GetType();
        var renderer = renderers
            .Where(entry => entry.Key.IsAssignableFrom(modelType))
            .OrderByDescending(entry => TypeDepth(entry.Key))
            .Select(entry => entry.Value)
            .FirstOrDefault();
        if (renderer is null)
        {
            return Result.Failure<FrameworkElement>(ErrorCode.NotFound);
        }

        try
        {
            return Result.Success(renderer(model));
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<FrameworkElement>(ErrorCode.Cancelled);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or FormatException)
        {
            return Result.Failure<FrameworkElement>(ErrorCode.ValidationFailed);
        }
        catch (NotSupportedException)
        {
            return Result.Failure<FrameworkElement>(ErrorCode.UnsupportedOperation);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure<FrameworkElement>(ErrorCode.PluginInternalError);
        }
    }

    private FrameworkElement RenderComponentPage(PluginComponentPageModel model)
    {
        var panel = new StackPanel { Margin = new Thickness(16) };
        PopulateComponentPage(panel, model);
        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false
        };
    }

    private void PopulateComponentPage(StackPanel panel, PluginComponentPageModel model)
    {
        panel.Children.Clear();
        var culture = cultureProvider();
        var title = Resolve(model, model.Title, culture);
        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        AutomationProperties.SetName(titleBlock, title);
        AutomationProperties.SetHeadingLevel(titleBlock, AutomationHeadingLevel.Level1);
        panel.Children.Add(titleBlock);

        var status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Assertive);
        AutomationProperties.SetName(status, title);

        var valueReaders = new Dictionary<string, Func<Result<string>>>(StringComparer.Ordinal);
        foreach (var component in model.Components)
        {
            panel.Children.Add(RenderComponent(component, model, culture, valueReaders, panel, status));
        }

        var commandPanel = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        foreach (var command in model.Commands.Where(command => command.IsVisible))
        {
            AddCommandButtons(commandPanel, status, model, command, valueReaders, pageRequest: null, panel);
        }

        if (commandPanel.Children.Count > 0)
        {
            panel.Children.Add(commandPanel);
        }
        panel.Children.Add(status);
    }

    private FrameworkElement RenderComponent(
        PluginComponent component,
        PluginComponentPageModel model,
        CultureInfo culture,
        IDictionary<string, Func<Result<string>>> valueReaders,
        StackPanel pagePanel,
        TextBlock status)
    {
        return component switch
        {
            PluginTextComponent text => RenderText(Resolve(model, text.Text, culture)),
            PluginHeadingComponent heading => RenderHeading(Resolve(model, heading.Text, culture), heading.Level),
            PluginFieldGroupComponent group => RenderFieldGroup(group, model, culture, valueReaders),
            PluginValidationSummaryComponent summary => RenderValidationSummary(summary, model, culture),
            PluginTableComponent table => RenderTable(table, model, culture, valueReaders, pagePanel, status),
            PluginListComponent list => RenderList(list, model, culture),
            _ => throw new NotSupportedException("Unknown plugin component type.")
        };
    }

    private static TextBlock RenderText(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 4)
        };
        AutomationProperties.SetName(block, text);
        return block;
    }

    private static TextBlock RenderHeading(string text, int level)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = level switch { 1 => 22, 2 => 18, _ => 16 },
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 6)
        };
        AutomationProperties.SetName(block, text);
        AutomationProperties.SetHeadingLevel(block, level switch
        {
            1 => AutomationHeadingLevel.Level1,
            2 => AutomationHeadingLevel.Level2,
            _ => AutomationHeadingLevel.Level3
        });
        return block;
    }

    private FrameworkElement RenderFieldGroup(
        PluginFieldGroupComponent group,
        PluginComponentPageModel model,
        CultureInfo culture,
        IDictionary<string, Func<Result<string>>> valueReaders)
    {
        var content = new StackPanel();
        foreach (var input in group.Inputs)
        {
            content.Children.Add(RenderInput(input, model, culture, valueReaders));
        }

        var title = Resolve(model, group.Title, culture);
        var result = new GroupBox
        {
            Header = title,
            Content = content,
            Margin = new Thickness(0, 6, 0, 6),
            Padding = new Thickness(8)
        };
        AutomationProperties.SetName(result, title);
        return result;
    }

    private FrameworkElement RenderInput(
        PluginInputComponent input,
        PluginComponentPageModel model,
        CultureInfo culture,
        IDictionary<string, Func<Result<string>>> valueReaders)
    {
        var labelText = Resolve(model, input.Label, culture);
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
        var label = new Label { Content = labelText, Padding = new Thickness(0, 0, 0, 2) };
        FrameworkElement editor;
        Func<Result<string>> reader;

        switch (input)
        {
            case PluginTextInputComponent text:
                var textBox = new TextBox { Text = text.InitialValue ?? string.Empty, MaxLength = text.MaximumLength };
                editor = textBox;
                reader = () => Required(input, textBox.Text);
                break;
            case PluginNumberInputComponent number:
                var numberBox = new TextBox
                {
                    Text = number.InitialValue?.ToString(culture) ?? string.Empty
                };
                editor = numberBox;
                reader = () => ReadNumber(number, numberBox.Text, culture);
                break;
            case PluginDateInputComponent date:
                var datePicker = new DatePicker
                {
                    SelectedDate = date.InitialValue is { } initial
                        ? initial.ToDateTime(TimeOnly.MinValue)
                        : null
                };
                editor = datePicker;
                reader = () => ReadDate(date, datePicker.SelectedDate);
                break;
            case PluginChoiceInputComponent choice:
                var choiceBox = new ComboBox
                {
                    ItemsSource = choice.Options.Select(option => new ChoiceView(
                        option.Value,
                        Resolve(model, option.Label, culture))).ToArray(),
                    DisplayMemberPath = nameof(ChoiceView.Label),
                    SelectedValuePath = nameof(ChoiceView.Value),
                    SelectedValue = choice.InitialValue
                };
                editor = choiceBox;
                reader = () => Required(input, choiceBox.SelectedValue as string ?? string.Empty);
                break;
            case PluginToggleInputComponent toggle:
                var toggleBox = new CheckBox { IsChecked = toggle.InitialValue };
                editor = toggleBox;
                reader = () => Result.Success(toggleBox.IsChecked == true ? "true" : "false");
                break;
            default:
                throw new NotSupportedException("Unknown plugin input type.");
        }

        AutomationProperties.SetName(editor, labelText);
        AutomationProperties.SetHelpText(editor, labelText);
        label.Target = editor;
        panel.Children.Add(label);
        panel.Children.Add(editor);
        valueReaders.Add(input.ComponentId, reader);
        return panel;
    }

    private FrameworkElement RenderValidationSummary(
        PluginValidationSummaryComponent summary,
        PluginComponentPageModel model,
        CultureInfo culture)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
        AutomationProperties.SetLiveSetting(panel, AutomationLiveSetting.Assertive);
        foreach (var message in summary.Messages)
        {
            var text = Resolve(model, message.Message, culture);
            panel.Children.Add(RenderText(text));
        }
        return panel;
    }

    private FrameworkElement RenderTable(
        PluginTableComponent table,
        PluginComponentPageModel model,
        CultureInfo culture,
        IDictionary<string, Func<Result<string>>> valueReaders,
        StackPanel pagePanel,
        TextBlock status)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
        var caption = Resolve(model, table.Caption, culture);
        panel.Children.Add(RenderHeading(caption, 2));
        var gridView = new GridView();
        foreach (var column in table.Columns)
        {
            var columnId = column.ColumnId;
            gridView.Columns.Add(new GridViewColumn
            {
                Header = Resolve(model, column.Header, culture),
                DisplayMemberBinding = new System.Windows.Data.Binding($"Cells[{columnId}]")
            });
        }

        var listView = new ListView
        {
            View = gridView,
            ItemsSource = table.Rows,
            MaxHeight = 360
        };
        AutomationProperties.SetName(listView, caption);
        panel.Children.Add(listView);

        if (table.PageCommandId is not null)
        {
            var command = model.Commands.Single(item => item.CommandId.Equals(table.PageCommandId, StringComparison.Ordinal));
            var paging = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            var previous = new Button
            {
                Content = Resolve(model, table.PreviousPageLabel!, culture),
                IsEnabled = table.Page.PageIndex > 0,
                Margin = new Thickness(0, 0, 6, 0)
            };
            var next = new Button
            {
                Content = Resolve(model, table.NextPageLabel!, culture),
                IsEnabled = (table.Page.PageIndex + 1) * table.Page.PageSize < table.TotalRowCount
            };
            AutomationProperties.SetName(previous, previous.Content.ToString()!);
            AutomationProperties.SetName(next, next.Content.ToString()!);
            previous.Click += async (_, _) => await ExecuteCommandAsync(
                previous,
                null,
                model,
                command,
                valueReaders,
                table.Page with { PageIndex = table.Page.PageIndex - 1 },
                pagePanel,
                status).ConfigureAwait(true);
            next.Click += async (_, _) => await ExecuteCommandAsync(
                next,
                null,
                model,
                command,
                valueReaders,
                table.Page with { PageIndex = table.Page.PageIndex + 1 },
                pagePanel,
                status).ConfigureAwait(true);
            paging.Children.Add(previous);
            paging.Children.Add(next);
            panel.Children.Add(paging);
        }

        return panel;
    }

    private FrameworkElement RenderList(
        PluginListComponent list,
        PluginComponentPageModel model,
        CultureInfo culture)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
        var caption = Resolve(model, list.Caption, culture);
        panel.Children.Add(RenderHeading(caption, 2));
        var listBox = new ListBox
        {
            ItemsSource = list.Items.Select(item => Resolve(model, item.Text, culture)).ToArray()
        };
        AutomationProperties.SetName(listBox, caption);
        panel.Children.Add(listBox);
        return panel;
    }

    private void AddCommandButtons(
        Panel commandPanel,
        TextBlock status,
        PluginComponentPageModel model,
        PluginCommandDescriptor command,
        IDictionary<string, Func<Result<string>>> valueReaders,
        PluginPageRequest? pageRequest,
        StackPanel pagePanel)
    {
        var culture = cultureProvider();
        var execute = new Button
        {
            Content = Resolve(model, command.Label, culture),
            Margin = new Thickness(0, 0, 6, 0)
        };
        AutomationProperties.SetName(execute, execute.Content.ToString()!);
        Button? cancel = null;
        if (command.CanCancel)
        {
            cancel = new Button
            {
                Content = Resolve(model, command.CancelLabel!, culture),
                IsEnabled = false,
                Margin = new Thickness(0, 0, 6, 0)
            };
            AutomationProperties.SetName(cancel, cancel.Content.ToString()!);
        }

        var cancelButton = cancel;
        execute.Click += async (_, _) => await ExecuteCommandAsync(
            execute,
            cancelButton,
            model,
            command,
            valueReaders,
            pageRequest,
            pagePanel,
            status).ConfigureAwait(true);
        commandPanel.Children.Add(execute);
        if (cancel is not null)
        {
            commandPanel.Children.Add(cancel);
        }
    }

    private async Task ExecuteCommandAsync(
        Button execute,
        Button? cancel,
        PluginComponentPageModel model,
        PluginCommandDescriptor command,
        IDictionary<string, Func<Result<string>>> valueReaders,
        PluginPageRequest? pageRequest,
        StackPanel pagePanel,
        TextBlock? status = null)
    {
        status ??= pagePanel.Children.OfType<TextBlock>().LastOrDefault() ?? new TextBlock();
        var values = ReadValues(valueReaders);
        if (!values.IsSuccess)
        {
            SetStatus(status, Resolve(model, model.FailureText, cultureProvider()));
            return;
        }

        using var cancellation = new CancellationTokenSource();
        RoutedEventHandler? cancelHandler = null;
        if (cancel is not null)
        {
            cancelHandler = (_, _) => cancellation.Cancel();
            cancel.Click += cancelHandler;
        }

        execute.IsEnabled = false;
        if (cancel is not null) cancel.IsEnabled = true;
        try
        {
            var request = new PluginCommandRequest(
                model.Context.PluginId,
                model.Context.RouteKey,
                model.Context.UserId,
                model.Context.CorrelationId,
                command.CommandId,
                command.OperationKey,
                values.Value,
                pageRequest);
            var result = await commandDispatcher.DispatchAsync(
                request,
                TimeSpan.FromSeconds(command.TimeoutSeconds),
                cancellation.Token).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                var text = result.ErrorCode == ErrorCode.Cancelled
                    ? Resolve(model, model.CancelledText, cultureProvider())
                    : Resolve(model, model.FailureText, cultureProvider());
                SetStatus(status, text);
                return;
            }

            if (result.Value.UpdatedPage is { } updatedPage &&
                updatedPage.Context.PluginId.Equals(model.Context.PluginId, StringComparison.Ordinal) &&
                updatedPage.Context.RouteKey.Equals(model.Context.RouteKey, StringComparison.Ordinal) &&
                PluginComponentModelValidator.Validate(updatedPage).IsSuccess)
            {
                PopulateComponentPage(pagePanel, updatedPage);
                return;
            }

            if (result.Value.ValidationMessages is { Count: > 0 } messages)
            {
                SetStatus(status, string.Join(Environment.NewLine, messages.Select(message =>
                    Resolve(model, message.Message, cultureProvider()))));
                return;
            }

            SetStatus(
                status,
                result.Value.Message is { } message
                    ? Resolve(model, message, cultureProvider())
                    : string.Empty);
        }
        catch (OperationCanceledException)
        {
            SetStatus(status, Resolve(model, model.CancelledText, cultureProvider()));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            SetStatus(status, Resolve(model, model.FailureText, cultureProvider()));
        }
        finally
        {
            if (cancel is not null)
            {
                if (cancelHandler is not null) cancel.Click -= cancelHandler;
                cancel.IsEnabled = false;
            }
            execute.IsEnabled = true;
        }
    }

    private static Result<IReadOnlyDictionary<string, string>> ReadValues(
        IDictionary<string, Func<Result<string>>> readers)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var reader in readers)
        {
            var value = reader.Value();
            if (!value.IsSuccess)
            {
                return Result.Failure<IReadOnlyDictionary<string, string>>(
                    value.ErrorCode ?? ErrorCode.ValidationFailed);
            }
            values.Add(reader.Key, value.Value);
        }
        return Result.Success<IReadOnlyDictionary<string, string>>(values);
    }

    private static Result<string> Required(PluginInputComponent input, string value)
    {
        return input.IsRequired && string.IsNullOrWhiteSpace(value)
            ? Result.Failure<string>(ErrorCode.ValidationFailed)
            : Result.Success(value);
    }

    private static Result<string> ReadNumber(
        PluginNumberInputComponent input,
        string value,
        CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Required(input, value);
        }
        if (!decimal.TryParse(value, NumberStyles.Number, culture, out var number) ||
            (input.Minimum is { } minimum && number < minimum) ||
            (input.Maximum is { } maximum && number > maximum))
        {
            return Result.Failure<string>(ErrorCode.ValidationFailed);
        }
        return Result.Success(number.ToString(CultureInfo.InvariantCulture));
    }

    private static Result<string> ReadDate(PluginDateInputComponent input, DateTime? selectedDate)
    {
        if (selectedDate is null)
        {
            return input.IsRequired
                ? Result.Failure<string>(ErrorCode.ValidationFailed)
                : Result.Success(string.Empty);
        }
        var date = DateOnly.FromDateTime(selectedDate.Value);
        if ((input.Minimum is { } minimum && date < minimum) ||
            (input.Maximum is { } maximum && date > maximum))
        {
            return Result.Failure<string>(ErrorCode.ValidationFailed);
        }
        return Result.Success(date.ToString("O", CultureInfo.InvariantCulture));
    }

    private static void SetStatus(TextBlock status, string text)
    {
        status.Text = text;
        AutomationProperties.SetName(status, text);
        UIElementAutomationPeer.FromElement(status)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private static string Resolve(
        PluginComponentPageModel model,
        PluginLocalizedText text,
        CultureInfo culture)
    {
        var result = PluginLocalizationResolver.Resolve(model.Localization, text, culture);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Plugin localization could not be resolved.");
        }
        return result.Value;
    }

    private static int TypeDepth(Type type)
    {
        var depth = 0;
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            depth++;
        }
        return depth;
    }

    private sealed record ChoiceView(string Value, string Label);

    private sealed class RejectingPluginCommandDispatcher : IPluginCommandDispatcher
    {
        public static RejectingPluginCommandDispatcher Instance { get; } = new();

        public ValueTask<Result<PluginCommandResult>> DispatchAsync(
            PluginCommandRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Failure<PluginCommandResult>(ErrorCode.LicenseDenied));
    }
}
