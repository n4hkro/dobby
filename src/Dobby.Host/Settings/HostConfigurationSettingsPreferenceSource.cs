using Dobby.Configuration;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using System.Globalization;
using System.Text.Json;

namespace Dobby.Host.Settings;

public sealed class HostConfigurationSettingsPreferenceSource(
    IHostPersistenceCommandExecutor executor,
    SettingsPreferenceScope? scope = null) : ISettingsPreferenceSource
{
    private const string Source = "settings";
    private const string ThemeKey = "settings.appearance.theme";
    private const string AutoLockEnabledKey = "settings.security-session.auto-lock-enabled";
    private const string AutoLockMinutesKey = "settings.security-session.auto-lock-minutes";
    private const string SidebarCollapsedKey = "settings.shell.sidebar-collapsed";
    private const string SidebarOpenNodeIdsKey = "settings.shell.sidebar-open-node-ids";
    private const string SidebarRouteOrderKey = "settings.shell.sidebar-route-order";
    private const string ThirdBarRouteOrderKey = "settings.shell.third-bar-route-order";
    private const string TopToolbarPluginOrderKey = "settings.shell.top-toolbar-plugin-order";
    private const string HiddenTopToolbarPluginIdsKey = "settings.shell.hidden-top-toolbar-plugin-ids";
    private const string WindowWidthKey = "settings.shell.window-width";
    private const string WindowHeightKey = "settings.shell.window-height";
    private const string WindowLeftKey = "settings.shell.window-left";
    private const string WindowTopKey = "settings.shell.window-top";
    private const string LastActiveModuleRouteKey = "settings.shell.last-active-module-route-key";
    private const string RestoreLastActiveModuleOnStartupKey = "settings.shell.restore-last-active-module-on-startup";

    public Result<SettingsPreferences> Load()
    {
        var values = executor.Query<HostConfigurationRow>(
                "SELECT ConfigKey, ConfigValue FROM HostConfiguration WHERE Source = @Source;",
                new { Source })
            .ToDictionary(row => row.ConfigKey, row => row.ConfigValue, StringComparer.Ordinal);

        var defaults = SettingsPreferences.Default;
        var theme = values.TryGetValue(ScopedKey(ThemeKey), out var themeValue) &&
            Enum.TryParse<AppThemePreference>(themeValue, ignoreCase: false, out var parsedTheme)
            ? parsedTheme
            : defaults.Appearance.Theme;
        var autoLockEnabled = values.TryGetValue(ScopedKey(AutoLockEnabledKey), out var enabledValue) &&
            bool.TryParse(enabledValue, out var parsedEnabled)
            ? parsedEnabled
            : defaults.SecuritySession.AutoLockEnabled;
        var autoLockMinutes = values.TryGetValue(ScopedKey(AutoLockMinutesKey), out var minutesValue) &&
            int.TryParse(minutesValue, out var parsedMinutes) &&
            parsedMinutes > 0
            ? parsedMinutes
            : defaults.SecuritySession.AutoLockMinutes;

        return Result.Success(new SettingsPreferences(
            new AppearancePreference(theme),
            new SecuritySessionPreference(autoLockEnabled, autoLockMinutes),
            LoadShellView(values, defaults.ShellView)));
    }

    public Result Save(SettingsPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var validation = SecuritySessionPreference.Create(
            preferences.SecuritySession.AutoLockEnabled,
            preferences.SecuritySession.AutoLockMinutes);
        if (!validation.IsSuccess)
        {
            return Result.Failure(validation.ErrorCode!.Value);
        }

        Upsert(ScopedKey(ThemeKey), preferences.Appearance.Theme.ToString(), "string");
        Upsert(ScopedKey(AutoLockEnabledKey), preferences.SecuritySession.AutoLockEnabled.ToString(), "boolean");
        Upsert(ScopedKey(AutoLockMinutesKey), preferences.SecuritySession.AutoLockMinutes.ToString(CultureInfo.InvariantCulture), "integer");
        if (scope is not null)
        {
            SaveShellView(preferences.ShellView);
        }

        return Result.Success();
    }

    private ShellViewPreference LoadShellView(IReadOnlyDictionary<string, string?> values, ShellViewPreference defaults)
    {
        if (scope is null)
        {
            return defaults;
        }

        return new ShellViewPreference(
            ReadBool(values, SidebarCollapsedKey, defaults.SidebarCollapsed),
            ReadList(values, SidebarOpenNodeIdsKey, defaults.SidebarOpenNodeIds),
            ReadList(values, SidebarRouteOrderKey, defaults.SidebarRouteOrder),
            ReadList(values, ThirdBarRouteOrderKey, defaults.ThirdBarRouteOrder),
            ReadList(values, TopToolbarPluginOrderKey, defaults.TopToolbarPluginOrder),
            ReadList(values, HiddenTopToolbarPluginIdsKey, defaults.HiddenTopToolbarPluginIds),
            ReadDouble(values, WindowWidthKey, defaults.WindowWidth),
            ReadDouble(values, WindowHeightKey, defaults.WindowHeight),
            ReadNullableDouble(values, WindowLeftKey, defaults.WindowLeft),
            ReadNullableDouble(values, WindowTopKey, defaults.WindowTop),
            ReadString(values, LastActiveModuleRouteKey, defaults.LastActiveModuleRouteKey),
            ReadBool(values, RestoreLastActiveModuleOnStartupKey, defaults.RestoreLastActiveModuleOnStartup));
    }

    private void SaveShellView(ShellViewPreference shellView)
    {
        Upsert(ScopedKey(SidebarCollapsedKey), shellView.SidebarCollapsed.ToString(), "boolean");
        Upsert(ScopedKey(SidebarOpenNodeIdsKey), JsonSerializer.Serialize(shellView.SidebarOpenNodeIds), "json");
        Upsert(ScopedKey(SidebarRouteOrderKey), JsonSerializer.Serialize(shellView.SidebarRouteOrder), "json");
        Upsert(ScopedKey(ThirdBarRouteOrderKey), JsonSerializer.Serialize(shellView.ThirdBarRouteOrder), "json");
        Upsert(ScopedKey(TopToolbarPluginOrderKey), JsonSerializer.Serialize(shellView.TopToolbarPluginOrder), "json");
        Upsert(ScopedKey(HiddenTopToolbarPluginIdsKey), JsonSerializer.Serialize(shellView.HiddenTopToolbarPluginIds), "json");
        Upsert(ScopedKey(WindowWidthKey), shellView.WindowWidth.ToString(CultureInfo.InvariantCulture), "number");
        Upsert(ScopedKey(WindowHeightKey), shellView.WindowHeight.ToString(CultureInfo.InvariantCulture), "number");
        Upsert(ScopedKey(WindowLeftKey), shellView.WindowLeft?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, "number");
        Upsert(ScopedKey(WindowTopKey), shellView.WindowTop?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, "number");
        Upsert(ScopedKey(LastActiveModuleRouteKey), shellView.LastActiveModuleRouteKey ?? string.Empty, "string");
        Upsert(ScopedKey(RestoreLastActiveModuleOnStartupKey), shellView.RestoreLastActiveModuleOnStartup.ToString(), "boolean");
    }

    private string ScopedKey(string key)
    {
        if (scope is null)
        {
            return key;
        }

        const string settingsPrefix = "settings.";
        return key.StartsWith(settingsPrefix, StringComparison.Ordinal)
            ? $"{scope.KeyPrefix}.{key[settingsPrefix.Length..]}"
            : $"{scope.KeyPrefix}.{key}";
    }

    private bool ReadBool(IReadOnlyDictionary<string, string?> values, string key, bool defaultValue)
    {
        return values.TryGetValue(ScopedKey(key), out var value) &&
            bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;
    }

    private double ReadDouble(IReadOnlyDictionary<string, string?> values, string key, double defaultValue)
    {
        return values.TryGetValue(ScopedKey(key), out var value) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > 0
            ? parsed
            : defaultValue;
    }

    private double? ReadNullableDouble(IReadOnlyDictionary<string, string?> values, string key, double? defaultValue)
    {
        return values.TryGetValue(ScopedKey(key), out var value) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    private string? ReadString(IReadOnlyDictionary<string, string?> values, string key, string? defaultValue)
    {
        return values.TryGetValue(ScopedKey(key), out var value) && !string.IsNullOrEmpty(value)
            ? value
            : defaultValue;
    }

    private IReadOnlyList<string> ReadList(
        IReadOnlyDictionary<string, string?> values,
        string key,
        IReadOnlyList<string> defaultValue)
    {
        if (!values.TryGetValue(ScopedKey(key), out var value) || string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(value) ?? defaultValue;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    private void Upsert(string key, string value, string valueKind)
    {
        executor.Execute(
            """
            INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc)
            VALUES (@ConfigKey, @ConfigValue, @ValueKind, NULL, @Source, @UpdatedAtUtc)
            ON CONFLICT(ConfigKey) DO UPDATE SET
                ConfigValue = excluded.ConfigValue,
                ValueKind = excluded.ValueKind,
                Source = excluded.Source,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """,
            new
            {
                ConfigKey = key,
                ConfigValue = value,
                ValueKind = valueKind,
                Source,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
    }

    private sealed class HostConfigurationRow
    {
        public string ConfigKey { get; init; } = string.Empty;

        public string? ConfigValue { get; init; }
    }
}
