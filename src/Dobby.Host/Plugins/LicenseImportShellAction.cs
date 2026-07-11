using System.Windows;
using System.Diagnostics;
using Dobby.Core;
using Dobby.Licensing;
using Dobby.Localization;
using Dobby.Shell;

namespace Dobby.Host.Plugins;

public enum LicenseImportShellOutcome
{
    Imported,
    Rejected,
    PermissionDenied,
    Cancelled,
    Busy,
    NoSelection
}

public sealed class LicenseImportShellAction(
    ShellViewModel shellViewModel,
    ILicenseFilePicker filePicker,
    ILicenseImportFileCoordinator importCoordinator,
    Func<LicenseImportResult, ValueTask>? importSucceeded = null,
    Action<ErrorCode>? unexpectedFailureSink = null)
{
    private int importInProgress;
    private readonly Action<ErrorCode> reportUnexpectedFailure = unexpectedFailureSink ??
        (errorCode => Trace.TraceWarning($"license.import.failed:{errorCode}"));

    public async Task<LicenseImportShellOutcome> ImportAsync(
        string actorId,
        CorrelationId correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        if (shellViewModel.LicenseImportVisibility != Visibility.Visible)
        {
            shellViewModel.SetLicenseImportFeedback(HostResourceKeys.LicenseImportPermissionDenied);
            return LicenseImportShellOutcome.PermissionDenied;
        }

        if (Interlocked.CompareExchange(ref importInProgress, 1, 0) != 0)
        {
            return LicenseImportShellOutcome.Busy;
        }

        if (!shellViewModel.LicenseImportIsEnabled)
        {
            Interlocked.Exchange(ref importInProgress, 0);
            shellViewModel.SetLicenseImportFeedback(HostResourceKeys.LicenseImportPermissionDenied);
            return LicenseImportShellOutcome.PermissionDenied;
        }

        shellViewModel.SetLicenseImportBusy(true);
        try
        {
            var path = filePicker.PickLicenseFile();
            if (string.IsNullOrWhiteSpace(path))
            {
                return LicenseImportShellOutcome.NoSelection;
            }

            var result = await importCoordinator.ImportFileAsync(
                actorId,
                path,
                correlationId,
                cancellationToken);
            if (result.IsSuccess)
            {
                if (importSucceeded is not null)
                {
                    try
                    {
                        await importSucceeded(result);
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
                    {
                        reportUnexpectedFailure(ErrorCode.PluginInternalError);
                    }
                }

                shellViewModel.SetLicenseImportFeedback(HostResourceKeys.LicenseImportSuccess);
                return LicenseImportShellOutcome.Imported;
            }

            if (result.ReasonCode.Equals(LicenseRejectReasons.PermissionDenied, StringComparison.Ordinal))
            {
                shellViewModel.SetLicenseImportFeedback(HostResourceKeys.LicenseImportPermissionDenied);
                return LicenseImportShellOutcome.PermissionDenied;
            }

            shellViewModel.SetLicenseImportFeedback(HostResourceKeys.LicenseImportRejected);
            return result.ReasonCode.Equals(LicenseRejectReasons.Cancelled, StringComparison.Ordinal)
                ? LicenseImportShellOutcome.Cancelled
                : LicenseImportShellOutcome.Rejected;
        }
        catch (OperationCanceledException)
        {
            shellViewModel.SetLicenseImportFeedback(HostResourceKeys.LicenseImportRejected);
            return LicenseImportShellOutcome.Cancelled;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            reportUnexpectedFailure(ErrorCode.PluginInternalError);
            shellViewModel.SetLicenseImportFeedback(HostResourceKeys.LicenseImportRejected);
            return LicenseImportShellOutcome.Rejected;
        }
        finally
        {
            shellViewModel.SetLicenseImportBusy(false);
            Volatile.Write(ref importInProgress, 0);
        }
    }
}
