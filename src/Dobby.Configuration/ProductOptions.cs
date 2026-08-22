namespace Dobby.Configuration;

public sealed class ProductOptions
{
    public string SupportContact { get; init; } = string.Empty;

    public string LegalLine { get; init; } = string.Empty;

    public ProductArtifactMode ArtifactMode { get; init; } = ProductArtifactMode.Installed;
}
