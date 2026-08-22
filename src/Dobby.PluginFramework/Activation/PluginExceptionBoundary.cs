using Dobby.Core;

namespace Dobby.PluginFramework.Activation;

public enum PluginInvocationOutcomeKind
{
    Success,
    ExpectedRejection,
    AuthorizationOrLicenseDenied,
    Cancelled,
    TimedOut,
    PluginDeclaredFailure,
    PluginException,
    ContractBreach,
    HostInfrastructureFailure
}

public sealed record PluginInvocationOutcome(
    Result Result,
    PluginInvocationOutcomeKind Kind);

public sealed record PluginInvocationOutcome<T>(
    Result<T> Result,
    PluginInvocationOutcomeKind Kind);

public static class PluginExceptionBoundary
{
    public static async ValueTask<Result> InvokeLifecycleAsync(
        Func<CancellationToken, ValueTask<Result>> pluginCall,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        var outcome = await InvokeLifecycleClassifiedAsync(
            pluginCall,
            cancellationToken,
            timeout).ConfigureAwait(false);
        return outcome.Result;
    }

    public static async ValueTask<Result<T>> InvokeLifecycleAsync<T>(
        Func<CancellationToken, ValueTask<Result<T>>> pluginCall,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        var outcome = await InvokeLifecycleClassifiedAsync(
            pluginCall,
            cancellationToken,
            timeout).ConfigureAwait(false);
        return outcome.Result;
    }

    public static async ValueTask<PluginInvocationOutcome> InvokeLifecycleClassifiedAsync(
        Func<CancellationToken, ValueTask<Result>> pluginCall,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(pluginCall);
        ValidateTimeout(timeout);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        try
        {
            linkedSource.Token.ThrowIfCancellationRequested();
            var result = await pluginCall(linkedSource.Token)
                .AsTask()
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            return new PluginInvocationOutcome(result, ClassifyResult(result.IsSuccess, result.ErrorCode));
        }
        catch (OperationCanceledException)
        {
            return cancellationToken.IsCancellationRequested
                ? new PluginInvocationOutcome(Result.Failure(ErrorCode.Cancelled), PluginInvocationOutcomeKind.Cancelled)
                : new PluginInvocationOutcome(Result.Failure(ErrorCode.Timeout), PluginInvocationOutcomeKind.TimedOut);
        }
        catch (TimeoutException)
        {
            return new PluginInvocationOutcome(Result.Failure(ErrorCode.Timeout), PluginInvocationOutcomeKind.TimedOut);
        }
        catch (ArgumentException)
        {
            return new PluginInvocationOutcome(Result.Failure(ErrorCode.InvalidInput), PluginInvocationOutcomeKind.PluginException);
        }
        catch (NotSupportedException)
        {
            return new PluginInvocationOutcome(Result.Failure(ErrorCode.UnsupportedOperation), PluginInvocationOutcomeKind.PluginException);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return new PluginInvocationOutcome(Result.Failure(ErrorCode.PluginInternalError), PluginInvocationOutcomeKind.PluginException);
        }
    }

    public static async ValueTask<PluginInvocationOutcome<T>> InvokeLifecycleClassifiedAsync<T>(
        Func<CancellationToken, ValueTask<Result<T>>> pluginCall,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(pluginCall);
        ValidateTimeout(timeout);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        try
        {
            linkedSource.Token.ThrowIfCancellationRequested();
            var result = await pluginCall(linkedSource.Token)
                .AsTask()
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            return new PluginInvocationOutcome<T>(result, ClassifyResult(result.IsSuccess, result.ErrorCode));
        }
        catch (OperationCanceledException)
        {
            return cancellationToken.IsCancellationRequested
                ? new PluginInvocationOutcome<T>(Result.Failure<T>(ErrorCode.Cancelled), PluginInvocationOutcomeKind.Cancelled)
                : new PluginInvocationOutcome<T>(Result.Failure<T>(ErrorCode.Timeout), PluginInvocationOutcomeKind.TimedOut);
        }
        catch (TimeoutException)
        {
            return new PluginInvocationOutcome<T>(Result.Failure<T>(ErrorCode.Timeout), PluginInvocationOutcomeKind.TimedOut);
        }
        catch (ArgumentException)
        {
            return new PluginInvocationOutcome<T>(Result.Failure<T>(ErrorCode.InvalidInput), PluginInvocationOutcomeKind.PluginException);
        }
        catch (NotSupportedException)
        {
            return new PluginInvocationOutcome<T>(Result.Failure<T>(ErrorCode.UnsupportedOperation), PluginInvocationOutcomeKind.PluginException);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return new PluginInvocationOutcome<T>(Result.Failure<T>(ErrorCode.PluginInternalError), PluginInvocationOutcomeKind.PluginException);
        }
    }

    public static async Task<Result<T>> InvokeAsync<T>(
        Func<Task<T>> pluginCall,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pluginCall);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Result.Success(await pluginCall());
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<T>(ErrorCode.Cancelled);
        }
        catch (ArgumentException)
        {
            return Result.Failure<T>(ErrorCode.InvalidInput);
        }
        catch (NotSupportedException)
        {
            return Result.Failure<T>(ErrorCode.UnsupportedOperation);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure<T>(ErrorCode.PluginInternalError);
        }
    }

    public static PluginInvocationOutcomeKind ClassifyResult(bool isSuccess, ErrorCode? errorCode)
    {
        if (isSuccess)
        {
            return PluginInvocationOutcomeKind.Success;
        }
        if (errorCode == ErrorCode.Cancelled) return PluginInvocationOutcomeKind.Cancelled;
        if (errorCode == ErrorCode.Timeout) return PluginInvocationOutcomeKind.TimedOut;
        if (errorCode is { } authorization && (
            authorization == ErrorCode.Unauthorized ||
            authorization == ErrorCode.Forbidden ||
            authorization == ErrorCode.LicenseDenied ||
            authorization == ErrorCode.LicenseReadOnly ||
            authorization == ErrorCode.LicensePending ||
            authorization == ErrorCode.LicenseBindingMismatch))
        {
            return PluginInvocationOutcomeKind.AuthorizationOrLicenseDenied;
        }
        if (errorCode is { } expected && (
            expected == ErrorCode.InvalidInput ||
            expected == ErrorCode.ValidationFailed ||
            expected == ErrorCode.UnsupportedOperation ||
            expected == ErrorCode.NotFound ||
            expected == ErrorCode.AlreadyExists))
        {
            return PluginInvocationOutcomeKind.ExpectedRejection;
        }
        if (errorCode is { } infrastructure && (
            infrastructure == ErrorCode.DatabaseUnavailable ||
            infrastructure == ErrorCode.NetworkUnavailable ||
            infrastructure == ErrorCode.IoFailed ||
            infrastructure == ErrorCode.ResourceBusy))
        {
            return PluginInvocationOutcomeKind.HostInfrastructureFailure;
        }
        if (errorCode == ErrorCode.SerializationFailed)
        {
            return PluginInvocationOutcomeKind.ContractBreach;
        }
        return PluginInvocationOutcomeKind.PluginDeclaredFailure;
    }

    public static bool IsPluginDefect(PluginInvocationOutcomeKind outcomeKind) => outcomeKind is
        PluginInvocationOutcomeKind.TimedOut or
        PluginInvocationOutcomeKind.PluginDeclaredFailure or
        PluginInvocationOutcomeKind.PluginException or
        PluginInvocationOutcomeKind.ContractBreach;

    private static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
    }
}
