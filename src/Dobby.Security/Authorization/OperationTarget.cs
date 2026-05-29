namespace Dobby.Security.Authorization;

public sealed record OperationTarget(
    string TargetId,
    IReadOnlyList<OperationVerb> SupportedVerbs,
    string LabelKey,
    OperationTargetOrigin Origin)
{
    public bool Supports(OperationVerb verb)
    {
        return SupportedVerbs.Contains(verb);
    }
}
