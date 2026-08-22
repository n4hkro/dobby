using System.Globalization;
using Dobby.Contracts.PluginPages;
using Dobby.Core;

namespace Dobby.Shell.PluginPages;

public static class PluginLocalizationResolver
{
    public static Result<string> Resolve(
        PluginLocalizationCatalog catalog,
        PluginLocalizedText text,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);

        var validationBundle = catalog.Bundles.FirstOrDefault(bundle =>
                bundle.Culture.Equals(culture.Name, StringComparison.OrdinalIgnoreCase))
            ?? catalog.Bundles.FirstOrDefault(bundle =>
                bundle.Culture.Equals(catalog.DefaultCulture, StringComparison.OrdinalIgnoreCase));
        if (validationBundle is null || !validationBundle.Entries.TryGetValue(text.Key, out var format))
        {
            return Result.Failure<string>(ErrorCode.NotFound);
        }

        try
        {
            var arguments = (text.Arguments ?? [])
                .Select(argument => (object)SanitizeArgument(argument))
                .ToArray();
            return Result.Success(string.Format(culture, format, arguments));
        }
        catch (FormatException)
        {
            return Result.Failure<string>(ErrorCode.ValidationFailed);
        }
    }

    private static string SanitizeArgument(string value)
    {
        if (value.Length > 128 || value.Any(character => char.IsControl(character)))
        {
            throw new FormatException("Localized argument is malformed.");
        }

        return value;
    }
}
