using Dobby.Core.Versioning;
using Dobby.Security.Authorization;

namespace Dobby.PluginFramework.Manifest;

public static class PluginOperationTargetContributionBuilder
{
    public static PluginOperationTargetContributionBuildOutcome Build(
        PluginManifest manifest,
        HostVersion hostVersion)
    {
        if (manifest is null)
        {
            throw new ArgumentNullException(nameof(manifest));
        }

        var validation = PluginManifestValidator.ValidateDetailed(manifest, hostVersion);
        if (!validation.IsSuccess)
        {
            return PluginOperationTargetContributionBuildOutcome.Failure(validation.Reasons);
        }

        var contributions = new List<PluginOperationTargetContribution>();
        for (var index = 0; index < manifest.OperationTargets.Count; index++)
        {
            var target = manifest.OperationTargets[index];
            var verbs = new List<OperationVerb>();

            for (var verbIndex = 0; verbIndex < target.Verbs.Count; verbIndex++)
            {
                if (!OperationVerbParser.TryParse(target.Verbs[verbIndex], out var verb))
                {
                    return PluginOperationTargetContributionBuildOutcome.Failure(
                    [
                        new PluginManifestValidationReason(
                            "invalid-manifest",
                            "Operation target verb is not canonical.",
                            $"operationTargets[{index}].verbs[{verbIndex}]")
                    ]);
                }

                verbs.Add(verb);
            }

            contributions.Add(new PluginOperationTargetContribution(
                manifest.PluginId,
                target.Id,
                verbs
                    .Distinct()
                    .OrderBy(OperationVerbParser.ToWireValue, StringComparer.Ordinal)
                    .ToArray(),
                target.LabelKey));
        }

        return PluginOperationTargetContributionBuildOutcome.Success(contributions);
    }
}

public sealed record PluginOperationTargetContribution(
    string PluginId,
    string TargetId,
    IReadOnlyList<OperationVerb> Verbs,
    string LabelKey);

public sealed record PluginOperationTargetContributionBuildOutcome(
    bool IsSuccess,
    IReadOnlyList<PluginOperationTargetContribution> Contributions,
    IReadOnlyList<PluginManifestValidationReason> Reasons)
{
    public static PluginOperationTargetContributionBuildOutcome Success(
        IReadOnlyList<PluginOperationTargetContribution> contributions)
    {
        return new PluginOperationTargetContributionBuildOutcome(true, contributions, []);
    }

    public static PluginOperationTargetContributionBuildOutcome Failure(
        IReadOnlyList<PluginManifestValidationReason> reasons)
    {
        return new PluginOperationTargetContributionBuildOutcome(false, [], reasons);
    }
}
