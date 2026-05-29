using Dobby.Contracts.Ordering;

namespace Dobby.Navigation;

public sealed class DefaultUiOrderingComparer : IUiOrderingComparer, IComparer<UiOrderingKey>
{
    public int Compare(UiOrderingKey? x, UiOrderingKey? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        return CompareNonNull(x, y);
    }

    private static int CompareNonNull(UiOrderingKey left, UiOrderingKey right)
    {
        var group = StringComparer.Ordinal.Compare(left.Group, right.Group);
        if (group != 0)
        {
            return group;
        }

        var weight = left.Weight.CompareTo(right.Weight);
        if (weight != 0)
        {
            return weight;
        }

        var plugin = StringComparer.Ordinal.Compare(left.PluginId, right.PluginId);
        return plugin != 0 ? plugin : left.DeclarationIndex.CompareTo(right.DeclarationIndex);
    }
}
