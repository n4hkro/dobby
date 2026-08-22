using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Manifest;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.PluginFramework;

public sealed class PluginFrameworkTests
{
    [Fact]
    public void Manifest_validator_rejects_prerelease_plugin_version()
    {
        var manifest = ValidManifest() with { Version = "1.0.0-preview.1" };

        var result = PluginManifestValidator.Validate(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.ValidationFailed);
    }

    [Fact]
    public void Manifest_validator_accepts_sprint6_canonical_configure_and_grant_verbs()
    {
        var manifest = ValidManifest() with
        {
            OperationTargets =
            [
                new PluginOperationTarget("com.dobby.sample.settings", ["configure", "grant"], "sample.settings.label")
            ]
        };

        var result = PluginManifestValidator.Validate(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Manifest_validator_still_rejects_unknown_operation_verbs()
    {
        var manifest = ValidManifest() with
        {
            OperationTargets =
            [
                new PluginOperationTarget("com.dobby.sample.settings", ["configure", "approve"], "sample.settings.label")
            ]
        };

        var result = PluginManifestValidator.Validate(manifest, new HostVersion(1, 0, 0));

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.ValidationFailed);
    }

    [Fact]
    public void Dependency_sorter_orders_dependencies_before_dependents()
    {
        var plugins = new[]
        {
            new PluginDependencyDescriptor("reports", [new PluginDependency("core", "1.0.0")]),
            new PluginDependencyDescriptor("core", [])
        };

        var result = PluginDependencySorter.Sort(plugins);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(x => x.PluginId).ShouldBe(["core", "reports"]);
    }

    [Fact]
    public async Task Plugin_exception_mapper_maps_already_cancelled_token_to_cancelled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await PluginExceptionBoundary.InvokeAsync(
            () => Task.FromResult("unreachable"),
            cancellation.Token);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Cancelled);
    }

    [Fact]
    public async Task Plugin_exception_mapper_maps_operation_canceled_exception_to_cancelled()
    {
        var result = await PluginExceptionBoundary.InvokeAsync<string>(
            () => Throw<string>(new OperationCanceledException()),
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Cancelled);
    }

    [Theory]
    [MemberData(nameof(ArgumentExceptions))]
    public async Task Plugin_exception_mapper_maps_argument_exceptions_to_invalid_input(Exception exception)
    {
        var result = await PluginExceptionBoundary.InvokeAsync<string>(
            () => Throw<string>(exception),
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Fact]
    public async Task Plugin_exception_mapper_maps_not_supported_to_unsupported_operation()
    {
        var result = await PluginExceptionBoundary.InvokeAsync<string>(
            () => Throw<string>(new NotSupportedException("raw detail")),
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.UnsupportedOperation);
    }

    [Fact]
    public async Task Plugin_exception_mapper_maps_generic_exception_to_plugin_internal_error()
    {
        var result = await PluginExceptionBoundary.InvokeAsync<string>(
            () => Throw<string>(new Exception("raw detail")),
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.PluginInternalError);
    }

    public static TheoryData<Exception> ArgumentExceptions()
    {
        return
        [
            new ArgumentException("bad argument"),
            new ArgumentNullException("value")
        ];
    }

    private static Task<T> Throw<T>(Exception exception)
    {
        throw exception;
    }

    private static PluginManifest ValidManifest()
    {
        return new PluginManifest(
            "com.dobby.sample",
            "dobby.vendor",
            "1.0.0",
            "1.0.0",
            "bin/plugins/com.dobby.sample.dll",
            "Sample Plugin",
            "Sample plugin description",
            null,
            [],
            [new PluginCapability("plugin-page", "plugin-page")],
            [new PluginOperationTarget("com.dobby.sample.greet", ["execute"], "sample.greet.label")],
            [new PluginExtensionPoint("sample.home", "plugin-page-region", "sample.home", "sample.home.label", null, "default", 100)],
            [new PluginPackageFile("bin/plugins/com.dobby.sample.dll", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")],
            new PluginPackageSignature(
                ArtifactSignaturePurposes.PluginPackage,
                ArtifactSignatureProfile.Algorithm,
                ArtifactSignatureProfile.Format,
                PluginTrustPublicKeyProvider.ProductionPluginPackageKeyId,
                ArtifactSignatureCodec.ToUnpaddedBase64Url(new byte[ArtifactSignatureProfile.P256SignatureLength])));
    }
}
