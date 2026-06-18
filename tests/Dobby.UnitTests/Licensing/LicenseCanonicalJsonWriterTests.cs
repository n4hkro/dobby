using System.Text.Json;
using Dobby.Licensing;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicenseCanonicalJsonWriterTests
{
    [Fact]
    public void Write_orders_properties_ordinally_without_insignificant_whitespace()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "pluginId": "sample",
              "keyVersion": 1,
              "customerId": "default"
            }
            """);

        var json = Encoding.UTF8.GetString(LicenseCanonicalJsonWriter.Write(document.RootElement));

        json.ShouldBe("{\"customerId\":\"default\",\"keyVersion\":1,\"pluginId\":\"sample\"}");
    }

    [Fact]
    public void Write_recursively_orders_nested_objects_and_preserves_array_order()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "z": { "b": 1, "a": 2 },
              "a": [ { "d": 4, "c": 3 }, "tail" ]
            }
            """);

        var json = Encoding.UTF8.GetString(LicenseCanonicalJsonWriter.Write(document.RootElement));

        json.ShouldBe("{\"a\":[{\"c\":3,\"d\":4},\"tail\"],\"z\":{\"a\":2,\"b\":1}}");
    }

    [Fact]
    public void Write_keeps_numbers_numeric_and_utc_timestamps_as_strings()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "schemaVersion": 2,
              "issuedAt": "2026-06-18T00:00:00Z",
              "licenseRevision": 7
            }
            """);

        var json = Encoding.UTF8.GetString(LicenseCanonicalJsonWriter.Write(document.RootElement));

        json.ShouldContain("\"schemaVersion\":2");
        json.ShouldContain("\"licenseRevision\":7");
        json.ShouldContain("\"issuedAt\":\"2026-06-18T00:00:00Z\"");
        json.ShouldNotContain("\"schemaVersion\":\"2\"");
        json.ShouldNotContain(" ");
    }

    [Fact]
    public void Write_normalizes_strings_to_nfc()
    {
        var decomposed = "caf\u0065\u0301";
        using var document = JsonDocument.Parse($$"""{"name":"{{decomposed}}"}""");

        var json = Encoding.UTF8.GetString(LicenseCanonicalJsonWriter.Write(document.RootElement));

        json.ShouldBe("{\"name\":\"caf\u00e9\"}");
    }

    [Fact]
    public void Write_returns_utf8_without_bom()
    {
        using var document = JsonDocument.Parse("""{"keyVersion":1}""");

        var bytes = LicenseCanonicalJsonWriter.Write(document.RootElement);

        bytes.AsSpan(0, Encoding.UTF8.GetPreamble().Length)
            .SequenceEqual(Encoding.UTF8.GetPreamble())
            .ShouldBeFalse();
        Encoding.UTF8.GetString(bytes).ShouldBe("{\"keyVersion\":1}");
    }
}
