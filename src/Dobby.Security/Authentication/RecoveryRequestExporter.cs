using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dobby.Core;
using Dobby.Security.Trust;

namespace Dobby.Security.Authentication;

public sealed record RecoveryAuthorityContext(
    string CustomerId,
    string DeploymentId,
    string DeviceId);

public sealed record RecoveryRequestPayload(
    [property: JsonPropertyName("schemaVersion")]
    int SchemaVersion,
    [property: JsonPropertyName("purpose")]
    string Purpose,
    [property: JsonPropertyName("requestId")]
    string RequestId,
    [property: JsonPropertyName("nonce")]
    string Nonce,
    [property: JsonPropertyName("customerId")]
    string CustomerId,
    [property: JsonPropertyName("deploymentId")]
    string DeploymentId,
    [property: JsonPropertyName("deviceId")]
    string DeviceId,
    [property: JsonPropertyName("recoveryPublicKeyHash")]
    string RecoveryPublicKeyHash,
    [property: JsonPropertyName("issuedAt")]
    string IssuedAt,
    [property: JsonPropertyName("expiresAt")]
    string ExpiresAt,
    [property: JsonPropertyName("vendorKeyId")]
    string VendorKeyId);

public sealed record RecoveryRequestEnvelope(
    [property: JsonPropertyName("payload")]
    RecoveryRequestPayload Payload,
    [property: JsonPropertyName("possessionProof")]
    ArtifactSignatureEnvelope PossessionProof);

public sealed class DeviceRecoveryKeySigner
{
    private readonly byte[] privateKeyPkcs8;

    public DeviceRecoveryKeySigner(
        byte[] privateKeyPkcs8,
        byte[] publicKeySpki)
    {
        ArgumentNullException.ThrowIfNull(privateKeyPkcs8);
        ArgumentNullException.ThrowIfNull(publicKeySpki);
        if (privateKeyPkcs8.Length == 0 || publicKeySpki.Length == 0)
        {
            throw new InvalidOperationException("Device recovery key material is unavailable.");
        }

        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(privateKeyPkcs8, out var privateBytesRead);
        var derivedPublicKey = key.ExportSubjectPublicKeyInfo();
        if (privateBytesRead != privateKeyPkcs8.Length ||
            key.KeySize != 256 ||
            !CryptographicOperations.FixedTimeEquals(derivedPublicKey, publicKeySpki))
        {
            throw new InvalidOperationException("Device recovery key pair does not match.");
        }

        this.privateKeyPkcs8 = privateKeyPkcs8.ToArray();
        PublicKeyHash = Convert.ToHexString(SHA256.HashData(publicKeySpki)).ToLowerInvariant();
        KeyId = "device-" + PublicKeyHash[..16];
    }

    public string PublicKeyHash { get; }

    public string KeyId { get; }

    public ArtifactSignatureEnvelope SignPossessionProof(byte[] canonicalPayload)
    {
        ArgumentNullException.ThrowIfNull(canonicalPayload);
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(privateKeyPkcs8, out var bytesRead);
        if (bytesRead != privateKeyPkcs8.Length)
        {
            throw new InvalidOperationException("Device recovery private key is malformed.");
        }

        var signature = key.SignData(
            canonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new ArtifactSignatureEnvelope(
            ArtifactSignaturePurposes.OwnerRecoveryRequestPossession,
            ArtifactSignatureProfile.Algorithm,
            ArtifactSignatureProfile.Format,
            KeyId,
            ArtifactSignatureCodec.ToUnpaddedBase64Url(signature));
    }
}

public sealed class RecoveryRequestExporter(
    DeviceRecoveryKeySigner deviceKey,
    RecoveryAuthorityContext authorityContext,
    TimeProvider timeProvider,
    string vendorKeyId,
    TimeSpan? requestLifetime = null)
{
    private readonly TimeSpan lifetime = requestLifetime ?? TimeSpan.FromMinutes(30);

    public Result<RecoveryRequestEnvelope> CreateRequest()
    {
        if (!RecoveryContractValidation.IsBoundedIdentifier(authorityContext.CustomerId) ||
            !RecoveryContractValidation.IsBoundedIdentifier(authorityContext.DeploymentId) ||
            !RecoveryContractValidation.IsBoundedIdentifier(authorityContext.DeviceId) ||
            !RecoveryContractValidation.IsKeyId(vendorKeyId) ||
            lifetime <= TimeSpan.Zero ||
            lifetime > TimeSpan.FromHours(1))
        {
            return Result.Failure<RecoveryRequestEnvelope>(ErrorCode.InvalidInput);
        }

        var issuedAt = timeProvider.GetUtcNow().ToUniversalTime();
        var payload = new RecoveryRequestPayload(
            2,
            ArtifactSignaturePurposes.OwnerRecoveryRequest,
            Guid.CreateVersion7().ToString("D"),
            ArtifactSignatureCodec.ToUnpaddedBase64Url(RandomNumberGenerator.GetBytes(32)),
            authorityContext.CustomerId,
            authorityContext.DeploymentId,
            authorityContext.DeviceId,
            deviceKey.PublicKeyHash,
            RecoveryContractValidation.ToUtcZ(issuedAt),
            RecoveryContractValidation.ToUtcZ(issuedAt.Add(lifetime)),
            vendorKeyId);
        var canonicalPayload = RecoveryCanonicalJsonWriter.WriteRequestPayload(payload);
        return Result.Success(new RecoveryRequestEnvelope(
            payload,
            deviceKey.SignPossessionProof(canonicalPayload)));
    }

    public static string Serialize(RecoveryRequestEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
    }
}

public static class DeviceRecoveryIdentity
{
    public static string ComputeDeviceId(ReadOnlySpan<byte> stableDeviceId)
    {
        if (stableDeviceId.Length != 32)
        {
            throw new InvalidOperationException("Stable device identity material is unavailable.");
        }

        var domain = "dobby-recovery-device-id-v1\0"u8;
        var input = new byte[domain.Length + stableDeviceId.Length];
        domain.CopyTo(input);
        stableDeviceId.CopyTo(input.AsSpan(domain.Length));
        return "device:" + Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }
}
