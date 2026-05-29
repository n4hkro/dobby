using Dobby.Core;

namespace Dobby.Security.Authorization;

public sealed class OperationTargetRegistry
{
    private readonly Dictionary<string, OperationTarget> targets;

    private OperationTargetRegistry(IEnumerable<OperationTarget> builtInTargets)
    {
        targets = new Dictionary<string, OperationTarget>(StringComparer.Ordinal);
        foreach (var target in builtInTargets)
        {
            targets[target.TargetId] = Normalize(target);
        }
    }

    public IReadOnlyList<OperationTarget> Targets => targets.Values
        .OrderBy(target => target.TargetId, StringComparer.Ordinal)
        .ToArray();

    public static OperationTargetRegistry CreateDefault()
    {
        return new OperationTargetRegistry(BuiltInOperationTargets.All);
    }

    public Result<OperationTarget> Get(string targetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        return targets.TryGetValue(targetId, out var target)
            ? Result.Success(target)
            : Result.Failure<OperationTarget>(ErrorCode.NotFound);
    }

    public bool Supports(OperationKey key)
    {
        return targets.TryGetValue(key.TargetId, out var target) && target.Supports(key.Verb);
    }

    public Result RegisterPluginTarget(string targetId, IReadOnlyList<OperationVerb> verbs, string labelKey)
    {
        if (string.IsNullOrWhiteSpace(targetId) ||
            string.IsNullOrWhiteSpace(labelKey) ||
            verbs.Count == 0 ||
            verbs.Any(verb => !Enum.IsDefined(verb)))
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }

        if (targets.ContainsKey(targetId))
        {
            return Result.Failure(ErrorCode.AlreadyExists);
        }

        targets[targetId] = Normalize(new OperationTarget(
            targetId,
            verbs,
            labelKey,
            OperationTargetOrigin.Plugin));
        return Result.Success();
    }

    private static OperationTarget Normalize(OperationTarget target)
    {
        return target with
        {
            SupportedVerbs = target.SupportedVerbs
                .Distinct()
                .OrderBy(OperationVerbParser.ToWireValue, StringComparer.Ordinal)
                .ToArray()
        };
    }
}
