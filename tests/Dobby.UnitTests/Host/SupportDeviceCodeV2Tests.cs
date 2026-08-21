using Dobby.Host.Settings;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class SupportDeviceCodeV2Tests
{
    [Fact]
    public void Code_is_stable_versioned_human_formatted_and_checksummed()
    {
        var deviceId = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var key = Enumerable.Range(32, 32).Select(value => (byte)value).ToArray();

        var first = SupportDeviceCodeV2.Compute(deviceId, key);
        var repeated = SupportDeviceCodeV2.Compute(deviceId, key);

        first.ShouldBe(repeated);
        first.ShouldMatch("^D2-[0-9A-HJKMNP-TV-Z]{5}-[0-9A-HJKMNP-TV-Z]{5}-[0-9A-HJKMNP-TV-Z]$");
        SupportDeviceCodeV2.HasValidChecksum(first).ShouldBeTrue();
        SupportDeviceCodeV2.HasValidChecksum(first[..^1] + (first[^1] == '0' ? '1' : '0')).ShouldBeFalse();
    }

    [Fact]
    public void Device_identity_and_secret_key_are_both_required_and_never_rendered()
    {
        var deviceId = Enumerable.Repeat((byte)0x41, 32).ToArray();
        var key = Enumerable.Repeat((byte)0x42, 32).ToArray();
        var baseline = SupportDeviceCodeV2.Compute(deviceId, key);
        var changedDevice = SupportDeviceCodeV2.Compute(
            Enumerable.Repeat((byte)0x43, 32).ToArray(),
            key);
        var changedKey = SupportDeviceCodeV2.Compute(
            deviceId,
            Enumerable.Repeat((byte)0x44, 32).ToArray());

        changedDevice.ShouldNotBe(baseline);
        changedKey.ShouldNotBe(baseline);
        baseline.ShouldNotContain(Convert.ToHexString(deviceId), Case.Insensitive);
        SupportDeviceCodeV2.Compute(deviceId[..31], key).ShouldBe("device-unavailable");
        SupportDeviceCodeV2.Compute(deviceId, key[..31]).ShouldBe("device-unavailable");
    }
}
