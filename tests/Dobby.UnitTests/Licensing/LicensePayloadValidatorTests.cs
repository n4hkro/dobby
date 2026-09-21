using System.Text.Json;
using Dobby.Licensing;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicensePayloadValidatorTests
{
    [Fact]
    public void Validate_accepts_bounded_v3_grant_payload()
    {
        var result = LicensePayloadValidator.Validate(CreateArtifact());

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.Payload!.SchemaVersion.ShouldBe(3);
        result.Payload.Purpose.ShouldBe(ArtifactSignaturePurposes.PluginLicense);
        result.Payload.Intent.ShouldBe(LicenseGrantIntent.Granted);
        result.Payload.RevisionSeriesId.ShouldBe("series-1");
        result.Payload.LicenseRevision.ShouldBe(7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(LicenseV3Contract.MaximumGracePeriodDays)]
    public void Validate_accepts_grace_period_at_documented_bounds(int gracePeriodDays)
    {
        var result = LicensePayloadValidator.Validate(
            CreateArtifact(payload => payload["gracePeriodDays"] = gracePeriodDays));

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.Payload!.GracePeriodDays.ShouldBe(gracePeriodDays);
    }

    [Theory]
    [InlineData("schemaVersion", 2)]
    [InlineData("purpose", "dobby.branch-setup-authorization")]
    [InlineData("deviceKeyHash", "ABCDEF")]
    [InlineData("packageFingerprint", "abc")]
    [InlineData("licenseRevision", 0)]
    [InlineData("gracePeriodDays", -1)]
    [InlineData("gracePeriodDays", 91)]
    [InlineData("gracePeriodDays", 366)]
    [InlineData("intent", "active")]
    public void Validate_rejects_invalid_v3_authority_values(string propertyName, object value)
    {
        var result = LicensePayloadValidator.Validate(CreateArtifact(payload => payload[propertyName] = value));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe(LicenseRejectReasons.InvalidLicenseFile);
    }

    [Fact]
    public void Validate_rejects_payload_key_id_that_differs_from_signature_key_id()
    {
        var result = LicensePayloadValidator.Validate(CreateArtifact(payload => payload["keyId"] = "license-prod-other"));

        result.ReasonCode.ShouldBe(LicenseRejectReasons.InvalidSignature);
    }

    [Fact]
    public void Validate_rejects_missing_extra_and_duplicate_payload_fields()
    {
        LicensePayloadValidator.Validate(CreateArtifact(payload => payload.Remove("licenseId"))).IsSuccess.ShouldBeFalse();
        LicensePayloadValidator.Validate(CreateArtifact(payload => payload["unexpected"] = true)).IsSuccess.ShouldBeFalse();

        const string duplicate = """
            {"schemaVersion":3,"schemaVersion":3,"purpose":"dobby.plugin-license","keyId":"license-prod-2026-01","licenseId":"license-1","revisionSeriesId":"series-1","licenseRevision":7,"pluginId":"plugin.sample","customerId":"default","deploymentId":"default","deviceKeyHash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","packageFingerprint":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","issuedAt":"2026-06-18T00:00:00Z","notBefore":"2026-06-18T00:00:00Z","expiresAt":"2026-07-18T00:00:00Z","gracePeriodDays":14,"intent":"granted"}
            """;
        using var document = JsonDocument.Parse(duplicate);
        var artifact = CreateArtifact(document.RootElement.Clone());
        LicensePayloadValidator.Validate(artifact).IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData("issuedAt", "2026-06-18T00:00:00+04:00")]
    [InlineData("issuedAt", "2026-06-18T00:00:00.12345678Z")]
    [InlineData("expiresAt", "9999-12-31T23:59:59Z")]
    public void Validate_rejects_noncanonical_or_grace_overflowing_timestamps(string field, string value)
    {
        var result = LicensePayloadValidator.Validate(CreateArtifact(payload => payload[field] = value));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe(LicenseRejectReasons.InvalidLicenseFile);
    }

    [Fact]
    public void Validate_accepts_only_granted_or_revoked_intent()
    {
        LicensePayloadValidator.Validate(CreateArtifact(payload => payload["intent"] = "granted")).Payload!.Intent
            .ShouldBe(LicenseGrantIntent.Granted);
        LicensePayloadValidator.Validate(CreateArtifact(payload => payload["intent"] = "revoked")).Payload!.Intent
            .ShouldBe(LicenseGrantIntent.Revoked);
    }

    [Fact]
    public void Required_payload_fields_match_v3_schema()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(CanonicalLicenseSchemaPath()));
        var required = document.RootElement.GetProperty("properties").GetProperty("payload")
            .GetProperty("required").EnumerateArray().Select(value => value.GetString()!).ToArray();

        LicenseArtifactFixtureBuilder.ValidPayload().Keys.ShouldBe(required);
    }

    [Fact]
    public void Validate_rejects_a_v2_shaped_payload_because_no_compatibility_reader_exists()
    {
        var v2Payload = LicenseArtifactFixtureBuilder.ValidPayload();
        v2Payload["schemaVersion"] = 2;
        v2Payload.Remove("intent");
        v2Payload["status"] = "active";

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(v2Payload));
        var result = LicensePayloadValidator.Validate(CreateArtifact(document.RootElement.Clone()));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe(
            LicenseRejectReasons.InvalidLicenseFile,
            "docs/specs/license-import.md declares the v2 compatibility reader unimplemented, so a " +
            "v2-shaped payload must be rejected fail-closed and its 'status' value must never be " +
            "normalized into a runtime mode");
    }

    private static LicenseArtifact CreateArtifact(Action<Dictionary<string, object?>>? mutate = null)
    {
        var payload = LicenseArtifactFixtureBuilder.ValidPayload();
        mutate?.Invoke(payload);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        return CreateArtifact(document.RootElement.Clone());
    }

    private static LicenseArtifact CreateArtifact(JsonElement payload)
    {
        var canonicalJson = LicenseCanonicalJsonWriter.Write(payload);
        return new LicenseArtifact(
            payload,
            new LicenseSignatureBlock(
                ArtifactSignaturePurposes.PluginLicense,
                ArtifactSignatureProfile.Algorithm,
                ArtifactSignatureProfile.Format,
                PluginTrustPublicKeyProvider.ProductionPluginLicenseKeyId,
                ArtifactSignatureCodec.ToUnpaddedBase64Url(new byte[ArtifactSignatureProfile.P256SignatureLength])),
            ArtifactCanonicalPayload.Compose(
                ArtifactSignaturePurposes.PluginLicense,
                LicenseV3Contract.SchemaVersion,
                canonicalJson));
    }

    private static string CanonicalLicenseSchemaPath() => Path.Combine(
        RepoRoot().FullName,
        "docs",
        "schemas",
        "dobby-plugin-license-v3.schema.json");

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
