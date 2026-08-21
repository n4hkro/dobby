using Dobby.PluginFramework.Packaging;
using Shouldly;

namespace Dobby.UnitTests.PluginFramework.Packaging;

public sealed class PluginPackagePathPolicyTests
{
    [Theory]
    [InlineData("content/hello.txt")]
    [InlineData("bin/plugins/Dobby.Sample.Plugin.dll")]
    [InlineData("assets/caf\u00e9/icon.svg")]
    public void Canonical_relative_paths_are_accepted_unchanged(string path)
    {
        PluginPackagePathPolicy.TryNormalize(path, out var normalized).ShouldBeTrue();
        normalized.ShouldBe(path);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/absolute")]
    [InlineData("//server/share")]
    [InlineData("C:/drive")]
    [InlineData("folder\\file")]
    [InlineData("folder//file")]
    [InlineData("folder/./file")]
    [InlineData("folder/../file")]
    [InlineData("folder/file.")]
    [InlineData("folder/file ")]
    [InlineData("NUL")]
    [InlineData("con.txt")]
    [InlineData("folder/COM9.log")]
    [InlineData("folder/LPT1")]
    [InlineData("folder/file:stream")]
    [InlineData("folder/control\u0001.txt")]
    public void Non_portable_or_escaping_paths_are_rejected(string path)
    {
        PluginPackagePathPolicy.TryNormalize(path, out _).ShouldBeFalse();
    }

    [Fact]
    public void Path_length_is_bounded()
    {
        PluginPackagePathPolicy.TryNormalize(new string('a', 512), out _).ShouldBeTrue();
        PluginPackagePathPolicy.TryNormalize(new string('a', 513), out _).ShouldBeFalse();
    }
}
