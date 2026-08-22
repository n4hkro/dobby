using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class StorageOptionsTests
{
    [Fact]
    public void Storage_security_floor_cannot_be_lowered()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Storage = new StorageOptions
                {
                    RequireDataAtRestProtection = false,
                    BusyTimeoutMilliseconds = 50
                }
            });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("RequireDataAtRestProtection", StringComparison.Ordinal));
        result.Failures.ShouldContain(x => x.Contains("BusyTimeoutMilliseconds", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_storage_provider_must_use_approved_encrypted_provider()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Runtime = new RuntimeOptions { EnvironmentName = RuntimeEnvironmentName.Production },
                Storage = new StorageOptions { ProviderMode = StorageProviderMode.ProviderNeutralSkeleton }
            });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("SQLite3MultipleCiphers", StringComparison.Ordinal));
    }

    [Fact]
    public void Approved_encrypted_provider_is_valid_in_production()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Runtime = new RuntimeOptions { EnvironmentName = RuntimeEnvironmentName.Production },
                Storage = new StorageOptions { ProviderMode = StorageProviderMode.SQLite3MultipleCiphers },
                Product = new ProductOptions
                {
                    SupportContact = "https://github.com/n4hkro/dobby/issues",
                    LegalLine = "Copyright © 2026 Dobby contributors"
                }
            });

        result.Failed.ShouldBeFalse();
    }

    [Fact]
    public void Provider_neutral_skeleton_remains_valid_outside_production_test_fixtures()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Runtime = new RuntimeOptions { EnvironmentName = RuntimeEnvironmentName.Test },
                Storage = new StorageOptions { ProviderMode = StorageProviderMode.ProviderNeutralSkeleton }
            });

        result.Failed.ShouldBeFalse();
    }
}
