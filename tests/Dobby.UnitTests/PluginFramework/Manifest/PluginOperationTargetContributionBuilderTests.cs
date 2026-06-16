using Dobby.Core.Versioning;
using Dobby.PluginFramework.Manifest;
using Dobby.Security.Authorization;
using Dobby.UnitTests.PluginFramework.Packaging;
using Shouldly;

namespace Dobby.UnitTests.PluginFramework.Manifest;

public sealed class PluginOperationTargetContributionBuilderTests
{
    [Fact]
    public void Build_returns_contributions_from_valid_manifest()
    {
        var manifest = PluginPackageTestData.CreateManifest(
            operationTargets:
            [
                new PluginOperationTarget($"{PluginPackageTestData.PluginId}.settings", ["configure", "grant"], "sample.settings.label")
            ]);

        var result = PluginOperationTargetContributionBuilder.Build(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeTrue();
        var contribution = result.Contributions.Single();
        contribution.PluginId.ShouldBe(PluginPackageTestData.PluginId);
        contribution.TargetId.ShouldBe($"{PluginPackageTestData.PluginId}.settings");
        contribution.LabelKey.ShouldBe("sample.settings.label");
        contribution.Verbs.ShouldBe([OperationVerb.Configure, OperationVerb.Grant]);
    }

    [Fact]
    public void Build_returns_no_contributions_when_manifest_validation_fails()
    {
        var manifest = PluginPackageTestData.CreateManifest(
            operationTargets:
            [
                new PluginOperationTarget("com.other.settings", ["configure"], "sample.settings.label")
            ]);

        var result = PluginOperationTargetContributionBuilder.Build(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeFalse();
        result.Contributions.ShouldBeEmpty();
        result.Reasons.Any(reason => reason.Path == "operationTargets[0].id").ShouldBeTrue();
    }
}
