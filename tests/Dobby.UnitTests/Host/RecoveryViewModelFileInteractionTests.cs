using Dobby.Host.Login;
using Dobby.Core;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class RecoveryViewModelFileInteractionTests
{
    [Fact]
    public void Recovery_view_model_uses_file_shell_and_clipboard_adapters()
    {
        var picker = new RecordingPicker("C:/tmp/import.dobbyrecovery");
        var opener = new RecordingFolderOpener();
        var clipboard = new RecordingClipboard();
        var viewModel = new RecoveryViewModel(picker, opener, clipboard);

        viewModel.SelectImportFile();
        viewModel.OpenExportFolder("C:/tmp/recovery");
        viewModel.CopyExportPath("C:/tmp/recovery/request.dobbyrecoveryrequest");

        viewModel.SelectedImportPath.ShouldBe("C:/tmp/import.dobbyrecovery");
        opener.OpenedPath.ShouldBe("C:/tmp/recovery");
        clipboard.CopiedText.ShouldBe("C:/tmp/recovery/request.dobbyrecoveryrequest");
    }

    [Fact]
    public void Import_response_verifies_selected_file_prompts_for_password_and_confirms_reset()
    {
        var challenge = Challenge("nonce-confirm");
        var importer = new RecordingImportCoordinator(Result.Success(challenge));
        var prompt = new RecordingPasswordPrompt("new owner password");
        var viewModel = new RecoveryViewModel(
            new RecordingPicker("C:/tmp/import.dobbyrecovery"),
            new RecordingFolderOpener(),
            new RecordingClipboard(),
            importCoordinator: importer,
            passwordResetPrompt: prompt);

        var result = viewModel.ImportResponse(CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        importer.VerifiedPath.ShouldBe("C:/tmp/import.dobbyrecovery");
        prompt.PromptedChallenge.ShouldBe(challenge);
        importer.ConfirmedChallenge.ShouldBe(challenge);
        importer.ConfirmedPasswordValue.ShouldBe("new owner password");
        importer.CancelledChallenge.ShouldBeNull();
        viewModel.SelectedImportPath.ShouldBe("C:/tmp/import.dobbyrecovery");
    }

    [Fact]
    public void Import_response_cancels_verified_challenge_without_confirming_when_password_prompt_is_cancelled()
    {
        var challenge = Challenge("nonce-cancel");
        var importer = new RecordingImportCoordinator(Result.Success(challenge));
        var prompt = new RecordingPasswordPrompt(null);
        var viewModel = new RecoveryViewModel(
            new RecordingPicker("C:/tmp/import.dobbyrecovery"),
            new RecordingFolderOpener(),
            new RecordingClipboard(),
            importCoordinator: importer,
            passwordResetPrompt: prompt);

        var result = viewModel.ImportResponse(CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.Cancelled);
        importer.VerifiedPath.ShouldBe("C:/tmp/import.dobbyrecovery");
        prompt.PromptedChallenge.ShouldBe(challenge);
        importer.CancelledChallenge.ShouldBe(challenge);
        importer.ConfirmedChallenge.ShouldBeNull();
    }

    private sealed class RecordingPicker(string selectedPath) : IRecoveryFilePicker
    {
        public string? PickRecoveryResponseFile() => selectedPath;
    }

    private sealed class RecordingFolderOpener : IRecoveryExportFolderOpener
    {
        public string? OpenedPath { get; private set; }

        public void OpenFolder(string path)
        {
            OpenedPath = path;
        }
    }

    private sealed class RecordingClipboard : IRecoveryExportPathClipboard
    {
        public string? CopiedText { get; private set; }

        public void CopyPath(string path)
        {
            CopiedText = path;
        }
    }

    private sealed class RecordingImportCoordinator(Result<RecoveryVerifiedChallenge> verifyResult) : IRecoveryResponseImportCoordinator
    {
        public string? VerifiedPath { get; private set; }

        public RecoveryVerifiedChallenge? ConfirmedChallenge { get; private set; }

        public string? ConfirmedPasswordValue { get; private set; }

        public RecoveryVerifiedChallenge? CancelledChallenge { get; private set; }

        public Result<RecoveryVerifiedChallenge> VerifyFile(string path)
        {
            VerifiedPath = path;
            return verifyResult;
        }

        public Result ConfirmPasswordReset(RecoveryVerifiedChallenge challenge, string newPassword, CorrelationId correlationId)
        {
            ConfirmedChallenge = challenge;
            ConfirmedPasswordValue = newPassword;
            return Result.Success();
        }

        public Result Cancel(RecoveryVerifiedChallenge challenge, CorrelationId correlationId)
        {
            CancelledChallenge = challenge;
            return Result.Failure(ErrorCode.Cancelled);
        }
    }

    private sealed class RecordingPasswordPrompt(string? password) : IRecoveryPasswordResetPrompt
    {
        public RecoveryVerifiedChallenge? PromptedChallenge { get; private set; }

        public string? CaptureNewOwnerPassword(RecoveryVerifiedChallenge challenge)
        {
            PromptedChallenge = challenge;
            return password;
        }
    }

    private static RecoveryVerifiedChallenge Challenge(string nonce)
    {
        return new RecoveryVerifiedChallenge(new RecoveryResponsePayload(
            2,
            "dobby.owner-recovery-response",
            "recovery-test-2026-01",
            "018e51be-7f65-7bcd-8000-000000000001",
            "018e51be-7f65-7bcd-8000-000000000002",
            nonce,
            "customer-1",
            "deployment-1",
            "device:abc",
            new string('a', 64),
            "reset-owner-password",
            "2026-08-21T00:00:00.0000000Z",
            "2026-08-22T00:00:00.0000000Z"));
    }
}
