namespace Dobby.Navigation;

public static class PluginRouteNormalizer
{
    public static string Normalize(string pluginId, string routeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);

        var normalizedRouteKey = routeKey.Trim('/');
        if (normalizedRouteKey.Length == 0)
        {
            throw new ArgumentException("Route key must contain a non-empty segment.", nameof(routeKey));
        }

        var pluginPrefix = $"plugin/{pluginId}/";
        if (normalizedRouteKey.StartsWith(pluginPrefix, StringComparison.Ordinal))
        {
            if (normalizedRouteKey.Length == pluginPrefix.Length)
            {
                throw new ArgumentException("Route key must contain a non-empty segment.", nameof(routeKey));
            }

            return normalizedRouteKey;
        }

        if (normalizedRouteKey.StartsWith("plugin/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Normalized plugin route must match the owning plugin id.", nameof(routeKey));
        }

        return $"plugin/{pluginId}/{normalizedRouteKey}";
    }
}
