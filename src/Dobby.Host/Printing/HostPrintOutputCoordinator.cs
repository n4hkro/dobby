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

        if (runtimeMode == PluginRuntimeMode.Denied)
        {
            return Result.Failure(ErrorCode.LicenseDenied);
        }

        if (runtimeMode == PluginRuntimeMode.FullAccess)
        {
            return await execute(cancellationToken);
        }

        var targetClass = targetClassifier.Classify(target);
        var decision = PrintOutputPolicy.EvaluateReadOnlyExpiredOutput(targetClass, isPrintToFile);
        if (decision == PrintOutputDecision.Denied)
        {
            return Result.Failure(ErrorCode.LicenseReadOnly);
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
