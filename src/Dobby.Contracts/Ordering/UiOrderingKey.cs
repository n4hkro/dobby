namespace Dobby.Contracts.Ordering;

public sealed record UiOrderingKey(
    string Group,
    int Weight,
    string PluginId,
    int DeclarationIndex);

public interface IUiOrderingComparer
{
    int Compare(UiOrderingKey left, UiOrderingKey right);
}
