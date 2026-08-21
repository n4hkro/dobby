using Dobby.Configuration;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.IO;

namespace Dobby.Host;

public static class HostConfigurationSources
{
    private static readonly HashSet<string> KnownDobbySections = new(StringComparer.OrdinalIgnoreCase)
    {
        "Runtime",
        "Storage",
        "Auth",
        "Session",
        "Diagnostics",
        "Audit",
        "Localization",
        "Plugins",
        "Licensing",
        "BranchShared"
    };

    public static IConfigurationRoot Build(HostConfigurationSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var runtimeOverridePath = Path.Combine(options.RuntimeConfigDirectory, "appsettings.json");
        var builder = new ConfigurationBuilder()
            .AddJsonFile(options.PackagedAppSettingsPath, optional: false, reloadOnChange: false);

        if (!Path.GetFullPath(options.PackagedAppSettingsPath).Equals(Path.GetFullPath(runtimeOverridePath), StringComparison.OrdinalIgnoreCase))
        {
            builder.AddJsonFile(runtimeOverridePath, optional: true, reloadOnChange: false);
        }

        var environmentValues = options.EnvironmentVariables ?? ReadDobbyEnvironmentVariables();
        if (environmentValues.Count > 0)
        {
            builder.AddInMemoryCollection(environmentValues.Select(ConvertEnvironmentVariable));
        }

        if (options.EnvironmentName.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
        }

        return builder.Build();
    }

    public static IReadOnlyList<string> FindUnknownDobbySections(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration
            .GetSection("Dobby")
            .GetChildren()
            .Select(section => section.Key)
            .Where(section => !KnownDobbySections.Contains(section))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static DobbyOptions BindDobbyOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new DobbyOptions
        {
            Auth = new AuthOptions { LoginThrottleStepsSeconds = [] },
            Localization = new LocalizationOptions { SupportedCultures = [] }
        };
        configuration.GetSection("Dobby").Bind(options);

        return NormalizeDefaults(options);
    }

    private static DobbyOptions NormalizeDefaults(DobbyOptions options)
    {
        return new DobbyOptions
        {
            Runtime = options.Runtime,
            Storage = options.Storage,
            Auth = options.Auth.LoginThrottleStepsSeconds.Length == 0
                ? new AuthOptions
                {
                    PasswordKdfMode = options.Auth.PasswordKdfMode,
                    LoginThrottleFailureWindowSeconds = options.Auth.LoginThrottleFailureWindowSeconds,
                    LoginGlobalFailureThreshold = options.Auth.LoginGlobalFailureThreshold,
                    LoginGlobalBackoffSeconds = options.Auth.LoginGlobalBackoffSeconds,
                    MaxConcurrentPasswordVerifications = options.Auth.MaxConcurrentPasswordVerifications
                }
                : options.Auth,
            Session = options.Session,
            Diagnostics = options.Diagnostics,
            Audit = options.Audit,
            Localization = options.Localization.SupportedCultures.Length == 0
                ? new LocalizationOptions { DefaultCulture = options.Localization.DefaultCulture }
                : options.Localization,
            Plugins = options.Plugins,
            Licensing = options.Licensing,
            BranchShared = options.BranchShared
        };
    }

    private static Dictionary<string, string?> ReadDobbyEnvironmentVariables()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in Environment.GetEnvironmentVariables().Keys.OfType<string>())
        {
            if (key.StartsWith("DOBBY__", StringComparison.OrdinalIgnoreCase))
            {
                values[key] = Environment.GetEnvironmentVariable(key);
            }
        }

        return values;
    }

    private static KeyValuePair<string, string?> ConvertEnvironmentVariable(KeyValuePair<string, string?> value)
    {
        var normalizedKey = value.Key.StartsWith("DOBBY__", StringComparison.OrdinalIgnoreCase)
            ? "Dobby:" + value.Key["DOBBY__".Length..].Replace("__", ":", StringComparison.Ordinal)
            : value.Key;

        return new KeyValuePair<string, string?>(normalizedKey, value.Value);
    }
}
