using Dobby.Infrastructure.HardwareIdentity;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Recovery;

public sealed class DeviceFingerprintComposerTests
{
    [Fact]
    public void Compose_hashes_all_five_sources_without_returning_raw_values()
    {
        var composer = new DeviceFingerprintComposer(new StaticHardwareIdentityProvider(new HardwareIdentitySnapshot("machine", "cpu", "board", "disk", "mac")));

        var result = composer.Compose();

        result.IsSuccess.ShouldBeTrue();
        result.Value.MachineGuid.ShouldNotBe("machine");
        result.Value.CpuId.ShouldNotBe("cpu");
        result.Value.MotherboardSerial.ShouldNotBe("board");
        result.Value.DiskSerial.ShouldNotBe("disk");
        result.Value.MacAddress.ShouldNotBe("mac");
        result.Value.MachineGuid.Length.ShouldBe(64);
    }

    [Fact]
    public void Fuzzy_match_requires_at_least_three_of_five_matching_hashes()
    {
        var baseline = new DeviceFingerprint("a", "b", "c", "d", "e");

        DeviceFingerprintComposer.IsFuzzyMatch(baseline, new DeviceFingerprint("a", "b", "c", "x", "y")).ShouldBeTrue();
        DeviceFingerprintComposer.IsFuzzyMatch(baseline, new DeviceFingerprint("a", "b", "x", "y", "z")).ShouldBeFalse();
    }

    [Fact]
    public void Compose_fails_closed_when_any_source_is_missing()
    {
        var composer = new DeviceFingerprintComposer(new StaticHardwareIdentityProvider(new HardwareIdentitySnapshot("machine", "cpu", "", "disk", "mac")));

        composer.Compose().IsSuccess.ShouldBeFalse();
    }

    private sealed class StaticHardwareIdentityProvider(HardwareIdentitySnapshot snapshot) : IHardwareIdentityProvider
    {
        public HardwareIdentitySnapshot Read() => snapshot;
    }
}
