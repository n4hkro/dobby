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
    public void Compute_pins_the_v1_digest_for_a_fixed_machine_guid_and_stable_device_id()
    {
        // ADR-0063 canonical derivation: this literal must only change together with a new
        // artifact schemaVersion, because the digest is the license device binding value.
        var stableDeviceId = new byte[32];
        for (var i = 0; i < stableDeviceId.Length; i++)
        {
            stableDeviceId[i] = (byte)i;
        }

        var result = LicenseDeviceFingerprintV1.Compute("11111111-2222-3333-4444-555555555555", stableDeviceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("cf9da4f9fe72849d8c9d583b42569b4eb46d41dd39f3aa52afd3f9b869873155");
    }

    [Theory]
    [InlineData("11111111-2222-3333-4444-555555555555")]
    [InlineData("  11111111-2222-3333-4444-555555555555  ")]
    [InlineData("11111111-2222-3333-4444-555555555555\t")]
    [InlineData("11111111-2222-3333-4444-5555555555 55")]
    public void Compute_is_stable_across_whitespace_and_case_variants_of_the_same_machine_guid(string machineGuid)
    {
        var stableDeviceId = new byte[] { 0x01, 0x02, 0x0a };
        var canonical = LicenseDeviceFingerprintV1.Compute("11111111-2222-3333-4444-555555555555", stableDeviceId);
        var upperCased = LicenseDeviceFingerprintV1.Compute(machineGuid.ToUpperInvariant(), stableDeviceId);
        var variant = LicenseDeviceFingerprintV1.Compute(machineGuid, stableDeviceId);

        canonical.IsSuccess.ShouldBeTrue();
        variant.IsSuccess.ShouldBeTrue();
        upperCased.IsSuccess.ShouldBeTrue();

        // Leading/trailing whitespace and letter case are normalized away; an interior
        // difference is not, so it must produce a different digest.
        var expectedEquivalent = machineGuid.Trim() == "11111111-2222-3333-4444-555555555555";
        variant.Value.Value.Equals(canonical.Value.Value, StringComparison.Ordinal).ShouldBe(expectedEquivalent);
        upperCased.Value.Value.ShouldBe(variant.Value.Value);
    }

    [Fact]
    public void Compute_digests_the_nfc_form_of_a_decomposed_machine_guid()
    {
        // ADR-0063 § 1: the `machineGuid` field entering the digest is the trim +
        // `ToLowerInvariant` + Unicode `FormC` normalization of the registry value.
        //
        // The expected value is built by hand and hashed directly, NOT by calling `Compute` a
        // second time with the precomposed input. That earlier shape --
        // `Compute(decomposed).ShouldBe(Compute(composed))` -- holds for any function that is
        // merely deterministic, so it stayed green with the normalization deleted and proved
        // nothing about the rule it was named after (`NFR-027`: a test that cannot fail for its
        // stated reason is not evidence). Hashing an independently constructed canonical input
        // makes the NFC guarantee itself the thing under test, at every layer that could drop
        // it -- the derivation's own call and the canonical writer alike.
        //
        // "e" + COMBINING ACUTE ACCENT (U+0301) goes in; precomposed U+00E9 is expected. Both
        // are written as escapes rather than literal marks so a source re-encoding cannot
        // silently normalize the very input this test depends on. Case and surrounding
        // whitespace vary at the same time, so the full documented field pipeline is pinned.
        var stableDeviceId = new byte[] { 0x01, 0x02, 0x0a };
        var expectedCanonicalInput =
            "{\"algorithmVersion\":\"dobby-license-device-fingerprint-v1\"," +
            "\"machineGuid\":\"caf\u00e9-guid\"," +
            "\"stableDeviceId\":\"01020a\"}";
        var expected = Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expectedCanonicalInput)))
            .ToLowerInvariant();

        var result = LicenseDeviceFingerprintV1.Compute("  CAFE\u0301-GUID  ", stableDeviceId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Fact]
    public void Compute_binds_the_stable_device_id_so_a_single_byte_change_changes_the_digest()
    {
        var baseline = LicenseDeviceFingerprintV1.Compute("machine", [0x01, 0x02, 0x0a]);
        var altered = LicenseDeviceFingerprintV1.Compute("machine", [0x01, 0x02, 0x0b]);

        baseline.IsSuccess.ShouldBeTrue();
        altered.IsSuccess.ShouldBeTrue();
        altered.Value.Value.ShouldNotBe(baseline.Value.Value);
        LicenseDeviceFingerprintV1.Matches(baseline.Value.Value, altered.Value.Value).ShouldBeFalse();
    }

    [Fact]
    public void Matches_uses_exact_constant_time_hex_comparison_not_recovery_fuzzy_matching()
    {
        var baseline = new string('a', 64);
        var oneCharacterDifferent = new string('a', 63) + "b";

        LicenseDeviceFingerprintV1.Matches(baseline, baseline).ShouldBeTrue();
        LicenseDeviceFingerprintV1.Matches(baseline, oneCharacterDifferent).ShouldBeFalse();
    }
}
