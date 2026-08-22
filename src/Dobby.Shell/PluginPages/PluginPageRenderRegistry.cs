using System.Windows;
using System.Windows.Controls;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Contracts.Licensing;

namespace Dobby.Shell.PluginPages;

[PluginRuntimeActionOwner(PluginRuntimeAction.PluginAction)]
public sealed class PluginPageRenderRegistry
{
    private readonly Action<string, string, string, ErrorCode>? actionFaultSink;
    private readonly Func<string, Result> actionExecutionGate;
    private readonly Dictionary<Type, Func<IPluginPageModel, FrameworkElement>> renderers = new();

    public PluginPageRenderRegistry(
        Action<string, string, string, ErrorCode>? actionFaultSink = null,
        Func<string, Result>? actionExecutionGate = null)
    {
        this.actionFaultSink = actionFaultSink;
        this.actionExecutionGate = actionExecutionGate ??
            (_ => Result.Failure(ErrorCode.LicenseDenied));
    }

    public static PluginPageRenderRegistry CreateDefault(
        Func<string, Result> actionExecutionGate,
        Action<string, string, string, ErrorCode>? actionFaultSink = null)
    {
        ArgumentNullException.ThrowIfNull(actionExecutionGate);

        var registry = new PluginPageRenderRegistry(actionFaultSink, actionExecutionGate);
        registry.Register<PluginTextPageModel>(model => new TextBlock
        {
            Text = model.MessageKey,
            TextWrapping = TextWrapping.Wrap
        });
        registry.Register<PluginActionPageModel>(registry.RenderActionPage);
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
        catch (ArgumentException)
        {
            return Result.Failure<FrameworkElement>(ErrorCode.InvalidInput);
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

    private static int TypeDepth(Type type)
    {
        var depth = 0;
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            depth++;
        }

        return depth;
    }

    private FrameworkElement RenderActionPage(PluginActionPageModel model)
    {
        var panel = new StackPanel();
        var message = new TextBlock
        {
            Text = model.MessageKey,
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(message);

        var status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };

        foreach (var action in model.Actions)
        {
            var button = new Button
            {
                Content = action.LabelKey,
                IsEnabled = actionExecutionGate(model.Context.PluginId).IsSuccess
            };
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                try
                {
                    var gate = actionExecutionGate(model.Context.PluginId);
                    var result = gate.IsSuccess
                        ? await ExecutePageActionAsync(action, model.Context).ConfigureAwait(true)
                        : Result.Failure<PluginPageActionResult>(gate.ErrorCode ?? ErrorCode.LicenseDenied);
                    status.Text = result.IsSuccess
                        ? result.Value.MessageKey
                        : result.ErrorCode?.ToString() ?? ErrorCode.PluginInternalError.ToString();
                    if (!result.IsSuccess)
                    {
                        RecordActionFault(action, model.Context, result.ErrorCode ?? ErrorCode.PluginInternalError);
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
                {
                    status.Text = ErrorCode.PluginInternalError.ToString();
                    RecordActionFault(action, model.Context, ErrorCode.PluginInternalError);
                }
                finally
                {
                    button.IsEnabled = actionExecutionGate(model.Context.PluginId).IsSuccess;
                }
            };
            panel.Children.Add(button);
        }

        panel.Children.Add(status);
        return panel;
    }

    private void RecordActionFault(
        PluginPageAction action,
        PluginPageActionContext context,
        ErrorCode errorCode)
    {
        if (errorCode == ErrorCode.Cancelled)
        {
            return;
        }

        actionFaultSink?.Invoke(
            context.PluginId,
            context.RouteKey,
            action.ActionId,
            errorCode);
    }

    private static async Task<Result<PluginPageActionResult>> ExecutePageActionAsync(
        PluginPageAction action,
        PluginPageActionContext context)
    {
        try
        {
            return await action.ExecuteAsync(context, CancellationToken.None).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<PluginPageActionResult>(ErrorCode.Cancelled);
        }
        catch (ArgumentException)
        {
            return Result.Failure<PluginPageActionResult>(ErrorCode.InvalidInput);
        }
        catch (NotSupportedException)
        {
            return Result.Failure<PluginPageActionResult>(ErrorCode.UnsupportedOperation);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure<PluginPageActionResult>(ErrorCode.PluginInternalError);
        }
    }
}
