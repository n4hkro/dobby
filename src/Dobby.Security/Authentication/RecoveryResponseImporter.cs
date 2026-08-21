using System.Text.Json;
using Dobby.Core;
using Dobby.Security.Trust;

namespace Dobby.Security.Authentication;

public sealed record RecoveryVerifiedChallenge(RecoveryResponsePayload Payload);

public sealed record RecoveryResponseVerificationResult(
    Result<RecoveryVerifiedChallenge> Result,
    string Outcome);

public sealed class RecoveryResponseImporter(IVendorRecoveryPublicKey vendorPublicKey)
{
    private const int MaximumEnvelopeBytes = 64 * 1024;
    private static readonly HashSet<string> EnvelopeProperties = new(StringComparer.Ordinal)
    {
        "payload",
        "signature"
    };
    private static readonly HashSet<string> PayloadProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "purpose",
        "keyId",
        "responseId",
        "requestId",
        "nonce",
        "customerId",
        "deploymentId",
        "deviceId",
        "recoveryPublicKeyHash",
        "action",
        "issuedAt",
        "expiresAt"
    };
    private static readonly HashSet<string> SignatureProperties = new(StringComparer.Ordinal)
    {
        "purpose",
        "algorithm",
        "format",
        "keyId",
        "value"
    };

    public Result<RecoveryVerifiedChallenge> Verify(RecoveryResponseEnvelope envelope, DateTimeOffset now)
    {
        return VerifyWithOutcome(envelope, now).Result;
    }

    public RecoveryResponseVerificationResult VerifyWithOutcome(
        RecoveryResponseEnvelope envelope,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (!IsCompleteAndBounded(envelope) ||
            envelope.Payload.SchemaVersion != 2 ||
            !StringComparer.Ordinal.Equals(
                envelope.Payload.Purpose,
                ArtifactSignaturePurposes.OwnerRecoveryResponse) ||
            !StringComparer.Ordinal.Equals(envelope.Payload.Action, "reset-owner-password") ||
            !StringComparer.Ordinal.Equals(
                envelope.Signature.Purpose,
                ArtifactSignaturePurposes.OwnerRecoveryResponse) ||
            !StringComparer.Ordinal.Equals(envelope.Signature.Algorithm, ArtifactSignatureProfile.Algorithm) ||
            !StringComparer.Ordinal.Equals(envelope.Signature.Format, ArtifactSignatureProfile.Format) ||
            !StringComparer.Ordinal.Equals(envelope.Signature.KeyId, envelope.Payload.KeyId))
        {
            return Failure("invalid-artifact");
        }

        if (!ArtifactSignatureCodec.TryDecodeUnpaddedBase64Url(
                envelope.Signature.Value,
                ArtifactSignatureProfile.P256SignatureLength,
                out var signature))
        {
            return Failure("signature-fail");
        }

        var canonical = RecoveryCanonicalJsonWriter.WritePayload(envelope.Payload);
        if (!vendorPublicKey.Verify(
                envelope.Payload.KeyId,
                ArtifactSignaturePurposes.OwnerRecoveryResponse,
                canonical,
                signature,
                now))
        {
            return Failure("signature-fail");
        }

        if (!RecoveryContractValidation.TryParseUtcZ(envelope.Payload.IssuedAt, out var issuedAt) ||
            !RecoveryContractValidation.TryParseUtcZ(envelope.Payload.ExpiresAt, out var expiresAt) ||
            expiresAt <= issuedAt ||
            expiresAt - issuedAt > TimeSpan.FromHours(24) ||
            issuedAt > now.ToUniversalTime().AddMinutes(5) ||
            now.ToUniversalTime() >= expiresAt)
        {
            return Failure("expired");
        }

        return new RecoveryResponseVerificationResult(
            Result.Success(new RecoveryVerifiedChallenge(envelope.Payload)),
            "success");
    }

    public static RecoveryResponseEnvelope ReadEnvelope(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumEnvelopeBytes)
        {
            throw new InvalidOperationException("Recovery response envelope exceeds its size bound.");
        }

        ValidateKnownProperties(json);
        var envelope = JsonSerializer.Deserialize<RecoveryResponseEnvelope>(json)
            ?? throw new InvalidOperationException("Recovery response envelope could not be parsed.");
        if (!IsCompleteAndBounded(envelope))
        {
            throw new InvalidOperationException("Recovery response envelope is missing or exceeds required members.");
        }

        return envelope;
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
            throw new InvalidOperationException("Recovery response envelope must be a JSON object.");
        }

        ValidateObjectProperties(root, EnvelopeProperties);
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("signature", out var signature) || signature.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Recovery response envelope members are malformed.");
        }

        ValidateObjectProperties(payload, PayloadProperties);
        ValidateObjectProperties(signature, SignatureProperties);
    }

    private static void ValidateObjectProperties(
        JsonElement element,
        IReadOnlySet<string> allowedProperties)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name) || !seen.Add(property.Name))
            {
                throw new InvalidOperationException("Recovery response contains unsupported or duplicate members.");
            }
        }
    }

    private static bool IsCompleteAndBounded(RecoveryResponseEnvelope envelope)
    {
        return envelope.Payload is not null &&
            envelope.Signature is not null &&
            RecoveryContractValidation.IsKeyId(envelope.Payload.KeyId) &&
            RecoveryContractValidation.IsUuid(envelope.Payload.ResponseId) &&
            RecoveryContractValidation.IsUuid(envelope.Payload.RequestId) &&
            ArtifactSignatureCodec.TryDecodeUnpaddedBase64Url(envelope.Payload.Nonce, 32, out _) &&
            RecoveryContractValidation.IsBoundedIdentifier(envelope.Payload.CustomerId) &&
            RecoveryContractValidation.IsBoundedIdentifier(envelope.Payload.DeploymentId) &&
            RecoveryContractValidation.IsBoundedIdentifier(envelope.Payload.DeviceId) &&
            RecoveryContractValidation.IsLowercaseSha256(envelope.Payload.RecoveryPublicKeyHash) &&
            envelope.Payload.Action is { Length: <= 64 } &&
            RecoveryContractValidation.IsKeyId(envelope.Signature.KeyId) &&
            envelope.Signature.Value is { Length: 86 };
    }

    private static RecoveryResponseVerificationResult Failure(string outcome)
    {
        return new RecoveryResponseVerificationResult(
            Result.Failure<RecoveryVerifiedChallenge>(ErrorCode.Forbidden),
            outcome);
    }
}
