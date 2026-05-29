using Dobby.Core;

namespace Dobby.Security.Authorization;

public readonly record struct OperationKey(string TargetId, OperationVerb Verb)
{
    public static Result<OperationKey> Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<OperationKey>(ErrorCode.InvalidInput);
        }

        var splitIndex = value.LastIndexOf('.');
        if (splitIndex <= 0 || splitIndex == value.Length - 1)
        {
            return Result.Failure<OperationKey>(ErrorCode.InvalidInput);
        }

        var targetId = value[..splitIndex];
        var verbText = value[(splitIndex + 1)..];
        return OperationVerbParser.TryParse(verbText, out var verb)
            ? Result.Success(new OperationKey(targetId, verb))
            : Result.Failure<OperationKey>(ErrorCode.InvalidInput);
    }

    public override string ToString() => $"{TargetId}.{OperationVerbParser.ToWireValue(Verb)}";
}
