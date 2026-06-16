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
}
