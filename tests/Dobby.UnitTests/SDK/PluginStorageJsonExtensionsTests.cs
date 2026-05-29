using System.Text.Json;
using Dobby.Contracts.PluginStorage;
using Dobby.Core;
using Dobby.SDK.PluginStorage;
using Shouldly;

namespace Dobby.UnitTests.SDK;

public sealed class PluginStorageJsonExtensionsTests
{
    [Fact]
    public async Task SetJsonAsync_serializes_payload_and_delegates_to_compartment()
    {
        var compartment = new RecordingPluginStorageCompartment();
        var key = PluginStorageKey.Create("settings/profile").Value;
        var payload = new SamplePayload("Ada", 7);

        var result = await compartment.SetJsonAsync(key, payload);

        result.IsSuccess.ShouldBeTrue();
        compartment.LastSetKey.ShouldBe(key);
        var storedValue = compartment.LastSetValue ?? throw new InvalidOperationException("Expected stored value.");
        storedValue.ContentType.ShouldBe("application/json");
        JsonSerializer.Deserialize<SamplePayload>(storedValue.Payload.Span).ShouldBe(payload);
    }

    [Fact]
    public async Task GetJsonAsync_deserializes_payload_from_compartment()
    {
        var compartment = new RecordingPluginStorageCompartment
        {
            GetResult = Result.Success<PluginStorageValue?>(
                PluginStorageValue.FromUtf8Json("""{"Name":"Ada","Count":7}""")),
        };
        var key = PluginStorageKey.Create("settings/profile").Value;

        var result = await compartment.GetJsonAsync<SamplePayload>(key);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new SamplePayload("Ada", 7));
    }

    [Fact]
    public async Task GetJsonAsync_propagates_failed_get_result()
    {
        var compartment = new RecordingPluginStorageCompartment
        {
            GetResult = Result.Failure<PluginStorageValue?>(ErrorCode.DatabaseUnavailable),
        };
        var key = PluginStorageKey.Create("settings/profile").Value;

        var result = await compartment.GetJsonAsync<SamplePayload>(key);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
    }

    [Fact]
    public async Task GetJsonAsync_returns_serialization_failed_for_invalid_json()
    {
        var compartment = new RecordingPluginStorageCompartment
        {
            GetResult = Result.Success<PluginStorageValue?>(
                PluginStorageValue.FromUtf8Json("{not-json")),
        };
        var key = PluginStorageKey.Create("settings/profile").Value;

        var result = await compartment.GetJsonAsync<SamplePayload>(key);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.SerializationFailed);
    }

    private sealed record SamplePayload(string Name, int Count);

    private sealed class RecordingPluginStorageCompartment : IPluginStorageCompartment
    {
        public Result<PluginStorageValue?> GetResult { get; init; } = Result.Success<PluginStorageValue?>(null);

        public PluginStorageKey? LastSetKey { get; private set; }

        public PluginStorageValue? LastSetValue { get; private set; }

        public ValueTask<Result<PluginStorageValue?>> GetAsync(
            PluginStorageKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GetResult);
        }

        public ValueTask<Result> SetAsync(
            PluginStorageKey key,
            PluginStorageValue value,
            CancellationToken cancellationToken = default)
        {
            LastSetKey = key;
            LastSetValue = value;
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result<bool>> DeleteAsync(
            PluginStorageKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Result.Success(false));
        }

        public ValueTask<Result<bool>> ExistsAsync(
            PluginStorageKey key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Result.Success(false));
        }

        public ValueTask<Result<IReadOnlyList<PluginStorageKey>>> ListKeysAsync(
            PluginStorageKeyPrefix prefix,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PluginStorageKey> keys = [];
            return ValueTask.FromResult(Result.Success(keys));
        }
    }
}
