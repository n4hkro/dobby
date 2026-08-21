using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.Core;
using Dobby.Security.Trust;

namespace Dobby.Security.Authentication;

public sealed class RecoveryRequestVerifier
{
    private const int MaximumEnvelopeBytes = 64 * 1024;
    private static readonly HashSet<string> EnvelopeProperties = new(StringComparer.Ordinal)
    {
        "payload",
        "possessionProof"
    };
    private static readonly HashSet<string> PayloadProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "purpose",
        "requestId",
        "nonce",
        "customerId",
        "deploymentId",
        "deviceId",
        "recoveryPublicKeyHash",
        "issuedAt",
        "expiresAt",
        "vendorKeyId"
    };
    private static readonly HashSet<string> SignatureProperties = new(StringComparer.Ordinal)
    {
        "purpose",
        "algorithm",
        "format",
        "keyId",
        "value"
    };

    public Result<RecoveryRequestPayload> Verify(
        RecoveryRequestEnvelope envelope,
        byte[] provisionedDevicePublicKeySpki,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(provisionedDevicePublicKeySpki);
        if (!IsCompleteAndBounded(envelope) ||
            envelope.Payload.SchemaVersion != 2 ||
            !StringComparer.Ordinal.Equals(
                envelope.Payload.Purpose,
                ArtifactSignaturePurposes.OwnerRecoveryRequest) ||
            !StringComparer.Ordinal.Equals(
                envelope.PossessionProof.Purpose,
                ArtifactSignaturePurposes.OwnerRecoveryRequestPossession) ||
            !StringComparer.Ordinal.Equals(envelope.PossessionProof.Algorithm, ArtifactSignatureProfile.Algorithm) ||
            !StringComparer.Ordinal.Equals(envelope.PossessionProof.Format, ArtifactSignatureProfile.Format) ||
            !StringComparer.Ordinal.Equals(
                envelope.PossessionProof.KeyId,
                "device-" + envelope.Payload.RecoveryPublicKeyHash[..16]) ||
            !ArtifactSignatureCodec.TryDecodeUnpaddedBase64Url(
                envelope.PossessionProof.Value,
                ArtifactSignatureProfile.P256SignatureLength,
                out var signature) ||
            !RecoveryContractValidation.TryParseUtcZ(envelope.Payload.IssuedAt, out var issuedAt) ||
            !RecoveryContractValidation.TryParseUtcZ(envelope.Payload.ExpiresAt, out var expiresAt) ||
            expiresAt <= issuedAt ||
            expiresAt - issuedAt > TimeSpan.FromHours(1) ||
            issuedAt > now.ToUniversalTime().AddMinutes(5) ||
            now.ToUniversalTime() >= expiresAt)
        {
            return Result.Failure<RecoveryRequestPayload>(ErrorCode.Forbidden);
        }

        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(provisionedDevicePublicKeySpki, out var bytesRead);
            if (bytesRead != provisionedDevicePublicKeySpki.Length || verifier.KeySize != 256)
            {
                return Result.Failure<RecoveryRequestPayload>(ErrorCode.Forbidden);
            }

            var publicKeyHash = Convert.ToHexString(SHA256.HashData(provisionedDevicePublicKeySpki))
                .ToLowerInvariant();
            if (!StringComparer.Ordinal.Equals(publicKeyHash, envelope.Payload.RecoveryPublicKeyHash) ||
                !verifier.VerifyData(
                    RecoveryCanonicalJsonWriter.WriteRequestPayload(envelope.Payload),
                    signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                return Result.Failure<RecoveryRequestPayload>(ErrorCode.Forbidden);
            }

            return Result.Success(envelope.Payload);
        }
        catch (CryptographicException)
        {
            return Result.Failure<RecoveryRequestPayload>(ErrorCode.Forbidden);
        }
    }

    public static RecoveryRequestEnvelope ReadEnvelope(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (Encoding.UTF8.GetByteCount(json) > MaximumEnvelopeBytes)
        {
            throw new InvalidOperationException("Recovery request envelope exceeds its size bound.");
        }

        ValidateKnownProperties(json);
        var envelope = JsonSerializer.Deserialize<RecoveryRequestEnvelope>(json)
            ?? throw new InvalidOperationException("Recovery request envelope could not be parsed.");
        if (!IsCompleteAndBounded(envelope))
        {
            throw new InvalidOperationException("Recovery request envelope is missing or exceeds required members.");
        }

        return envelope;
    }

    public static byte[] ReadPublicKey(string pemOrDerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pemOrDerPath);
        var bytes = File.ReadAllBytes(pemOrDerPath);
        if (bytes.Length is 0 or > 16 * 1024)
        {
            throw new InvalidOperationException("Provisioned recovery public key is invalid.");
        }

        try
        {
            using var key = ECDsa.Create();
            if (bytes.AsSpan().StartsWith("-----BEGIN"u8))
            {
                key.ImportFromPem(Encoding.UTF8.GetString(bytes));
            }
            else
            {
                key.ImportSubjectPublicKeyInfo(bytes, out var bytesRead);
                if (bytesRead != bytes.Length)
                {
                    throw new InvalidOperationException("Provisioned recovery public key has trailing data.");
                }
            }

            if (key.KeySize != 256)
            {
                throw new InvalidOperationException("Provisioned recovery public key must use P-256.");
            }

            return key.ExportSubjectPublicKeyInfo();
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("Provisioned recovery public key is malformed.", exception);
        }
    }

    private static void ValidateKnownProperties(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8
        });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Recovery request envelope must be a JSON object.");
        }

        ValidateObjectProperties(root, EnvelopeProperties);
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("possessionProof", out var proof) || proof.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Recovery request envelope members are malformed.");
        }

        ValidateObjectProperties(payload, PayloadProperties);
        ValidateObjectProperties(proof, SignatureProperties);
    }

    private static void ValidateObjectProperties(JsonElement element, IReadOnlySet<string> allowedProperties)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name) || !seen.Add(property.Name))
            {
                throw new InvalidOperationException("Recovery request contains unsupported or duplicate members.");
            }
        }
    }

    private static bool IsCompleteAndBounded(RecoveryRequestEnvelope envelope)
    {
        return envelope.Payload is not null &&
            envelope.PossessionProof is not null &&
            RecoveryContractValidation.IsUuid(envelope.Payload.RequestId) &&
            ArtifactSignatureCodec.TryDecodeUnpaddedBase64Url(envelope.Payload.Nonce, 32, out _) &&
            RecoveryContractValidation.IsBoundedIdentifier(envelope.Payload.CustomerId) &&
            RecoveryContractValidation.IsBoundedIdentifier(envelope.Payload.DeploymentId) &&
            RecoveryContractValidation.IsBoundedIdentifier(envelope.Payload.DeviceId) &&
            RecoveryContractValidation.IsLowercaseSha256(envelope.Payload.RecoveryPublicKeyHash) &&
            RecoveryContractValidation.IsKeyId(envelope.Payload.VendorKeyId) &&
            RecoveryContractValidation.IsKeyId(envelope.PossessionProof.KeyId) &&
            envelope.PossessionProof.Value is { Length: 86 };
    }
}
