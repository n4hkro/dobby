using Dobby.Core;

namespace Dobby.PluginFramework.Activation;

public static class PluginExceptionBoundary
{
    public static async ValueTask<Result> InvokeLifecycleAsync(
        Func<CancellationToken, ValueTask<Result>> pluginCall,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(pluginCall);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        try
        {
            linkedSource.Token.ThrowIfCancellationRequested();
            return await pluginCall(linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure(
                cancellationToken.IsCancellationRequested
                    ? ErrorCode.Cancelled
                    : ErrorCode.Timeout);
        }
        catch (ArgumentException)
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }
        catch (NotSupportedException)
        {
            return Result.Failure(ErrorCode.UnsupportedOperation);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure(ErrorCode.PluginInternalError);
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
}
