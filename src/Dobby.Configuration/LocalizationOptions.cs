namespace Dobby.Configuration;

public sealed class LocalizationOptions
{
    public string DefaultCulture { get; init; } = "az-AZ";

    public string[] SupportedCultures { get; init; } = ["az-AZ", "en-US", "tr-TR"];
}
