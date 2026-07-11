using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dobby.Core;
using Dobby.Localization;

namespace Dobby.Host.Login;

public enum LockViewModelFeedbackKind
{
    None,
    InvalidCredentials,
    Countdown,
    GenericFailure
}

public sealed record LockViewModelActionResult(
    bool IsSuccess,
    ErrorCode? ErrorCode = null,
    int RetryAfterSeconds = 0)
{
    public static LockViewModelActionResult FromResult(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? new LockViewModelActionResult(IsSuccess: true)
            : new LockViewModelActionResult(
                IsSuccess: false,
                result.ErrorCode ?? Dobby.Core.ErrorCode.UnsupportedOperation);
    }

    public static LockViewModelActionResult FromRuntimeResult(LockRuntimeWorkflowActionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? new LockViewModelActionResult(IsSuccess: true)
            : new LockViewModelActionResult(
                IsSuccess: false,
                result.ErrorCode ?? Dobby.Core.ErrorCode.UnsupportedOperation,
                result.RetryAfterSeconds);
    }

    public static LockViewModelActionResult Failure(ErrorCode errorCode, int retryAfterSeconds = 0)
    {
        return new LockViewModelActionResult(IsSuccess: false, errorCode, retryAfterSeconds);
    }
}

public interface ILockViewModelActions
{
    LockViewModelActionResult Unlock(string userId, string password, CorrelationId correlationId);

    LockViewModelActionResult Logout(string userId, CorrelationId correlationId);
}

public sealed class LockViewModel : INotifyPropertyChanged
{
    private readonly string userId;
    private readonly ILockViewModelActions actions;
    private readonly HostStringCatalog strings;
    private string submittedPassword = string.Empty;
    private string feedbackText = string.Empty;
    private LockViewModelFeedbackKind feedbackKind;
    private int retryAfterSeconds;
    private bool isBusy;

    public LockViewModel(string userId, ILockViewModelActions actions, HostStringCatalog strings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(strings);

        this.userId = userId;
        this.actions = actions;
        this.strings = strings;
    }

    public string CurrentUserLabel => userId;

    public string SubmittedPassword
    {
        get => submittedPassword;
        set
        {
            var nextValue = value ?? string.Empty;
            if (string.Equals(submittedPassword, nextValue, StringComparison.Ordinal))
            {
                return;
            }

            submittedPassword = nextValue;
            if (FeedbackKind != LockViewModelFeedbackKind.None)
            {
                ClearFeedback();
            }

            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (isBusy == value)
            {
                return;
            }

            isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UnlockEnabled));
            OnPropertyChanged(nameof(LogoutEnabled));
        }
    }

    public bool UnlockEnabled => !IsBusy;

    public bool LogoutEnabled => !IsBusy;

    public LockViewModelFeedbackKind FeedbackKind
    {
        get => feedbackKind;
        private set
        {
            if (feedbackKind == value)
            {
                return;
            }

            feedbackKind = value;
            OnPropertyChanged();
        }
    }

    public int RetryAfterSeconds
    {
        get => retryAfterSeconds;
        private set
        {
            if (retryAfterSeconds == value)
            {
                return;
            }

            retryAfterSeconds = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CountdownText));
        }
    }

    public string? CountdownText => RetryAfterSeconds > 0
        ? RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : null;

    public string FeedbackText
    {
        get => feedbackText;
        private set
        {
            if (string.Equals(feedbackText, value, StringComparison.Ordinal))
            {
                return;
            }

            feedbackText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ErrorText));
        }
    }

    public string ErrorText => FeedbackText;

    public bool? SubmitUnlock(CorrelationId correlationId)
    {
        return Submit(
            () => actions.Unlock(userId, SubmittedPassword, correlationId),
            dialogResult: true);
    }

    public bool? SubmitLogout(CorrelationId correlationId)
    {
        return Submit(
            () => actions.Logout(userId, correlationId),
            dialogResult: false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool? Submit(Func<LockViewModelActionResult> action, bool dialogResult)
    {
        if (IsBusy)
        {
            return null;
        }

        IsBusy = true;
        try
        {
            ClearFeedback();
            var outcome = action();
            if (outcome.IsSuccess)
            {
                return dialogResult;
            }

            ApplyFailure(outcome);
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFailure(LockViewModelActionResult outcome)
    {
        if (outcome.RetryAfterSeconds > 0)
        {
            RetryAfterSeconds = outcome.RetryAfterSeconds;
            FeedbackKind = LockViewModelFeedbackKind.Countdown;
            FeedbackText = $"{strings.GetString(HostResourceKeys.LoginThrottleCountdown)}: {CountdownText}";
            return;
        }

        RetryAfterSeconds = 0;
        FeedbackKind = outcome.ErrorCode == ErrorCode.Unauthorized
            ? LockViewModelFeedbackKind.InvalidCredentials
            : LockViewModelFeedbackKind.GenericFailure;
        FeedbackText = strings.GetString(
            FeedbackKind == LockViewModelFeedbackKind.InvalidCredentials
                ? HostResourceKeys.LoginInvalidCredentials
                : HostResourceKeys.LoginGenericFailure);
    }

    private void ClearFeedback()
    {
        RetryAfterSeconds = 0;
        FeedbackKind = LockViewModelFeedbackKind.None;
        FeedbackText = string.Empty;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class LockViewModelActions(LockRuntimeWorkflow workflow) : ILockViewModelActions
{
    public LockViewModelActionResult Unlock(string userId, string password, CorrelationId correlationId)
    {
        return LockViewModelActionResult.FromRuntimeResult(
            workflow.UnlockForLockScreen(userId, password, correlationId));
    }

    public LockViewModelActionResult Logout(string userId, CorrelationId correlationId)
    {
        return LockViewModelActionResult.FromResult(workflow.Logout(userId, correlationId));
    }
}
