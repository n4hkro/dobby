using System.Security.Cryptography;
using System.Text;
using Dobby.Core;
using Dobby.Infrastructure.HardwareIdentity;

namespace Dobby.Security.Authentication;

public sealed class DeviceFingerprintComposer(IHardwareIdentityProvider hardwareIdentityProvider)
{
    public Result<DeviceFingerprint> Compose()
    {
        var snapshot = hardwareIdentityProvider.Read();
        if (HasMissingSource(snapshot))
        {
            return Result.Failure<DeviceFingerprint>(ErrorCode.IoFailed);
        }

        return Result.Success(new DeviceFingerprint(
            Hash(snapshot.MachineGuid),
            Hash(snapshot.CpuId),
            Hash(snapshot.MotherboardSerial),
            Hash(snapshot.DiskSerial),
            Hash(snapshot.MacAddress)));
    }

    public static bool IsFuzzyMatch(DeviceFingerprint expected, DeviceFingerprint actual)
    {
        var matches = 0;
        matches += StringComparer.Ordinal.Equals(expected.MachineGuid, actual.MachineGuid) ? 1 : 0;
        matches += StringComparer.Ordinal.Equals(expected.CpuId, actual.CpuId) ? 1 : 0;
        matches += StringComparer.Ordinal.Equals(expected.MotherboardSerial, actual.MotherboardSerial) ? 1 : 0;
        matches += StringComparer.Ordinal.Equals(expected.DiskSerial, actual.DiskSerial) ? 1 : 0;
        matches += StringComparer.Ordinal.Equals(expected.MacAddress, actual.MacAddress) ? 1 : 0;
        return matches >= 3;
    }

    private static bool HasMissingSource(HardwareIdentitySnapshot snapshot)
    {
        return string.IsNullOrWhiteSpace(snapshot.MachineGuid)
            || string.IsNullOrWhiteSpace(snapshot.CpuId)
            || string.IsNullOrWhiteSpace(snapshot.MotherboardSerial)
            || string.IsNullOrWhiteSpace(snapshot.DiskSerial)
            || string.IsNullOrWhiteSpace(snapshot.MacAddress);
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
