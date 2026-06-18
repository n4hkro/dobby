using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.Core;

namespace Dobby.Licensing;

public sealed record LicenseDeviceFingerprintV1(string Value, string RedactedPrefix)
{
    private const string AlgorithmVersion = "dobby-license-device-fingerprint-v1";

    public static Result<LicenseDeviceFingerprintV1> Compute(string machineGuid, byte[] stableDeviceId)
    {
        if (string.IsNullOrWhiteSpace(machineGuid) || stableDeviceId.Length == 0)
        {
            return Result.Failure<LicenseDeviceFingerprintV1>(ErrorCode.LicenseBindingMismatch);
        }

        var normalizedMachineGuid = machineGuid.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormC);
        var stableDeviceIdHex = Convert.ToHexString(stableDeviceId).ToLowerInvariant();
        var canonicalInput = CanonicalInput(normalizedMachineGuid, stableDeviceIdHex);
        var fingerprint = Convert.ToHexString(SHA256.HashData(canonicalInput)).ToLowerInvariant();

        return Result.Success(new LicenseDeviceFingerprintV1(fingerprint, fingerprint[..12]));
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
