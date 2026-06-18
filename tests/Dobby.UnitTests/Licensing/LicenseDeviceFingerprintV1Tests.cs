using System.Security.Cryptography;
using Dobby.Licensing;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicenseDeviceFingerprintV1Tests
{
    [Fact]
    public void Compute_hashes_normalized_machine_guid_and_stable_device_id_with_v1_algorithm_marker()
    {
        var stableDeviceId = new byte[] { 0x01, 0x02, 0x0a };
        var expectedInput = "{\"algorithmVersion\":\"dobby-license-device-fingerprint-v1\",\"machineGuid\":\"machine-guid\",\"stableDeviceId\":\"01020a\"}";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expectedInput))).ToLowerInvariant();

        var result = LicenseDeviceFingerprintV1.Compute("  MACHINE-GUID  ", stableDeviceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
        result.Value.RedactedPrefix.ShouldBe(expected[..12]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Compute_fails_closed_when_machine_guid_is_missing(string machineGuid)
    {
        var result = LicenseDeviceFingerprintV1.Compute(machineGuid, [0x01]);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(Dobby.Core.ErrorCode.LicenseBindingMismatch);
    }

    [Fact]
    public void Compute_fails_closed_when_stable_device_id_is_missing()
    {
        var result = LicenseDeviceFingerprintV1.Compute("machine", []);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(Dobby.Core.ErrorCode.LicenseBindingMismatch);
    }

    [Fact]
    public void Matches_uses_exact_constant_time_hex_comparison_not_recovery_fuzzy_matching()
    {
        var baseline = new string('a', 64);
        var oneCharacterDifferent = new string('a', 63) + "b";

        LicenseDeviceFingerprintV1.Matches(baseline, baseline).ShouldBeTrue();
        LicenseDeviceFingerprintV1.Matches(baseline, oneCharacterDifferent).ShouldBeFalse();
    }

    [Fact]
    public void Redact_never_returns_raw_machine_or_stable_device_values()
    {
        var result = LicenseDeviceFingerprintV1.Compute("machine", Encoding.UTF8.GetBytes("stable-device"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RedactedPrefix.ShouldNotContain("machine");
        result.Value.RedactedPrefix.ShouldNotContain("stable-device");
        result.Value.RedactedPrefix.Length.ShouldBe(12);
    }
}
