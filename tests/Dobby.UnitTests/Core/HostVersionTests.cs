using Dobby.Core.Versioning;
using Shouldly;

namespace Dobby.UnitTests.Core;

public sealed class HostVersionTests
{
    [Theory]
    [InlineData("0.0.0", 0, 0, 0)]
    [InlineData("1.0.0", 1, 0, 0)]
    [InlineData("123.45.67", 123, 45, 67)]
    public void TryParse_accepts_canonical_version(
        string input,
        int major,
        int minor,
        int patch)
    {
        HostVersion.TryParse(input, out var version).ShouldBeTrue();

        version.ShouldBe(new HostVersion(major, minor, patch));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("01.0.0")]
    [InlineData("1.02.0")]
    [InlineData("1.0.03")]
    [InlineData(" 1.0.0")]
    [InlineData("1.0.0 ")]
    [InlineData("1. 2.3")]
    [InlineData("1.2 .3")]
    [InlineData("\t1.0.0")]
    [InlineData("1.+2.3")]
    [InlineData("1.-2.3")]
    [InlineData("1.2.3-rc1")]
    [InlineData("1.2.3+build")]
    [InlineData(".1.2.3")]
    [InlineData("1.2.3.")]
    public void TryParse_rejects_invalid_inputs(string input)
    {
        HostVersion.TryParse(input, out _).ShouldBeFalse();
    }

    [Fact]
    public void CompareTo_orders_by_major_then_minor_then_patch()
    {
        var patch = new HostVersion(1, 0, 1);
        var minor = new HostVersion(1, 1, 0);
        var major = new HostVersion(2, 0, 0);

        new HostVersion(1, 0, 0).CompareTo(patch).ShouldBeLessThan(0);
        patch.CompareTo(minor).ShouldBeLessThan(0);
        minor.CompareTo(major).ShouldBeLessThan(0);
        major.CompareTo(new HostVersion(1, 9, 9)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Equals_is_value_equality_and_symmetric()
    {
        var first = new HostVersion(1, 2, 3);
        var second = new HostVersion(1, 2, 3);
        var different = new HostVersion(1, 2, 4);

        first.Equals(second).ShouldBeTrue();
        second.Equals(first).ShouldBeTrue();
        first.Equals(different).ShouldBeFalse();
    }

    [Fact]
    public void FixedHostVersionProvider_returns_constructor_value()
    {
        var version = new HostVersion(10, 2, 7);
        IHostVersionProvider provider = new FixedHostVersionProvider(version);

        provider.Current.ShouldBe(version);
    }
}
