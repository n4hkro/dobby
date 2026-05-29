using Dobby.Configuration;
using Dobby.Host;
using Dobby.Host.Startup;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.IntegrationTests.Configuration;

public sealed class ConfigurationSourceOrderTests
{
    [Fact]
    public void Runtime_config_overrides_packaged_defaults_and_environment_overrides_runtime()
    {
        using var temp = new TemporaryDirectory();
        var packaged = Path.Combine(temp.Path, "packaged.json");
        var runtimeDirectory = Path.Combine(temp.Path, "config");
        Directory.CreateDirectory(runtimeDirectory);

        File.WriteAllText(packaged, """{ "Dobby": { "Runtime": { "EnvironmentName": "Production" } } }""");
        File.WriteAllText(Path.Combine(runtimeDirectory, "appsettings.json"), """{ "Dobby": { "Runtime": { "EnvironmentName": "Development" } } }""");

        var configuration = HostConfigurationSources.Build(
            new HostConfigurationSourceOptions(
                packaged,
                runtimeDirectory,
                "Production",
                new Dictionary<string, string?> { ["DOBBY__RUNTIME__ENVIRONMENTNAME"] = "Test" }));

        configuration["Dobby:Runtime:EnvironmentName"].ShouldBe("Test");
    }

    [Fact]
    public void Unknown_dobby_sections_are_reported_without_flagging_serilog()
    {
        using var temp = new TemporaryDirectory();
        var packaged = Path.Combine(temp.Path, "appsettings.json");
        File.WriteAllText(
            packaged,
            """{ "Dobby": { "Runtime": {}, "Unexpected": {} }, "Serilog": { "MinimumLevel": "Information" } }""");

        var configuration = HostConfigurationSources.Build(new HostConfigurationSourceOptions(packaged, temp.Path, "Production"));

        HostConfigurationSources.FindUnknownDobbySections(configuration).ShouldBe(["Unexpected"]);
    }

