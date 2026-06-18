using System.Security.Cryptography;
using System.Text.Json;
using Dobby.Licensing;

namespace Dobby.UnitTests.Licensing;

internal static class LicenseArtifactFixtureBuilder
{
    public static ECDsa CreateKeyPair(out byte[] publicKeyPem)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        publicKeyPem = EncodePem(key.ExportSubjectPublicKeyInfo());
        return key;
    }

    public static string SignToBase64Url(ECDsa key, byte[] canonicalPayload)
    {
        var signature = key.SignData(
            canonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        return ToBase64Url(signature);
    }

    public static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static SignedLicenseArtifact CreateSignedArtifact(
        ECDsa key,
        Action<Dictionary<string, object?>>? mutate = null)
    {
        var payload = ValidPayload();
        mutate?.Invoke(payload);
        using var payloadDocument = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var canonicalPayload = LicenseCanonicalJsonWriter.Write(payloadDocument.RootElement);
        var signature = SignToBase64Url(key, canonicalPayload);
        var payloadJson = Encoding.UTF8.GetString(canonicalPayload);
        var artifactJson = "{\"payload\":" + payloadJson +
            ",\"signature\":{\"algorithm\":\"ECDSA-P256-SHA256\",\"keyVersion\":1,\"value\":\"" +
            signature +
            "\"}}";

        return new SignedLicenseArtifact(Encoding.UTF8.GetBytes(artifactJson), signature);
    }

    private static Dictionary<string, object?> ValidPayload()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
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
    }

    private static byte[] EncodePem(byte[] der)
    {
        var base64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        var pem = $"-----BEGIN PUBLIC KEY-----\n{base64}\n-----END PUBLIC KEY-----\n";
        return Encoding.UTF8.GetBytes(pem);
    }
}

internal sealed record SignedLicenseArtifact(byte[] Bytes, string SignatureValue);
