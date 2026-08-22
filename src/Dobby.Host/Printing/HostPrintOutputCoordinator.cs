using System.Windows;
using Dobby.Application.Printing;
using Dobby.Contracts.Licensing;
using Dobby.Core;
using Dobby.Localization;

namespace Dobby.Host.Printing;

public interface IPhysicalPrintConfirmation
{
    ValueTask<bool> ConfirmAsync(
        PrintTargetDescriptor target,
        CancellationToken cancellationToken = default);
}

public sealed class HostPrintOutputCoordinator(
    IPrintTargetClassifier targetClassifier,
    IPhysicalPrintConfirmation confirmation)
{
    public async ValueTask<Result> ExecuteAsync(
        PluginRuntimeMode runtimeMode,
        PrintTargetDescriptor target,
        bool isPrintToFile,
        Func<CancellationToken, ValueTask<Result>> execute,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(execute);

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(ErrorCode.Cancelled);
        }

        var action = isPrintToFile
            ? PluginRuntimeAction.VirtualOrFileOutput
            : targetClassifier.Classify(target) == PrintTargetClass.PhysicalPrinter
                ? PluginRuntimeAction.PhysicalPrint
                : PluginRuntimeAction.VirtualOrFileOutput;
        var runtimeDecision = PluginRuntimeActionPolicy.Evaluate(runtimeMode, action);
        if (runtimeDecision == PluginRuntimeActionDecision.Denied)
        {
            return Result.Failure(runtimeMode == PluginRuntimeMode.ReadOnlyExpired
                ? ErrorCode.LicenseReadOnly
                : ErrorCode.LicenseDenied);
        }

        if (runtimeDecision == PluginRuntimeActionDecision.Allowed)
        {
            return await execute(cancellationToken);
        }

        var confirmed = await confirmation.ConfirmAsync(target, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(ErrorCode.Cancelled);
        }

        if (!confirmed)
        {
            return Result.Failure(ErrorCode.LicenseReadOnly);
        }

        return await execute(cancellationToken);
    }
}

public sealed class WpfPhysicalPrintConfirmation(HostStringCatalog strings) : IPhysicalPrintConfirmation
{
    public ValueTask<bool> ConfirmAsync(
        PrintTargetDescriptor target,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(false);
        }

        var result = MessageBox.Show(
            strings.GetString(HostResourceKeys.PrintReadOnlyConfirmationMessage),
            strings.GetString(HostResourceKeys.PrintReadOnlyConfirmationTitle),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return ValueTask.FromResult(result == MessageBoxResult.Yes);
    }
}
