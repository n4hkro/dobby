using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Shell.WorkSessions;

namespace Dobby.Host.Plugins;

public sealed class ShellPluginWorkSessionService(WorkSessionRegistry registry) :
    IPluginWorkSessionService,
    IPluginWorkSessionCleanup
{
    public Result<PluginWorkSessionHandle> Open(string ownerId, string routeKey, string labelKey)
    {
        try
        {
            var session = registry.Open(ownerId, routeKey, labelKey);
            return Result.Success(new PluginWorkSessionHandle(
                session.SessionId,
                session.OwnerId,
                session.RouteKey,
                session.LabelKey));
        }
        catch (ArgumentException)
        {
            return Result.Failure<PluginWorkSessionHandle>(ErrorCode.InvalidInput);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<PluginWorkSessionHandle>(ErrorCode.ResourceBusy);
        }
    }

    public Result UpdateProgress(Guid sessionId, int percent)
    {
        try
        {
            registry.UpdateProgress(sessionId, WorkSessionProgress.Determinate(percent));
            return Result.Success();
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }
    }

    public Result RecordResult(Guid sessionId, string resultSummaryKey)
    {
        try
        {
            registry.RecordResult(sessionId, resultSummaryKey);
            return Result.Success();
        }
        catch (ArgumentException)
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }
    }

    public Result RecordError(Guid sessionId, string errorSummaryKey)
    {
        try
        {
            registry.RecordError(sessionId, errorSummaryKey);
            return Result.Success();
        }
        catch (ArgumentException)
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }
    }

    public Result Close(Guid sessionId)
    {
        registry.Close(sessionId);
        return Result.Success();
    }

    public Result CloseOwnedBy(string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        foreach (var sessionId in registry.Snapshot()
                     .Where(session => session.OwnerId.Equals(ownerId, StringComparison.Ordinal))
                     .Select(session => session.SessionId)
                     .ToArray())
        {
            registry.Close(sessionId);
        }

        return Result.Success();
    }
}
