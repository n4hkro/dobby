using Dobby.Contracts.Ordering;

namespace Dobby.Navigation;

public enum NavigationTarget
{
    TopToolbar,
    Sidebar,
    ThirdBar
}

public enum NavigationFixedPosition
{
    None,
    First,
    Last
}

public sealed record NavigationContribution(
    string Id,
    string RouteKey,
    string LabelKey,
    NavigationTarget Target,
    UiOrderingKey Ordering,
    string? ParentId,
    NavigationFixedPosition FixedPosition)
{
    public static NavigationContribution Create(
        string id,
        string routeKey,
        string labelKey,
        NavigationTarget target,
        UiOrderingKey ordering,
        string? parentId = null,
        NavigationFixedPosition fixedPosition = NavigationFixedPosition.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelKey);
        ArgumentNullException.ThrowIfNull(ordering);

        return new NavigationContribution(id, routeKey, labelKey, target, ordering, parentId, fixedPosition);
    }
}

public sealed record NavigationUserOverrides(
    IReadOnlyList<string> TopToolbarOrder,
    IReadOnlyList<string> SidebarRouteOrder,
    IReadOnlyList<string> ThirdBarRouteOrder,
    IReadOnlyCollection<string> HiddenRouteKeys,
    bool ResetToDefaults)
{
    public static NavigationUserOverrides Empty { get; } = new(
        TopToolbarOrder: [],
        SidebarRouteOrder: [],
        ThirdBarRouteOrder: [],
        HiddenRouteKeys: [],
        ResetToDefaults: false);
}