    [Fact]
    public void Environment_variables_round_trip_all_ADR_0027_keys()
    {
        using var temp = new TemporaryDirectory();
        var packaged = Path.Combine(temp.Path, "appsettings.json");
        File.WriteAllText(packaged, """{ "Dobby": {} }""");

        var configuration = HostConfigurationSources.Build(
            new HostConfigurationSourceOptions(
                packaged,
                temp.Path,
                "Production",
                new Dictionary<string, string?>
                {
                    ["DOBBY__RUNTIME__DATAROOT"] = @"C:\DobbyData",
                    ["DOBBY__RUNTIME__ENVIRONMENTNAME"] = "Test",
                    ["DOBBY__STORAGE__PROVIDERMODE"] = "SQLite3MultipleCiphers",
                    ["DOBBY__STORAGE__REQUIREDATAATRESTPROTECTION"] = "true",
                    ["DOBBY__STORAGE__BUSYTIMEOUTMILLISECONDS"] = "1000",
                    ["DOBBY__AUTH__LOGINTHROTTLESTEPSSECONDS__0"] = "0",
                    ["DOBBY__AUTH__LOGINTHROTTLESTEPSSECONDS__1"] = "0",
                    ["DOBBY__AUTH__LOGINTHROTTLESTEPSSECONDS__2"] = "10",
                    ["DOBBY__AUTH__LOGINTHROTTLESTEPSSECONDS__3"] = "30",
                    ["DOBBY__AUTH__LOGINTHROTTLESTEPSSECONDS__4"] = "60",
                    ["DOBBY__AUTH__PASSWORDKDFMODE"] = "Pbkdf2Sha256",
                    ["DOBBY__SESSION__ABSOLUTETIMEOUTMINUTES"] = "480",
                    ["DOBBY__SESSION__IDLETIMEOUTMINUTES"] = "30",
                    ["DOBBY__SESSION__AUTOLOCKENABLEDBYDEFAULT"] = "false",
                    ["DOBBY__DIAGNOSTICS__TECHNICALLOGRETENTIONDAYS"] = "30",
                    ["DOBBY__DIAGNOSTICS__TECHNICALLOGTOTALSIZECAPMEGABYTES"] = "256",
                    ["DOBBY__AUDIT__RETENTIONDAYS"] = "365",
                    ["DOBBY__LOCALIZATION__DEFAULTCULTURE"] = "az-AZ",
                    ["DOBBY__LOCALIZATION__SUPPORTEDCULTURES__0"] = "az-AZ",
                    ["DOBBY__LOCALIZATION__SUPPORTEDCULTURES__1"] = "en-US",
                    ["DOBBY__LOCALIZATION__SUPPORTEDCULTURES__2"] = "tr-TR",
                    ["DOBBY__PLUGINS__ALLOWPRELOGINDISCOVERY"] = "false",
                    ["DOBBY__LICENSING__IMPORTREQUIRESPERMISSION"] = "true",
                    ["DOBBY__BRANCHSHARED__RUNTIMEENABLED"] = "false"
                }));

        var options = HostConfigurationSources.BindDobbyOptions(configuration);

        options.Runtime.DataRoot.ShouldBe(@"C:\DobbyData");
        options.Runtime.EnvironmentName.ShouldBe(RuntimeEnvironmentName.Test);
        options.Storage.ProviderMode.ShouldBe(StorageProviderMode.SQLite3MultipleCiphers);
        options.Storage.RequireDataAtRestProtection.ShouldBeTrue();
        options.Storage.BusyTimeoutMilliseconds.ShouldBe(1000);
        options.Auth.LoginThrottleStepsSeconds.ShouldBe([0, 0, 10, 30, 60]);
        options.Auth.PasswordKdfMode.ShouldBe("Pbkdf2Sha256");
        options.Session.AbsoluteTimeoutMinutes.ShouldBe(480);
        options.Session.IdleTimeoutMinutes.ShouldBe(30);
        options.Session.AutoLockEnabledByDefault.ShouldBeFalse();
        options.Diagnostics.TechnicalLogRetentionDays.ShouldBe(30);
        options.Diagnostics.TechnicalLogTotalSizeCapMegabytes.ShouldBe(256);
        options.Audit.RetentionDays.ShouldBe(365);
        options.Localization.DefaultCulture.ShouldBe("az-AZ");
        options.Localization.SupportedCultures.ShouldBe(["az-AZ", "en-US", "tr-TR"]);
        options.Plugins.AllowPreLoginDiscovery.ShouldBeFalse();
        options.Licensing.ImportRequiresPermission.ShouldBeTrue();
        options.BranchShared.RuntimeEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Production_configuration_does_not_add_user_secrets_provider()
    {
        using var temp = new TemporaryDirectory();
        var packaged = Path.Combine(temp.Path, "appsettings.json");
        File.WriteAllText(packaged, """{ "Dobby": {} }""");

        var configuration = HostConfigurationSources.Build(new HostConfigurationSourceOptions(packaged, temp.Path, "Production"));

        configuration
            .Providers
            .Select(provider => provider.ToString() ?? string.Empty)
            .ShouldNotContain(provider => provider.Contains("secrets", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Malformed_security_critical_environment_values_fail_closed_at_startup()
    {
        using var temp = new TemporaryDirectory();
        var runtimeRoot = Path.Combine(temp.Path, "runtime-root");
        var packaged = Path.Combine(temp.Path, "appsettings.json");
        File.WriteAllText(packaged, BootstrapAppSettings(runtimeRoot));

        var exception = Should.Throw<OptionsValidationException>(() =>
            HostBootstrapper.Bootstrap(new HostBootstrapOptions(
                ShowMainWindow: false,
                PackagedAppSettingsPath: packaged,
                RuntimeConfigDirectory: Path.Combine(temp.Path, "config"),
                EnvironmentName: "Test",
                EnvironmentVariables: new Dictionary<string, string?>
                {
                    ["DOBBY__STORAGE__REQUIREDATAATRESTPROTECTION"] = "false"
                })));

        exception.Failures.ShouldContain(failure => failure.Contains("RequireDataAtRestProtection", StringComparison.Ordinal));
    }

    private static string BootstrapAppSettings(string runtimeRoot)
    {
        return $$"""
            {
              "Dobby": {
                "Runtime": { "DataRoot": "{{runtimeRoot.Replace("\\", "\\\\")}}", "EnvironmentName": "Test" },
                "Storage": { "ProviderMode": "SQLite3MultipleCiphers", "RequireDataAtRestProtection": true, "BusyTimeoutMilliseconds": 5000 },
                "Auth": { "LoginThrottleStepsSeconds": [0, 0, 10, 30, 60], "PasswordKdfMode": "Pbkdf2Sha256" },
                "Session": { "AbsoluteTimeoutMinutes": 480, "IdleTimeoutMinutes": 30, "AutoLockEnabledByDefault": false },
                "Diagnostics": { "TechnicalLogRetentionDays": 30, "TechnicalLogTotalSizeCapMegabytes": 256 },
                "Audit": { "RetentionDays": 365 },
                "Localization": { "DefaultCulture": "az-AZ", "SupportedCultures": ["az-AZ", "en-US", "tr-TR"] },
                "Plugins": { "AllowPreLoginDiscovery": false },
                "Licensing": { "ImportRequiresPermission": true },
                "BranchShared": { "RuntimeEnabled": false }
              },
              "Serilog": { "MinimumLevel": "Information" }
            }
            """;
    }
}
