using Dobby.Licensing;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicenseArtifactParserTests
{
    [Fact]
    public void Parse_accepts_dobbylicense_artifact_and_extracts_canonical_payload_bytes()
    {
        var artifact = Encoding.UTF8.GetBytes(
            """
            {
              "signature": {
                "value": "AQID",
                "keyVersion": 1,
                "algorithm": "ECDSA-P256-SHA256"
              },
              "payload": {
                "pluginId": "plugin.sample",
                "schemaVersion": 2,
                "keyVersion": 1
              }
            }
            """);

        var result = LicenseArtifactParser.Parse("sample.dobbylicense", artifact);

        result.IsSuccess.ShouldBeTrue();
        result.ReasonCode.ShouldBeEmpty();
        result.Artifact.ShouldNotBeNull();
        result.Artifact.Signature.Algorithm.ShouldBe("ECDSA-P256-SHA256");
        result.Artifact.Signature.KeyVersion.ShouldBe(1);
        result.Artifact.Signature.Value.ShouldBe("AQID");
        Encoding.UTF8.GetString(result.Artifact.CanonicalPayload).ShouldBe(
            "{\"keyVersion\":1,\"pluginId\":\"plugin.sample\",\"schemaVersion\":2}");
    }

    [Theory]
    [InlineData("sample.json")]
    [InlineData("sample.entitlement")]
    public void Parse_rejects_non_dobbylicense_extensions(string fileName)
    {
        var result = LicenseArtifactParser.Parse(fileName, MinimalArtifactBytes());

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-license-file");
    }

    [Fact]
    public void Parse_rejects_invalid_utf8()
    {
        var result = LicenseArtifactParser.Parse("sample.dobbylicense", [0xff]);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-license-file");
    }

    [Fact]
    public void Parse_rejects_malformed_json()
    {
        var result = LicenseArtifactParser.Parse("sample.dobbylicense", Encoding.UTF8.GetBytes("{"));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-license-file");
    }

    [Fact]
    public void Parse_rejects_utf8_bom()
    {
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(MinimalArtifactBytes())
            .ToArray();

        var result = LicenseArtifactParser.Parse("sample.dobbylicense", bytes);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-license-file");
    }

    [Fact]
    public void Parse_rejects_root_extra_properties_before_signature_verification()
    {
        var result = LicenseArtifactParser.Parse(
            "sample.dobbylicense",
            Encoding.UTF8.GetBytes(
                """
                {
                  "payload": {},
                  "signature": {
                    "algorithm": "ECDSA-P256-SHA256",
                    "keyVersion": 1,
                    "value": "AQID"
                  },
                  "extra": true
                }
                """));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-license-file");
    }

    [Theory]
    [InlineData("""{"signature":{"algorithm":"ECDSA-P256-SHA256","keyVersion":1,"value":"AQID"}}""")]
    [InlineData("""{"payload":{}}""")]
    public void Parse_rejects_missing_root_payload_or_signature(string json)
    {
        var result = LicenseArtifactParser.Parse("sample.dobbylicense", Encoding.UTF8.GetBytes(json));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-license-file");
    }

    [Theory]
    [InlineData("RSA-SHA256", "1", "AQID")]
    [InlineData("ECDSA-P256-SHA256", "\"1\"", "AQID")]
    [InlineData("ECDSA-P256-SHA256", "2", "AQID")]
    [InlineData("ECDSA-P256-SHA256", "1", "AQID==")]
    [InlineData("ECDSA-P256-SHA256", "1", "not-valid@")]
    public void Parse_rejects_invalid_signature_block_shape(
        string algorithm,
        string keyVersionJson,
        string value)
    {
        var json =
            $$"""
              {
                "payload": {},
                "signature": {
                  "algorithm": "{{algorithm}}",
                  "keyVersion": {{keyVersionJson}},
                  "value": "{{value}}"
                }
              }
              """;

        var result = LicenseArtifactParser.Parse("sample.dobbylicense", Encoding.UTF8.GetBytes(json));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-signature");
    }

    private static byte[] MinimalArtifactBytes()
    {
        return Encoding.UTF8.GetBytes(
            """
            {
              "payload": {},
              "signature": {
                "algorithm": "ECDSA-P256-SHA256",
                "keyVersion": 1,
                "value": "AQID"
              }
            }
            """);
    }
}
