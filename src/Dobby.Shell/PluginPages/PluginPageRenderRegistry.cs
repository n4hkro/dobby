using System.Windows;
using System.Windows.Controls;
using Dobby.Contracts.PluginPages;
using Dobby.Core;

namespace Dobby.Shell.PluginPages;

public sealed class PluginPageRenderRegistry
{
    private readonly Dictionary<Type, Func<IPluginPageModel, FrameworkElement>> renderers = new();

    public static PluginPageRenderRegistry CreateDefault()
    {
        var registry = new PluginPageRenderRegistry();
        registry.Register<PluginTextPageModel>(model => new TextBlock
        {
            Text = model.MessageKey,
            TextWrapping = TextWrapping.Wrap
        });
        registry.Register<PluginActionPageModel>(RenderActionPage);
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

        return Result.Success(renderer(model));
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

    private static FrameworkElement RenderActionPage(PluginActionPageModel model)
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
                Content = action.LabelKey
            };
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                try
                {
                    var result = await action.ExecuteAsync(model.Context, CancellationToken.None);
                    status.Text = result.IsSuccess
                        ? result.Value.MessageKey
                        : result.ErrorCode?.ToString() ?? ErrorCode.PluginInternalError.ToString();
                }
                catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
                {
                    status.Text = ErrorCode.PluginInternalError.ToString();
                }
                finally
                {
                    button.IsEnabled = true;
                }
            };
            panel.Children.Add(button);
        }

        panel.Children.Add(status);
        return panel;
    }
}
