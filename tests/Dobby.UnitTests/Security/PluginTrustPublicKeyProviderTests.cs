using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class PluginTrustPublicKeyProviderTests
{
    [Fact]
    public void Resolve_returns_selected_production_resource()
    {
        using var key = PluginTrustTestData.CreateKeyPair(out var publicKeyDer, out var publicKeyPem);
        var provider = new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionResourceName] = publicKeyPem
            });

        var result = provider.Resolve();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ResourceName.ShouldBe(PluginTrustPublicKeyProvider.ProductionResourceName);
        result.Value.Profile.ShouldBe(DobbyPluginTrustProfile.Production);
        result.Value.Purpose.ShouldBe(ArtifactSignaturePurposes.PluginPackage);
        result.Value.KeyId.ShouldBe(PluginTrustPublicKeyProvider.ProductionPluginPackageKeyId);
        result.Value.PublicKeyBytes.ShouldBe(publicKeyDer);
        result.Value.Sha256.ShouldBe(PluginTrustTestData.HashHex(publicKeyDer));
    }

    [Fact]
    public void Resolve_rejects_missing_resource()
    {
        var provider = new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>());

        var result = provider.Resolve();

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("trust-failed");
    }

    [Fact]
    public void Resolve_rejects_mixed_profile_resources()
    {
        using var key = PluginTrustTestData.CreateKeyPair(out _, out var productionPem);
        using var devQaKey = PluginTrustTestData.CreateKeyPair(out _, out var devQaPem);
        var provider = new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionResourceName] = productionPem,
                [PluginTrustPublicKeyProvider.DevQaResourceName] = devQaPem
            });

        var result = provider.Resolve();

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("trust-failed");
    }

    [Fact]
    public void Resolve_rejects_empty_resource()
    {
        var provider = new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionResourceName] = []
            });

        var result = provider.Resolve();

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("trust-failed");
        result.Message.ShouldContain("empty");
    }

    [Fact]
    public void Resolve_rejects_malformed_pem_resource()
    {
        var provider = new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionResourceName] = "not a pem"u8.ToArray()
            });

        var result = provider.Resolve();

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("trust-failed");
        result.Message.ShouldContain("malformed");
    }

    [Fact]
    public void Resolve_rejects_unexpected_logical_name_without_filesystem_fallback()
    {
        using var key = PluginTrustTestData.CreateKeyPair(out _, out var publicKeyPem);
        var provider = new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                ["Dobby.Security.Trust.unexpected-public-key.pem"] = publicKeyPem
            });

        var result = provider.Resolve();

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("trust-failed");
        result.Message.ShouldContain("Missing trust resource");
    }

    [Fact]
    public void Public_api_does_not_accept_user_writable_key_paths()
    {
        var constructors = typeof(PluginTrustPublicKeyProvider).GetConstructors();

        constructors
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter =>
                parameter.ParameterType == typeof(string) &&
                parameter.Name!.Contains("path", StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse();

        constructors
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(IReadOnlyDictionary<string, byte[]>))
            .ShouldBeFalse();
    }

    [Fact]
    public void Resolve_uses_a_distinct_license_resource_key_and_purpose()
    {
        using var key = PluginTrustTestData.CreateKeyPair(out var publicKeyDer, out var publicKeyPem);
        var provider = new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionLicenseResourceName] = publicKeyPem
            },
            ArtifactSignaturePurposes.PluginLicense);

        var result = provider.Resolve();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Purpose.ShouldBe(ArtifactSignaturePurposes.PluginLicense);
        result.Value.KeyId.ShouldBe(PluginTrustPublicKeyProvider.ProductionPluginLicenseKeyId);
        result.Value.PublicKeyBytes.ShouldBe(publicKeyDer);
        result.Value.ResourceName.ShouldNotBe(PluginTrustPublicKeyProvider.ProductionResourceName);
    }
}
