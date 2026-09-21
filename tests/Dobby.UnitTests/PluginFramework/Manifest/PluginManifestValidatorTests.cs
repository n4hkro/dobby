using Dobby.Core.Versioning;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Manifest;
using Dobby.UnitTests.PluginFramework.Packaging;
using Shouldly;

namespace Dobby.UnitTests.PluginFramework.Manifest;

public sealed class PluginManifestValidatorTests
{
    [Fact]
    public void Validate_detailed_accepts_canonical_manifest()
    {
        var manifest = PluginPackageTestData.CreateManifest();

        var result = PluginManifestValidator.ValidateDetailed(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeTrue();
        result.Manifest.ShouldBe(manifest);
    }

    [Fact]
    public void Validate_detailed_rejects_self_dependency()
    {
        var manifest = PluginPackageTestData.CreateManifest(
            dependsOn:
            [
                new PluginManifestDependency(PluginPackageTestData.PluginId, "1.0.0")
            ]);

        var result = PluginManifestValidator.ValidateDetailed(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Single().Code.ShouldBe("self-dependency");
    }

    [Fact]
    public void Validate_detailed_rejects_operation_target_outside_plugin_namespace()
    {
        var manifest = PluginPackageTestData.CreateManifest(
            operationTargets:
            [
                new PluginOperationTarget("com.other.sample.greet", ["execute"], "sample.greet.label")
            ]);

        var result = PluginManifestValidator.ValidateDetailed(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Any(reason => reason.Path == "operationTargets[0].id").ShouldBeTrue();
    }

    [Fact]
    public void Validate_detailed_rejects_operation_target_missing_label_duplicate_id_and_duplicate_verbs()
    {
        var manifest = PluginPackageTestData.CreateManifest(
            operationTargets:
            [
                new PluginOperationTarget($"{PluginPackageTestData.PluginId}.greet", ["execute", "execute"], ""),
                new PluginOperationTarget($"{PluginPackageTestData.PluginId}.greet", ["fly"], "sample.greet.label")
            ]);

        var result = PluginManifestValidator.ValidateDetailed(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Any(reason => reason.Path == "operationTargets[0].labelKey").ShouldBeTrue();
        result.Reasons.Any(reason => reason.Path == "operationTargets[0].verbs").ShouldBeTrue();
        result.Reasons.Any(reason => reason.Path == "operationTargets[1].id").ShouldBeTrue();
        result.Reasons.Any(reason => reason.Path == "operationTargets[1].verbs[0]").ShouldBeTrue();
    }

    [Fact]
    public void Validate_detailed_rejects_duplicate_extension_id_missing_label_and_missing_parent()
    {
        var manifest = PluginPackageTestData.CreateManifest(
            extensionPoints:
            [
                new PluginExtensionPoint("sample.home", "plugin-page-region", "sample.home", null, null, "default", 10),
                new PluginExtensionPoint("sample.home", "plugin-page-region", "sample.child", "sample.child.label", "missing-parent", "default", 20)
            ]);

        var result = PluginManifestValidator.ValidateDetailed(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Any(reason => reason.Path == "extensionPoints[0].labelKey").ShouldBeTrue();
        result.Reasons.Any(reason => reason.Path == "extensionPoints[1].id").ShouldBeTrue();
        result.Reasons.Any(reason => reason.Code == "missing-parent").ShouldBeTrue();
    }

    [Fact]
    public void Validate_detailed_allows_top_toolbar_label_fallback_to_display_name()
    {
        var manifest = PluginPackageTestData.CreateManifest(
            extensionPoints:
            [
                new PluginExtensionPoint("sample.home", "top-toolbar", "sample.home", null, null, null, null)
            ]);

        var result = PluginManifestValidator.ValidateDetailed(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeTrue(result.Reasons.FirstOrDefault()?.Message);
    }

    [Theory]
    [InlineData("plugin-page-region", "sample/home")]
    [InlineData("unknown-target", "sample.home")]
    [InlineData("plugin-page-region", "")]
    public void Validate_detailed_enforces_extension_target_and_route_shape(string target, string routeKey)
    {
        var manifest = PluginPackageTestData.CreateManifest(
            extensionPoints:
            [
                new PluginExtensionPoint("sample.home", target, routeKey, "sample.home.label", null, "default", 10)
            ]);

        var result = PluginManifestValidator.ValidateDetailed(manifest, new HostVersion(1, 0, 0));

        if (target.Equals("plugin-page-region", StringComparison.Ordinal) &&
            routeKey.Equals("sample/home", StringComparison.Ordinal))
        {
            result.IsSuccess.ShouldBeTrue();
            return;
        }

        result.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData("sample home", "sample.home", null, "extensionPoints[0].id")]
    [InlineData("sample.home", "sample home/dashboard", null, "extensionPoints[0].routeKey")]
    [InlineData("sample.child", "sample.home", "bad parent", "extensionPoints[sample.child].parentId")]
    public void Validate_detailed_rejects_extension_identifiers_that_do_not_match_schema(
        string extensionId,
        string routeKey,
        string? parentId,
        string expectedPath)
    {
        var manifest = PluginPackageTestData.CreateManifest(
            extensionPoints:
            [
                new PluginExtensionPoint(extensionId, "plugin-page-region", routeKey, "sample.home.label", parentId, "default", 10)
            ]);

        var result = PluginManifestValidator.ValidateDetailed(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Any(reason =>
            reason.Code == "invalid-manifest" &&
            reason.Path == expectedPath).ShouldBeTrue();
    }

    [Fact]
    public void Validate_detailed_rejects_empty_package_file_list()
    {
        var manifest = PluginPackageTestData.CreateManifest(files: []);

        var result = PluginManifestValidator.ValidateDetailed(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Any(reason =>
            reason.Code == "invalid-manifest" &&
            reason.Path == "files").ShouldBeTrue();
    }

    [Fact]
    public void Validate_detailed_rejects_extension_parent_cycle()
    {
        var manifest = PluginPackageTestData.CreateManifest(
            extensionPoints:
            [
                new PluginExtensionPoint("alpha", "plugin-page-region", "alpha", "alpha.label", "beta", "default", 10),
                new PluginExtensionPoint("beta", "plugin-page-region", "beta", "beta.label", "alpha", "default", 20)
            ]);

        var result = PluginManifestValidator.ValidateDetailed(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Any(reason => reason.Code == "parent-cycle").ShouldBeTrue();
    }

    [Fact]
    public void Legacy_v1_manifest_is_rejected_at_install_validation_and_at_activation_reverification()
    {
        var legacy = PluginPackageTestData.CreateManifest() with
        {
            SchemaVersion = PluginManifestContract.LegacySchemaVersion
        };

        var installValidation = PluginManifestValidator.ValidateDetailed(legacy, new HostVersion(1, 0, 0));

        installValidation.IsSuccess.ShouldBeFalse();
        installValidation.Reasons.Any(reason =>
                reason.Code == "unsupported-manifest-version" && reason.Path == "schemaVersion")
            .ShouldBeTrue(
                "docs/schemas/artifact-compatibility-matrix.md declares no v1 manifest read path: " +
                "install validation must reject schemaVersion 1 fail-closed");

        var activationReverification = PluginCompatibilityEvaluator.Evaluate(legacy, new HostVersion(1, 0, 0));

        activationReverification.IsCompatible.ShouldBeFalse();
        activationReverification.Status.ShouldBe(PluginActivationStatus.Rejected);
        activationReverification.Reason.ShouldBe(
            "unsupported-manifest-version",
            "AC-049 re-verifies the manifest at activation, so a v1 manifest that somehow reached " +
            "installed state must still never be activated");
    }

    [Fact]
    public void Only_the_current_manifest_schema_version_maps_to_a_supported_package_format()
    {
        PluginCompatibilityMatrix.PackageFormatVersionForSchema(PluginManifestContract.CurrentSchemaVersion)
            .ShouldBe(PluginCompatibilityMatrix.CurrentPackageFormatVersion);

        foreach (var unsupported in new[] { PluginManifestContract.LegacySchemaVersion, 0, -1, 3, int.MaxValue })
        {
            PluginCompatibilityMatrix.PackageFormatVersionForSchema(unsupported).ShouldBe(
                0,
                "an unmapped schema version must not resolve to a usable package format version");
        }
    }
}
