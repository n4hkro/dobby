using Dobby.Application.Printing;
using Dobby.Core;
using Shouldly;

namespace Dobby.UnitTests.Application;

public sealed class PrintOutputPolicyTests
{
    [Theory]
    [InlineData(PrintTargetClass.PhysicalPrinter, true)]
    [InlineData(PrintTargetClass.VirtualOrFileOutput, false)]
    [InlineData(PrintTargetClass.Unknown, false)]
    public void Allows_output_only_for_physical_printers(PrintTargetClass targetClass, bool expected)
    {
        PrintOutputPolicy.AllowsOutput(targetClass).ShouldBe(expected);
    }

    [Fact]
    public void Read_only_expired_print_policy_denies_virtual_or_unknown_output_with_license_error()
    {
        PrintOutputPolicy.EnsureReadOnlyExpiredOutputAllowed(PrintTargetClass.VirtualOrFileOutput)
            .ErrorCode.ShouldBe(ErrorCode.LicenseReadOnly);
        PrintOutputPolicy.EnsureReadOnlyExpiredOutputAllowed(PrintTargetClass.Unknown)
            .ErrorCode.ShouldBe(ErrorCode.LicenseReadOnly);
    }

    [Fact]
    public void Read_only_expired_print_policy_allows_physical_printers()
    {
        PrintOutputPolicy.EnsureReadOnlyExpiredOutputAllowed(PrintTargetClass.PhysicalPrinter)
            .IsSuccess.ShouldBeTrue();
    }
}
