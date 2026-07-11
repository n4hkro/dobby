using Dobby.Application.Printing;
using Dobby.Contracts.Licensing;
using Dobby.Core;
using Dobby.Host.Printing;
using Dobby.Licensing;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class HostPrintOutputServiceTests
{
    [Fact]
    public async Task ReadOnlyExpired_virtual_print_output_is_denied_before_execution()
    {
        var service = new HostPrintOutputService(
            new FixedRuntimeModeProvider(PluginRuntimeMode.ReadOnlyExpired),
            new HostPrintOutputCoordinator(
                new FixedPrintTargetClassifier(PrintTargetClass.VirtualOrFileOutput),
                new RecordingPrintConfirmation(confirmed: true)));
        var executionCalls = 0;

        var result = await service.ExecuteAsync(
            "plugin.sample",
            "package-fingerprint",
            new PrintTargetDescriptor("Microsoft Print to PDF", "Microsoft Print To PDF", "PORTPROMPT:", false),
            isPrintToFile: false,
            _ =>
            {
                executionCalls++;
                return ValueTask.FromResult(Result.Success());
            });

        result.ErrorCode.ShouldBe(ErrorCode.LicenseReadOnly);
        executionCalls.ShouldBe(0);
    }

    [Fact]
    public async Task FullAccess_print_output_executes_through_runtime_mode_lookup()
    {
        var runtimeModeProvider = new FixedRuntimeModeProvider(PluginRuntimeMode.FullAccess);
        var service = new HostPrintOutputService(
            runtimeModeProvider,
            new HostPrintOutputCoordinator(
                new FixedPrintTargetClassifier(PrintTargetClass.VirtualOrFileOutput),
                new RecordingPrintConfirmation(confirmed: false)));
        var executionCalls = 0;

        var result = await service.ExecuteAsync(
            "plugin.sample",
            "package-fingerprint",
            new PrintTargetDescriptor("Microsoft Print to PDF", "Microsoft Print To PDF", "PORTPROMPT:", false),
            isPrintToFile: true,
            _ =>
            {
                executionCalls++;
                return ValueTask.FromResult(Result.Success());
            });

        result.IsSuccess.ShouldBeTrue();
        runtimeModeProvider.PluginId.ShouldBe("plugin.sample");
        runtimeModeProvider.PackageFingerprint.ShouldBe("package-fingerprint");
        executionCalls.ShouldBe(1);
    }

    private sealed class FixedRuntimeModeProvider(PluginRuntimeMode mode) : ILicenseRuntimeModeProvider
    {
        public string? PluginId { get; private set; }

        public string? PackageFingerprint { get; private set; }

        public Result<PluginRuntimeMode> GetCurrentMode(string pluginId, string packageFingerprint)
        {
            PluginId = pluginId;
            PackageFingerprint = packageFingerprint;
            return Result.Success(mode);
        }
    }

    private sealed class FixedPrintTargetClassifier(PrintTargetClass targetClass) : IPrintTargetClassifier
    {
        public PrintTargetClass Classify(PrintTargetDescriptor target) => targetClass;
    }

    private sealed class RecordingPrintConfirmation(bool confirmed) : IPhysicalPrintConfirmation
    {
        public ValueTask<bool> ConfirmAsync(
            PrintTargetDescriptor target,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(confirmed);
        }
    }
}
