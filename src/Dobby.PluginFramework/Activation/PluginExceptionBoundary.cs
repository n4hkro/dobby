using Dobby.Core;

namespace Dobby.PluginFramework.Activation;

public static class PluginExceptionBoundary
{
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
