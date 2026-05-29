using Dobby.Core;
using System.Text.Json;

namespace Dobby.Security.Authentication;

public sealed record RecoveryVerifiedChallenge(RecoveryResponsePayload Payload);

public sealed record RecoveryResponseVerificationResult(
    Result<RecoveryVerifiedChallenge> Result,
    string Outcome);

public sealed class RecoveryResponseImporter(
    IVendorRecoveryPublicKey vendorPublicKey,
    IDeviceFingerprintSource deviceFingerprintSource)
{
    private const string ExpectedResponseType = "dobby.owner-recovery-response.v1";
    private const int SupportedKeyVersion = 1;
    private static readonly HashSet<string> EnvelopeProperties = new(StringComparer.Ordinal)
    {
        "payload",
        "signature"
    };

    private static readonly HashSet<string> PayloadProperties = new(StringComparer.Ordinal)
    {
        "type",
        "requestId",
        "deviceIdentity",
        "issuedAt",
        "expiresAt",
        "nonce",
        "keyVersion"
    };

    private static readonly HashSet<string> DeviceIdentityProperties = new(StringComparer.Ordinal)
    {
        "machineGuid",
        "cpuId",
        "motherboardSerial",
        "diskSerial",
        "macAddress"
    };

    public Result<RecoveryVerifiedChallenge> Verify(RecoveryResponseEnvelope envelope, DateTimeOffset now)
    {
        return VerifyWithOutcome(envelope, now).Result;
    }

    public RecoveryResponseVerificationResult VerifyWithOutcome(RecoveryResponseEnvelope envelope, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!StringComparer.Ordinal.Equals(envelope.Payload.Type, ExpectedResponseType) ||
            envelope.Payload.KeyVersion != SupportedKeyVersion)
        {
            return Failure("signature-fail");
        }

        if (!TryDecodeStandardPaddedIeeeSignature(envelope.Signature, out var signature))
        {
            return Failure("signature-fail");
        }

        var canonical = RecoveryCanonicalJsonWriter.WritePayload(envelope.Payload);
        if (!vendorPublicKey.Verify(canonical, signature))
        {
            return Failure("signature-fail");
        }

        if (!DateTimeOffset.TryParse(envelope.Payload.IssuedAt, out var issuedAt) ||
            !DateTimeOffset.TryParse(envelope.Payload.ExpiresAt, out var expiresAt) ||
            expiresAt.ToUniversalTime() <= issuedAt.ToUniversalTime() ||
            expiresAt.ToUniversalTime() - issuedAt.ToUniversalTime() > TimeSpan.FromHours(24) ||
            now.ToUniversalTime() >= expiresAt.ToUniversalTime())
        {
            return Failure("expired");
        }

        var currentFingerprint = deviceFingerprintSource.Current();
        if (!currentFingerprint.IsSuccess)
        {
            return new RecoveryResponseVerificationResult(
                Result.Failure<RecoveryVerifiedChallenge>(currentFingerprint.ErrorCode!.Value),
                "device-mismatch");
        }

        if (!DeviceFingerprintComposer.IsFuzzyMatch(ToFingerprint(envelope.Payload.DeviceIdentity), currentFingerprint.Value))
        {
            return Failure("device-mismatch");
        }

        return new RecoveryResponseVerificationResult(
            Result.Success(new RecoveryVerifiedChallenge(envelope.Payload)),
            "success");
    }

    public static RecoveryResponseEnvelope ReadEnvelope(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        ValidateKnownProperties(json);
        var envelope = JsonSerializer.Deserialize<RecoveryResponseEnvelope>(json)
            ?? throw new InvalidOperationException("Recovery response envelope could not be parsed.");
        if (!IsComplete(envelope))
        {
            throw new InvalidOperationException("Recovery response envelope is missing required members.");
        }

        return envelope;
    }

    private static void ValidateKnownProperties(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Recovery response envelope must be a JSON object.");
        }

        ValidateObjectProperties(root, EnvelopeProperties, "Recovery response envelope contains unsupported members.");
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Recovery response payload must be a JSON object.");
        }

        ValidateObjectProperties(payload, PayloadProperties, "Recovery response payload contains unsupported members.");
        if (!payload.TryGetProperty("deviceIdentity", out var deviceIdentity) || deviceIdentity.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Recovery response device identity must be a JSON object.");
        }

        ValidateObjectProperties(deviceIdentity, DeviceIdentityProperties, "Recovery response device identity contains unsupported members.");
    }

    private static void ValidateObjectProperties(JsonElement element, IReadOnlySet<string> allowedProperties, string message)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name) || !seen.Add(property.Name))
            {
                throw new InvalidOperationException(message);
            }
        }
    }

    private static bool TryDecodeStandardPaddedIeeeSignature(string signatureText, out byte[] signature)
    {
        signature = [];
        if (string.IsNullOrWhiteSpace(signatureText) || signatureText.Contains('-') || signatureText.Contains('_') || !signatureText.Contains('='))
        {
            return false;
        }

        try
        {
            signature = Convert.FromBase64String(signatureText);
        }
        catch (FormatException)
        {
            return false;
        }

        return signature.Length == 64;
    }

    private static DeviceFingerprint ToFingerprint(RecoveryDeviceIdentity identity)
    {
        return new DeviceFingerprint(identity.MachineGuid, identity.CpuId, identity.MotherboardSerial, identity.DiskSerial, identity.MacAddress);
    }

    private static bool IsComplete(RecoveryResponseEnvelope envelope)
    {
        return envelope.Payload is not null &&
            !string.IsNullOrWhiteSpace(envelope.Signature) &&
            !string.IsNullOrWhiteSpace(envelope.Payload.Type) &&
            !string.IsNullOrWhiteSpace(envelope.Payload.RequestId) &&
            envelope.Payload.DeviceIdentity is not null &&
            !string.IsNullOrWhiteSpace(envelope.Payload.DeviceIdentity.MachineGuid) &&
            !string.IsNullOrWhiteSpace(envelope.Payload.DeviceIdentity.CpuId) &&
            !string.IsNullOrWhiteSpace(envelope.Payload.DeviceIdentity.MotherboardSerial) &&
            !string.IsNullOrWhiteSpace(envelope.Payload.DeviceIdentity.DiskSerial) &&
            !string.IsNullOrWhiteSpace(envelope.Payload.DeviceIdentity.MacAddress) &&
            !string.IsNullOrWhiteSpace(envelope.Payload.IssuedAt) &&
            !string.IsNullOrWhiteSpace(envelope.Payload.ExpiresAt) &&
            !string.IsNullOrWhiteSpace(envelope.Payload.Nonce);
    }

    private static RecoveryResponseVerificationResult Failure(string outcome)
    {
        return new RecoveryResponseVerificationResult(
            Result.Failure<RecoveryVerifiedChallenge>(ErrorCode.Forbidden),
            outcome);
    }
}
