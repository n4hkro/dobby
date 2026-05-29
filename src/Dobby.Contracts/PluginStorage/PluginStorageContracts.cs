using System.Text;
using Dobby.Core;

namespace Dobby.Contracts.PluginStorage;

public interface IPluginStorage
{
    IPluginStorageCompartment SharedDomainStorage { get; }

    IPluginStorageCompartment UserScopedStorage { get; }
}

public interface IPluginStorageCompartment
{
    ValueTask<Result<PluginStorageValue?>> GetAsync(PluginStorageKey key, CancellationToken cancellationToken = default);

    ValueTask<Result> SetAsync(PluginStorageKey key, PluginStorageValue value, CancellationToken cancellationToken = default);

    ValueTask<Result<bool>> DeleteAsync(PluginStorageKey key, CancellationToken cancellationToken = default);

    ValueTask<Result<bool>> ExistsAsync(PluginStorageKey key, CancellationToken cancellationToken = default);

    ValueTask<Result<IReadOnlyList<PluginStorageKey>>> ListKeysAsync(PluginStorageKeyPrefix prefix, CancellationToken cancellationToken = default);
}

public readonly record struct PluginStorageKey(string Value)
{
    public static Result<PluginStorageKey> Create(string value)
    {
        return IsValid(value)
            ? Result.Success(new PluginStorageKey(value))
            : Result.Failure<PluginStorageKey>(ErrorCode.InvalidInput);
    }

    internal static bool IsValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.StartsWith("/", StringComparison.Ordinal) ||
            value.StartsWith("\\", StringComparison.Ordinal) ||
            value.StartsWith("host:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalized = value.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && !segments.Any(segment => segment is "." or "..");
    }

    public override string ToString() => Value;
}

public readonly record struct PluginStorageKeyPrefix(string Value)
{
    public static Result<PluginStorageKeyPrefix> Create(string value)
    {
        if (value.Length == 0)
        {
            return Result.Success(new PluginStorageKeyPrefix(value));
        }

        return PluginStorageKey.IsValid(value)
            ? Result.Success(new PluginStorageKeyPrefix(value))
            : Result.Failure<PluginStorageKeyPrefix>(ErrorCode.InvalidInput);
    }

    public override string ToString() => Value;
}

public sealed record PluginStorageValue(
    ReadOnlyMemory<byte> Payload,
    string? ContentType,
    int? SchemaVersion)
{
    public static PluginStorageValue FromUtf8Json(string json, int? schemaVersion = null)
    {
        return new PluginStorageValue(
            Encoding.UTF8.GetBytes(json),
            "application/json",
            schemaVersion);
    }
}
