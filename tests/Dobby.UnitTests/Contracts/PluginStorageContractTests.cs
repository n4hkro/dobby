using Dobby.Contracts.PluginStorage;
using Dobby.Core;
using Shouldly;

namespace Dobby.UnitTests.Contracts;

public sealed class PluginStorageContractTests
{
    [Theory]
    [InlineData("orders/current")]
    [InlineData("settings.theme")]
    public void Plugin_storage_key_accepts_logical_relative_keys(string raw)
    {
        var result = PluginStorageKey.Create(raw);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(raw);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("/absolute")]
    [InlineData("\\absolute")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("nested/./escape")]
    [InlineData("host:reserved")]
    public void Plugin_storage_key_rejects_host_or_path_like_keys(string raw)
    {
        var result = PluginStorageKey.Create(raw);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Fact]
    public void Plugin_storage_value_keeps_payload_opaque()
    {
        var value = PluginStorageValue.FromUtf8Json("""{"name":"sample"}""", schemaVersion: 1);

        value.ContentType.ShouldBe("application/json");
        value.SchemaVersion.ShouldBe(1);
        value.Payload.ToArray().ShouldBe("""{"name":"sample"}"""u8.ToArray());
    }
}
