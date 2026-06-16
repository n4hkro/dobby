using Dobby.Contracts.Ordering;
using Dobby.Contracts.PluginPages;

namespace Dobby.Navigation;

public static class PluginNavigationContributionMapper
{
    public static IReadOnlyList<NavigationContribution> Map(IEnumerable<PluginNavigationContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        return contributions
            .Select(contribution => NavigationContribution.Create(
                contribution.ExtensionPointId,
                PluginRouteNormalizer.Normalize(contribution.PluginId, contribution.RouteKey),
                contribution.LabelKey,
                ParseTarget(contribution.Target),
                new UiOrderingKey(
                    contribution.Group ?? "default",
                    contribution.Weight ?? 100,
                    contribution.PluginId,
                    contribution.DeclarationIndex),
                contribution.ParentId))
            .ToArray();
    }

    private static NavigationTarget ParseTarget(string target)
    {
        return target switch
        {
            "top-toolbar" => NavigationTarget.TopToolbar,
            "sidebar" => NavigationTarget.Sidebar,
            "third-bar" => NavigationTarget.ThirdBar,
            _ => throw new InvalidOperationException($"Unsupported plugin navigation target '{target}'.")
        };
    }
}
