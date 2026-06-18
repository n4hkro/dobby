using System.Text.Json;
using Dobby.Licensing;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicensePayloadValidatorTests
{
    [Fact]
    public void Validate_accepts_schema_v2_payload_after_signature_shape_is_known()
    {
        var artifact = CreateArtifact();

        var result = LicensePayloadValidator.Validate(artifact);

        result.IsSuccess.ShouldBeTrue();
        result.ReasonCode.ShouldBeEmpty();
        result.Payload.ShouldNotBeNull();
        result.Payload.SchemaVersion.ShouldBe(2);
        result.Payload.PluginId.ShouldBe("plugin.sample");
        result.Payload.Status.ShouldBe(LicenseArtifactStatus.Active);
        result.Payload.LicenseRevision.ShouldBe(7);
    }

    [Theory]
    [InlineData("schemaVersion", 1)]
    [InlineData("deviceFingerprintAlgorithm", "v2")]
    [InlineData("deviceFingerprint", "ABCDEF")]
    [InlineData("packageFingerprint", "abc")]
    [InlineData("licenseRevision", 0)]
    [InlineData("gracePeriodDays", -1)]
    [InlineData("status", "grace")]
    [InlineData("keyVersion", 2)]
    public void Validate_rejects_invalid_payload_schema_values(string propertyName, object value)
    {
        var artifact = CreateArtifact(payload => payload[propertyName] = value);

        var result = LicensePayloadValidator.Validate(artifact);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe(propertyName == "keyVersion" ? "invalid-signature" : "invalid-license-file");
    }

    [Fact]
    public void Validate_rejects_missing_required_payload_field()
    {
        var artifact = CreateArtifact(payload => payload.Remove("licenseId"));

        var result = LicensePayloadValidator.Validate(artifact);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-license-file");
    }

    [Fact]
    public void Validate_rejects_payload_extra_field()
    {
        var artifact = CreateArtifact(payload => payload["unexpected"] = true);

        var result = LicensePayloadValidator.Validate(artifact);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-license-file");
    }

    [Fact]
    public void Validate_rejects_non_utc_timestamp()
    {
        var artifact = CreateArtifact(payload => payload["issuedAt"] = "2026-06-18T00:00:00+04:00");

        var result = LicensePayloadValidator.Validate(artifact);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-license-file");
    }

    [Fact]
    public void Validate_rejects_not_before_after_expires_at()
    {
        var artifact = CreateArtifact(payload =>
        {
            payload["notBefore"] = "2026-06-20T00:00:00Z";
            payload["expiresAt"] = "2026-06-19T00:00:00Z";
        });

        var result = LicensePayloadValidator.Validate(artifact);

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-license-file");
    }

    [Fact]
    public void Validate_maps_readonly_status_to_artifact_status()
    {
        var artifact = CreateArtifact(payload => payload["status"] = "readonly");

        var result = LicensePayloadValidator.Validate(artifact);

        result.IsSuccess.ShouldBeTrue();
        result.Payload!.Status.ShouldBe(LicenseArtifactStatus.ReadOnly);
    }

    private static LicenseArtifact CreateArtifact(Action<Dictionary<string, object?>>? mutate = null)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 2,
            ["licenseId"] = "license-1",
            ["pluginId"] = "plugin.sample",
            ["customerId"] = "default",
            ["deploymentId"] = "default",
            ["deviceFingerprintAlgorithm"] = "v1",
            ["deviceFingerprint"] = new string('a', 64),
            ["packageFingerprint"] = new string('b', 64),
            ["licenseRevision"] = 7,
            ["issuedAt"] = "2026-06-18T00:00:00Z",
            ["notBefore"] = "2026-06-18T00:00:00Z",
            ["expiresAt"] = "2026-07-18T00:00:00Z",
            ["gracePeriodDays"] = 14,
            ["status"] = "active",
            ["keyVersion"] = 1
        };

        mutate?.Invoke(payload);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var element = document.RootElement.Clone();
        return new LicenseArtifact(
            element,
            new LicenseSignatureBlock("ECDSA-P256-SHA256", 1, "AQID"),
            LicenseCanonicalJsonWriter.Write(element));
    }
}
