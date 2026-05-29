using Dobby.Core;
using Dobby.Core.Versioning;
using Shouldly;

namespace Dobby.UnitTests.Core;

public sealed class ResultTests
{
    [Fact]
    public void Success_result_exposes_value_and_no_error()
    {
        var result = Result.Success("ready");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("ready");
        result.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public void Failure_result_exposes_error_and_rejects_value_access()
    {
        var result = Result.Failure<string>(ErrorCode.Forbidden);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Cancelled_helper_is_failure_with_cancelled_error_code()
    {
        var result = Result.Cancelled<int>();

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Cancelled);
    }

    [Fact]
    public void Host_version_rejects_prerelease_and_build_metadata()
    {
        HostVersion.TryParse("1.2.3", out var version).ShouldBeTrue();
        version.ShouldBe(new HostVersion(1, 2, 3));

        HostVersion.TryParse("1.2.3-preview.1", out _).ShouldBeFalse();
        HostVersion.TryParse("1.2.3+build.5", out _).ShouldBeFalse();
    }
}
