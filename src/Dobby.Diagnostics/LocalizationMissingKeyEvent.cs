namespace Dobby.Diagnostics;

public sealed record LocalizationMissingKeyEvent(
    string Culture,
    string ResourceKey,
    string ResourceScope,
    string FallbackChainResult)
{
    public string EventName => "localization.missing-key";

    public string DeduplicationKey => string.Join('|', Culture, ResourceKey, ResourceScope);

    public string Severity => "Warning";
}
