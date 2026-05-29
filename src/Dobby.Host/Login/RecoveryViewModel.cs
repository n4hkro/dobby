using Dobby.Core;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public interface IRecoveryFilePicker
{
    string? PickRecoveryResponseFile();
}

public interface IRecoveryExportFolderOpener
{
    void OpenFolder(string path);
}

public interface IRecoveryExportPathClipboard
{
    void CopyPath(string path);
}

public interface IRecoveryResponseImportCoordinator
{
    Result<RecoveryVerifiedChallenge> VerifyFile(string path);

    Result ConfirmPasswordReset(RecoveryVerifiedChallenge challenge, string newPassword, CorrelationId correlationId);

    Result Cancel(RecoveryVerifiedChallenge challenge, CorrelationId correlationId);
}

public interface IRecoveryPasswordResetPrompt
{
    string? CaptureNewOwnerPassword(RecoveryVerifiedChallenge challenge);
}

public sealed class RecoveryViewModel(
    IRecoveryFilePicker filePicker,
    IRecoveryExportFolderOpener folderOpener,
    IRecoveryExportPathClipboard pathClipboard,
    RecoveryRequestExportCoordinator? exportCoordinator = null,
    IRecoveryResponseImportCoordinator? importCoordinator = null,
    IRecoveryPasswordResetPrompt? passwordResetPrompt = null)
{
    public string? SelectedImportPath { get; private set; }

    public string? LastExportPath { get; private set; }

    public void SelectImportFile()
    {
        SelectedImportPath = filePicker.PickRecoveryResponseFile();
    }

    public Result ImportResponse(CorrelationId correlationId)
    {
        if (importCoordinator is null || passwordResetPrompt is null)
        {
            return Result.Failure(ErrorCode.UnsupportedOperation);
        }

        SelectImportFile();
        if (SelectedImportPath is null)
        {
            return Result.Failure(ErrorCode.Cancelled);
        }

        var verified = importCoordinator.VerifyFile(SelectedImportPath);
        if (!verified.IsSuccess)
        {
            return Result.Failure(verified.ErrorCode!.Value);
        }

        var ownerResetSecret = passwordResetPrompt.CaptureNewOwnerPassword(verified.Value);
        if (string.IsNullOrWhiteSpace(ownerResetSecret))
        {
            return importCoordinator.Cancel(verified.Value, correlationId);
        }

        return importCoordinator.ConfirmPasswordReset(verified.Value, ownerResetSecret, correlationId);
    }

    public Result<RecoveryRequestExportResult> ExportRequest(string outputDirectory, CorrelationId correlationId)
    {
        if (exportCoordinator is null)
        {
            return Result.Failure<RecoveryRequestExportResult>(ErrorCode.UnsupportedOperation);
        }

        var result = exportCoordinator.Export(outputDirectory, correlationId);
        if (result.IsSuccess)
        {
            LastExportPath = result.Value.FilePath;
        }

        return result;
    }

    public void OpenExportFolder(string path)
    {
        folderOpener.OpenFolder(path);
    }

    public void CopyExportPath(string path)
    {
        pathClipboard.CopyPath(path);
    }
}
