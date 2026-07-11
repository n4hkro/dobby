using Dobby.Application.Printing;
using Dobby.Contracts.Licensing;
using Dobby.Core;
using Dobby.Host.Printing;
using Shouldly;

namespace Dobby.UnitTests.Application;

public sealed class PrintOutputPolicyTests
{
    [Fact]
    public void ReadOnlyExpired_physical_print_requires_confirmation()
    {
        PrintOutputPolicy.EvaluateReadOnlyExpiredOutput(
                PrintTargetClass.PhysicalPrinter,
                isPrintToFile: false)
            .ShouldBe(PrintOutputDecision.ConfirmationRequired);
    }

    [Theory]
    [InlineData(PrintTargetClass.PhysicalPrinter, true)]
    [InlineData(PrintTargetClass.VirtualOrFileOutput, false)]
    [InlineData(PrintTargetClass.Unknown, false)]
    public void Allows_output_only_for_physical_printers(PrintTargetClass targetClass, bool expected)
    {
        PrintOutputPolicy.AllowsOutput(targetClass).ShouldBe(expected);
    }

    [Fact]
    public void Read_only_expired_print_policy_denies_virtual_unknown_and_print_to_file_output()
    {
        PrintOutputPolicy.EvaluateReadOnlyExpiredOutput(
                PrintTargetClass.VirtualOrFileOutput,
                isPrintToFile: false)
            .ShouldBe(PrintOutputDecision.Denied);
        PrintOutputPolicy.EvaluateReadOnlyExpiredOutput(
                PrintTargetClass.Unknown,
                isPrintToFile: false)
            .ShouldBe(PrintOutputDecision.Denied);
        PrintOutputPolicy.EvaluateReadOnlyExpiredOutput(
                PrintTargetClass.PhysicalPrinter,
                isPrintToFile: true)
            .ShouldBe(PrintOutputDecision.Denied);
    }

    [Fact]
    public async Task Read_only_expired_physical_print_executes_only_after_confirmation()
    {
        var confirmation = new RecordingPrintConfirmation(confirmed: true);
        var coordinator = new HostPrintOutputCoordinator(
            new FixedPrintTargetClassifier(PrintTargetClass.PhysicalPrinter),
            confirmation);
        var executionCalls = 0;

        var result = await coordinator.ExecuteAsync(
            PluginRuntimeMode.ReadOnlyExpired,
            Target(),
            isPrintToFile: false,
            _ =>
            {
                executionCalls++;
                return ValueTask.FromResult(Result.Success());
            });

        result.IsSuccess.ShouldBeTrue();
        confirmation.Calls.ShouldBe(1);
        executionCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Read_only_expired_physical_print_cancel_returns_typed_denial_without_execution()
    {
        var confirmation = new RecordingPrintConfirmation(confirmed: false);
        var coordinator = new HostPrintOutputCoordinator(
            new FixedPrintTargetClassifier(PrintTargetClass.PhysicalPrinter),
            confirmation);
        var executionCalls = 0;

        var result = await coordinator.ExecuteAsync(
            PluginRuntimeMode.ReadOnlyExpired,
            Target(),
            isPrintToFile: false,
            _ =>
            {
                executionCalls++;
                return ValueTask.FromResult(Result.Success());
            });

        result.ErrorCode.ShouldBe(ErrorCode.LicenseReadOnly);
        confirmation.Calls.ShouldBe(1);
        executionCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Read_only_expired_cancellation_after_confirmation_does_not_execute_print()
    {
        using var cancellation = new CancellationTokenSource();
        var coordinator = new HostPrintOutputCoordinator(
            new FixedPrintTargetClassifier(PrintTargetClass.PhysicalPrinter),
            new CancellingPrintConfirmation(cancellation));
        var executionCalls = 0;

        var result = await coordinator.ExecuteAsync(
            PluginRuntimeMode.ReadOnlyExpired,
            Target(),
            isPrintToFile: false,
            _ =>
            {
                executionCalls++;
                return ValueTask.FromResult(Result.Success());
            },
            cancellation.Token);

        result.ErrorCode.ShouldBe(ErrorCode.Cancelled);
        executionCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(PrintTargetClass.VirtualOrFileOutput, false)]
    [InlineData(PrintTargetClass.Unknown, false)]
    [InlineData(PrintTargetClass.PhysicalPrinter, true)]
    public async Task Read_only_expired_non_physical_or_print_to_file_denies_before_confirmation_and_execution(
        PrintTargetClass targetClass,
        bool isPrintToFile)
    {
        var confirmation = new RecordingPrintConfirmation(confirmed: true);
        var coordinator = new HostPrintOutputCoordinator(
            new FixedPrintTargetClassifier(targetClass),
            confirmation);
        var executionCalls = 0;

        var result = await coordinator.ExecuteAsync(
            PluginRuntimeMode.ReadOnlyExpired,
            Target(),
            isPrintToFile,
            _ =>
            {
                executionCalls++;
                return ValueTask.FromResult(Result.Success());
            });

        result.ErrorCode.ShouldBe(ErrorCode.LicenseReadOnly);
        confirmation.Calls.ShouldBe(0);
        executionCalls.ShouldBe(0);
    }

    private static PrintTargetDescriptor Target()
    {
        return new PrintTargetDescriptor("Office Laser", "Vendor Driver", "USB001", true);
    }

    private sealed class FixedPrintTargetClassifier(PrintTargetClass targetClass) : IPrintTargetClassifier
    {
        public PrintTargetClass Classify(PrintTargetDescriptor target) => targetClass;
    }

    private sealed class RecordingPrintConfirmation(bool confirmed) : IPhysicalPrintConfirmation
    {
        public int Calls { get; private set; }

        public ValueTask<bool> ConfirmAsync(
            PrintTargetDescriptor target,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(confirmed);
        }
    }

    private sealed class CancellingPrintConfirmation(CancellationTokenSource cancellation) : IPhysicalPrintConfirmation
    {
        public ValueTask<bool> ConfirmAsync(
            PrintTargetDescriptor target,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromResult(true);
        }
    }
}
