using System.Text.Json;
using Dobby.Contracts.PluginStorage;
using Dobby.Core;

namespace Dobby.SDK.PluginStorage;

public static class PluginStorageJsonExtensions
{
    public static async ValueTask<Result> SetJsonAsync<T>(
        this IPluginStorageCompartment compartment,
        PluginStorageKey key,
        T value,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, options);
        return await compartment.SetAsync(key, PluginStorageValue.FromUtf8Json(json), cancellationToken);
    }

    public static async ValueTask<Result<T?>> GetJsonAsync<T>(
        this IPluginStorageCompartment compartment,
        PluginStorageKey key,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var stored = await compartment.GetAsync(key, cancellationToken);
        if (!stored.IsSuccess)
        {
            return Result.Failure<T?>(stored.ErrorCode ?? ErrorCode.SerializationFailed);
        }

        if (stored.Value is null)
        {
            return Result.Success<T?>(default);
        }

        try
        {
            return Result.Success(JsonSerializer.Deserialize<T>(stored.Value.Payload.Span, options));
        }
        catch (JsonException)
        {
            return Result.Failure<T?>(ErrorCode.SerializationFailed);
        }
    }
}
