using System.Reflection;
using System.Runtime.Loader;
using Dobby.Contracts.PluginFramework;

namespace Dobby.PluginFramework.Activation;

public sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dobby.Contracts",
        "Dobby.Core",
        "Dobby.SDK",
        "System.Text.Json"
    };

    private readonly AssemblyDependencyResolver resolver;

    public PluginLoadContext(string pluginId, string entryAssemblyPath)
        : base($"Dobby.Plugin:{pluginId}", isCollectible: false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryAssemblyPath);

        resolver = new AssemblyDependencyResolver(Path.GetFullPath(entryAssemblyPath));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsShared(assemblyName.Name))
        {
            return Default.Assemblies.FirstOrDefault(assembly =>
                    AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName))
                ?? Default.LoadFromAssemblyName(assemblyName);
        }

        var assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }

    private static bool IsShared(string? assemblyName)
    {
        return assemblyName is not null &&
            (SharedAssemblyNames.Contains(assemblyName) ||
             assemblyName.StartsWith("System.", StringComparison.Ordinal) ||
             assemblyName.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal));
    }
}
