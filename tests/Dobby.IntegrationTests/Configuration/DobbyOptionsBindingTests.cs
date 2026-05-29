using Dobby.Configuration;
using Dobby.Host;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Dobby.IntegrationTests.Configuration;

public sealed class DobbyOptionsBindingTests
{
    [Fact]
    public void Full_ADR_0027_schema_binds_to_typed_options()
    {
        using var temp = new TemporaryDirectory();
        var appsettings = Path.Combine(temp.Path, "appsettings.json");
        File.WriteAllText(appsettings, CanonicalJson());

        var configuration = HostConfigurationSources.Build(new HostConfigurationSourceOptions(appsettings, temp.Path, "Production"));
        var options = HostConfigurationSources.BindDobbyOptions(configuration);

        options.ShouldNotBeNull();
        options.Runtime.DataRoot.ShouldBe(@"C:\Users\Test\AppData\Local\Dobby");
        options.Runtime.EnvironmentName.ShouldBe(RuntimeEnvironmentName.Production);
        options.Storage.ProviderMode.ShouldBe(StorageProviderMode.SQLite3MultipleCiphers);
        options.Auth.LoginThrottleStepsSeconds.ShouldBe([0, 0, 10, 30, 60]);
        options.Auth.PasswordKdfMode.ShouldBe("Pbkdf2Sha256");
        options.Localization.SupportedCultures.ShouldBe(["az-AZ", "en-US", "tr-TR"]);
        options.BranchShared.RuntimeEnabled.ShouldBeFalse();
    }

    private static string CanonicalJson()
    {
        return """
            {
              "Dobby": {
                "Runtime": { "DataRoot": "C:\\Users\\Test\\AppData\\Local\\Dobby", "EnvironmentName": "Production" },
                "Storage": { "ProviderMode": "SQLite3MultipleCiphers", "RequireDataAtRestProtection": true, "BusyTimeoutMilliseconds": 5000 },
                "Auth": { "LoginThrottleStepsSeconds": [0, 0, 10, 30, 60], "PasswordKdfMode": "Pbkdf2Sha256" },
                "Session": { "AbsoluteTimeoutMinutes": 480, "IdleTimeoutMinutes": 30, "AutoLockEnabledByDefault": false },
                "Diagnostics": { "TechnicalLogRetentionDays": 30, "TechnicalLogTotalSizeCapMegabytes": 256 },
                "Audit": { "RetentionDays": 365 },
                "Localization": { "DefaultCulture": "az-AZ", "SupportedCultures": [ "az-AZ", "en-US", "tr-TR" ] },
                "Plugins": { "AllowPreLoginDiscovery": false },
                "Licensing": { "ImportRequiresPermission": true },
                "BranchShared": { "RuntimeEnabled": false }
              }
            }
            """;
    }
}
