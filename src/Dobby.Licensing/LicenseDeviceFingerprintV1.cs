using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.Core;

namespace Dobby.Licensing;

/// <summary>
/// Canonical license device binding digest (`ADR-0063` § `Canonical derivation`).
/// </summary>
/// <remarks>
/// This type deliberately exposes no truncated, "redacted" or otherwise
/// display-oriented projection of <see cref="Value"/>. The binding is a pure value
/// comparison with no possession proof, so any published prefix of the digest is an
/// offline-guessing shortcut against that comparison rather than a safe identifier.
/// The only sanctioned human-facing device identifier is the versioned keyed support
/// code (`SupportDeviceCodeV2`, `ADR-0058` § `Support identity`); the persisted
/// internal uniqueness discriminator is domain-separated and never derived from this
/// digest (`ADR-0063` § 3).
/// </remarks>
public sealed record LicenseDeviceFingerprintV1(string Value)
{
    private const string AlgorithmVersion = "dobby-license-device-fingerprint-v1";

    public static Result<LicenseDeviceFingerprintV1> Compute(string machineGuid, byte[] stableDeviceId)
    {
        if (string.IsNullOrWhiteSpace(machineGuid) || stableDeviceId.Length == 0)
        {
            return Result.Failure<LicenseDeviceFingerprintV1>(ErrorCode.LicenseBindingMismatch);
        }

        // The `FormC` call is redundant *today* and kept on purpose. `LicenseCanonicalJsonWriter`
        // normalizes every string value and every property name it writes, and its normalization
        // is idempotent and applied last, so `trim -> lower -> FormC -> FormC` and
        // `trim -> lower -> FormC` are the same string: no test can observe this call in
        // isolation, and deleting it changes no digest. It stays because `ADR-0063` § 1 assigns
        // the `machineGuid` *field* normalization here and names the writer only as the
        // *serialization* owner (ordinal key order). Leaning on the writer would make an
        // ADR-mandated field contract an emergent property of a component whose documented job
        // is key ordering -- a later writer change could then silently drop it, and the binding
        // digest of every machine with a decomposed `MachineGuid` would move.
        var normalizedMachineGuid = machineGuid.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormC);
        var stableDeviceIdHex = Convert.ToHexString(stableDeviceId).ToLowerInvariant();
        var canonicalInput = CanonicalInput(normalizedMachineGuid, stableDeviceIdHex);
        var fingerprint = Convert.ToHexString(SHA256.HashData(canonicalInput)).ToLowerInvariant();

        return Result.Success(new LicenseDeviceFingerprintV1(fingerprint));
    }

    public static bool Matches(string expectedFingerprint, string actualFingerprint)
    {
        if (!IsLowercaseSha256(expectedFingerprint) || !IsLowercaseSha256(actualFingerprint))
        {
            return false;
        }

        var expected = Convert.FromHexString(expectedFingerprint);
        var actual = Convert.FromHexString(actualFingerprint);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] CanonicalInput(string machineGuid, string stableDeviceIdHex)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["algorithmVersion"] = AlgorithmVersion,
            ["machineGuid"] = machineGuid,
            ["stableDeviceId"] = stableDeviceIdHex
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(values));
        return LicenseCanonicalJsonWriter.Write(document.RootElement);
    }

    private static bool IsLowercaseSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        return value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}
