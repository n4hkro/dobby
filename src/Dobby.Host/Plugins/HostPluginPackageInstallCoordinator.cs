using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Trust;
using System.IO;

namespace Dobby.Host.Plugins;

public sealed class HostPluginPackageInstallCoordinator(
    IInstalledPluginStateStore stateStore,
    IInstalledPluginMetadataCache metadataCache,
    IPluginInstallStagingArea stagingArea,
    PluginRuntimeAuditCoordinator auditCoordinator,
    IPluginTrustVerifier pluginTrustVerifier,
    PluginRuntimeDiagnosticsCoordinator? diagnosticsCoordinator = null)
{
    private readonly IPluginTrustVerifier trustVerifier = pluginTrustVerifier ?? throw new ArgumentNullException(nameof(pluginTrustVerifier));
    private readonly PluginRuntimeDiagnosticsCoordinator? diagnostics = diagnosticsCoordinator;

    public Result<InstalledPluginState> InstallPackage(
        string actorId,
        string packagePath,
        HostVersion hostVersion,
        CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(auditCoordinator);

        auditCoordinator.AppendInstallAttempt(actorId, packagePath, correlationId);

        if (!IsSupportedSourcePackage(packagePath))
        {
            var reasons = new[]
            {
                new PluginPackageValidationReason(
                    "unsupported-package-source",
                    "Plugin packages must be installed from a local .dobbyplugin file.",
                    packagePath)
            };
            auditCoordinator.AppendInstallRejected(
                actorId,
                pluginId: null,
                reasons,
                correlationId);
            diagnostics?.RecordInstallRejected(null, reasons);
            return Result.Failure<InstalledPluginState>(ErrorCode.InvalidInput);
        }

        var archive = PluginPackageReader.Read(packagePath);
        if (!archive.IsSuccess)
        {
            var reasons = new[]
            {
                new PluginPackageValidationReason("invalid-manifest", "Plugin package could not be read.", PluginPackageReader.ManifestFileName)
            };
            auditCoordinator.AppendInstallRejected(
                actorId,
                pluginId: null,
                reasons,
                correlationId);
            diagnostics?.RecordInstallRejected(null, reasons);
            return Result.Failure<InstalledPluginState>(archive.ErrorCode ?? ErrorCode.InvalidInput);
        }

        var validation = PluginPackageValidator.Validate(archive.Value, hostVersion, trustVerifier);
        if (!validation.IsSuccess)
        {
            auditCoordinator.AppendInstallRejected(
                actorId,
                archive.Value.Manifest.PluginId,
                validation.Reasons,
                correlationId);
            diagnostics?.RecordInstallRejected(archive.Value.Manifest.PluginId, validation.Reasons);
            return Result.Failure<InstalledPluginState>(ErrorCode.ValidationFailed);
        }

        var descriptor = validation.Descriptor!;
        var install = new PluginPackageInstaller(stagingArea, stateStore, metadataCache).Install(descriptor);
        if (!install.IsSuccess)
        {
            var reasons = new[]
            {
                new PluginPackageValidationReason("install-failed", "Plugin package installation failed.")
            };
            auditCoordinator.AppendInstallRejected(
                actorId,
                descriptor.Manifest.PluginId,
                reasons,
                correlationId);
            diagnostics?.RecordInstallRejected(descriptor.Manifest.PluginId, reasons);
            return install;
        }

        auditCoordinator.AppendInstallVerified(actorId, descriptor, correlationId);
        return install;
    }

    private static bool IsSupportedSourcePackage(string packagePath)
    {
        return File.Exists(packagePath) &&
            Path.GetExtension(packagePath).Equals(".dobbyplugin", StringComparison.OrdinalIgnoreCase);
    }
}
