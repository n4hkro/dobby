using System.IO;
using Dobby.Core;

namespace Dobby.Host.Plugins;

public interface IPluginDataRetentionStore
{
    Result DeletePluginData(string pluginId);
}

public sealed class PluginDataRetentionStore(string runtimeDataRoot) : IPluginDataRetentionStore
{
    private readonly string storageRoot = Path.GetFullPath(Path.Combine(
        string.IsNullOrWhiteSpace(runtimeDataRoot) ? "." : runtimeDataRoot,
        "plugins",
        "storage"));

    public Result DeletePluginData(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }

        var safePluginId = ValidatePluginId(pluginId);
        if (safePluginId is null)
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }

        try
        {
            DeleteCompartment("shared-domain", safePluginId);
            DeleteCompartment("user-scoped", safePluginId);
            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(ErrorCode.IoFailed);
        }
    }

    private void DeleteCompartment(string compartment, string safePluginId)
    {
        var path = Path.GetFullPath(Path.Combine(storageRoot, compartment, safePluginId));
        if (!IsUnderStorageRoot(path))
        {
            throw new IOException("Plugin data path escaped the storage root.");
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private bool IsUnderStorageRoot(string path)
    {
        var root = storageRoot.EndsWith(Path.DirectorySeparatorChar)
            ? storageRoot
            : storageRoot + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ValidatePluginId(string pluginId)
    {
        foreach (var character in pluginId)
        {
            if (!char.IsLetterOrDigit(character) && character is not ('.' or '-' or '_'))
            {
                return null;
            }
        }

        return pluginId;
    }
}

public sealed class NoopPluginDataRetentionStore : IPluginDataRetentionStore
{
    public static NoopPluginDataRetentionStore Instance { get; } = new();

    private NoopPluginDataRetentionStore()
    {
    }

    public Result DeletePluginData(string pluginId)
    {
        return Result.Success();
    }
}
