using Dobby.Licensing;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicenseArtifactParserTests
{
    [Fact]
    public void Parse_accepts_strict_v3_artifact_and_domain_separates_canonical_payload()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out _);
        var fixture = LicenseArtifactFixtureBuilder.CreateSignedArtifact(key);

        var result = LicenseArtifactParser.Parse("sample.dobbylicense", fixture.Bytes);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.Artifact!.Signature.Purpose.ShouldBe(ArtifactSignaturePurposes.PluginLicense);
        result.Artifact.Signature.Format.ShouldBe(ArtifactSignatureProfile.Format);
        result.Artifact.Signature.KeyId.ShouldBe(PluginTrustPublicKeyProvider.ProductionPluginLicenseKeyId);
        result.Artifact.Signature.Value.Length.ShouldBe(86);
        result.Artifact.CanonicalPayload.AsSpan().StartsWith("DOBBY-SIGNED-ARTIFACT\0"u8).ShouldBeTrue();
        LicensePayloadValidator.Validate(result.Artifact).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("sample.json")]
    [InlineData("sample.entitlement")]
    public void Parse_rejects_non_dobbylicense_extensions(string fileName)
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out _);
        var result = LicenseArtifactParser.Parse(fileName, LicenseArtifactFixtureBuilder.CreateSignedArtifact(key).Bytes);

        result.ReasonCode.ShouldBe(LicenseRejectReasons.InvalidLicenseFile);
    }

    [Fact]
    public void Parse_rejects_oversize_before_json_processing()
    {
        var result = LicenseArtifactParser.Parse(
            "sample.dobbylicense",
            new byte[LicenseV3Contract.MaximumArtifactBytes + 1]);

        result.ReasonCode.ShouldBe(LicenseRejectReasons.InvalidLicenseFile);
    }

    [Theory]
    [MemberData(nameof(MalformedArtifacts))]
    public void Parse_rejects_malformed_encoding_shape_duplicates_and_depth(byte[] bytes, string reason)
    {
        var result = LicenseArtifactParser.Parse("sample.dobbylicense", bytes);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe(reason);
    }

    public static IEnumerable<object[]> MalformedArtifacts()
    {
        yield return [new byte[] { 0xff }, LicenseRejectReasons.InvalidLicenseFile];
        yield return [Encoding.UTF8.GetBytes("{"), LicenseRejectReasons.InvalidLicenseFile];
        yield return [Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("{}")).ToArray(), LicenseRejectReasons.InvalidLicenseFile];
        yield return [Encoding.UTF8.GetBytes("""{"payload":{},"payload":{},"signature":{}}"""), LicenseRejectReasons.InvalidLicenseFile];
        yield return [Encoding.UTF8.GetBytes("""{"payload":{},"signature":{},"extra":true}"""), LicenseRejectReasons.InvalidLicenseFile];
        yield return [Encoding.UTF8.GetBytes("""{"payload":{},"signature":{"purpose":"dobby.plugin-license","algorithm":"ECDSA-P256-SHA256","format":"DER","keyId":"license-prod-2026-01","value":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}}"""), LicenseRejectReasons.InvalidSignature];
        yield return [Encoding.UTF8.GetBytes("""{"payload":{},"signature":{"purpose":"dobby.plugin-license","algorithm":"ECDSA-P256-SHA256","format":"IeeeP1363FixedFieldConcatenation","keyId":"license-prod-2026-01","value":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="}}"""), LicenseRejectReasons.InvalidSignature];
        var nested = new string('[', 18) + "0" + new string(']', 18);
        yield return [Encoding.UTF8.GetBytes("{\"payload\":{\"nested\":" + nested + "},\"signature\":{}}"), LicenseRejectReasons.InvalidLicenseFile];
    }
}
