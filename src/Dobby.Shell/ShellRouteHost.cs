using Dobby.Core;

namespace Dobby.Shell;

public sealed class ShellRouteHost(
    Func<string?, string> resolveRouteKey,
    Func<string, ValueTask<Result>> activateAsync)
{
    public async ValueTask<Result<string>> ActivateOrHomeAsync(string? routeKey)
    {
        ArgumentNullException.ThrowIfNull(resolveRouteKey);
        ArgumentNullException.ThrowIfNull(activateAsync);

        var resolvedRouteKey = resolveRouteKey(routeKey);
        Result result;
        try
        {
            result = await activateAsync(resolvedRouteKey).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<string>(ErrorCode.Cancelled);
        }
        catch (ArgumentException)
        {
            return Result.Failure<string>(ErrorCode.InvalidInput);
        }
        catch (NotSupportedException)
        {
            return Result.Failure<string>(ErrorCode.UnsupportedOperation);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure<string>(ErrorCode.PluginInternalError);
        }

        return result.IsSuccess
            ? Result.Success(resolvedRouteKey)
            : Result.Failure<string>(result.ErrorCode ?? ErrorCode.UnsupportedOperation);
    }
}
