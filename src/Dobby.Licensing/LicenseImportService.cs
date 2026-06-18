using System.Security.Cryptography;
using Dobby.Security.CurrentContext;

namespace Dobby.Licensing;

public sealed class LicenseImportService
{
    private readonly LicenseSignatureVerifier signatureVerifier;
    private readonly CurrentCustomerDeploymentContextService currentContextService;
    private readonly ILicenseStateStore stateStore;

    public LicenseImportService(
        LicenseSignatureVerifier signatureVerifier,
        CurrentCustomerDeploymentContextService currentContextService,
        ILicenseStateStore stateStore)
    {
        this.signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        this.currentContextService = currentContextService ?? throw new ArgumentNullException(nameof(currentContextService));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public LicenseImportResult Import(LicenseImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.PermissionGranted)
        {
            AppendAttestation(request, licenseRevision: null, LicenseRejectReasons.PermissionDenied);
            return LicenseImportResult.Failure(LicenseRejectReasons.PermissionDenied, "permission-denied");
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled(request, licenseRevision: null);
        }

        var parse = LicenseArtifactParser.Parse(request.FileName, request.ArtifactBytes);
        if (!parse.IsSuccess)
        {
            AppendAttestation(request, licenseRevision: null, parse.ReasonCode);
            return LicenseImportResult.Failure(parse.ReasonCode, parse.ReasonCode);
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled(request, licenseRevision: null);
        }

        var signature = signatureVerifier.Verify(parse.Artifact!.Signature, parse.Artifact.CanonicalPayload);
        if (!signature.IsSuccess)
        {
            AppendAttestation(request, licenseRevision: null, signature.ReasonCode);
            return LicenseImportResult.Failure(signature.ReasonCode, signature.SupportCode);
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled(request, licenseRevision: null);
        }

        var payloadValidation = LicensePayloadValidator.Validate(parse.Artifact);
        if (!payloadValidation.IsSuccess)
        {
            AppendAttestation(request, licenseRevision: null, payloadValidation.ReasonCode);
            return LicenseImportResult.Failure(payloadValidation.ReasonCode, payloadValidation.ReasonCode);
        }

        var payload = payloadValidation.Payload!;
        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled(request, payload.LicenseRevision);
        }

        var context = currentContextService.ValidateMatches(payload.CustomerId, payload.DeploymentId);
        if (!context.IsSuccess)
        {
            AppendAttestation(request, payload.LicenseRevision, context.ReasonCode);
            return LicenseImportResult.Failure(context.ReasonCode, context.SupportCode);
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled(request, payload.LicenseRevision);
        }

        var currentDeviceFingerprint = request.CurrentDeviceFingerprintProvider();
        if (!LicenseDeviceFingerprintV1.Matches(currentDeviceFingerprint, payload.DeviceFingerprint))
        {
            AppendAttestation(request, payload.LicenseRevision, LicenseRejectReasons.BindingMismatch);
            return LicenseImportResult.Failure(LicenseRejectReasons.BindingMismatch, "device-binding-mismatch");
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled(request, payload.LicenseRevision);
        }

        var installedPackageFingerprint = request.InstalledPackageFingerprintResolver(payload.PluginId);
        if (!payload.PackageFingerprint.Equals(installedPackageFingerprint, StringComparison.Ordinal))
        {
            AppendAttestation(request, payload.LicenseRevision, LicenseRejectReasons.PackageMismatch);
            return LicenseImportResult.Failure(LicenseRejectReasons.PackageMismatch, "package-binding-mismatch");
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled(request, payload.LicenseRevision);
        }

        var key = new RevisionSeriesKey(payload.PluginId, payload.CustomerId, payload.DeploymentId);
        var existing = stateStore.Read(key);
        if (!LicenseRevisionPolicy.CanImport(existing?.LicenseRevision, payload.LicenseRevision))
        {
            AppendAttestation(request, payload.LicenseRevision, LicenseRejectReasons.OlderRevision);
            return LicenseImportResult.Failure(LicenseRejectReasons.OlderRevision, "older-or-equal-revision");
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled(request, payload.LicenseRevision);
        }

        var lifecycleState = LicenseRuntimeModeService.EvaluateLifecycle(payload, request.ImportedAtUtc);
        stateStore.Save(new LicenseStateRecord(
            key,
            payload.PluginId,
            payload.CustomerId,
            payload.DeploymentId,
            payload.DeviceFingerprint,
            payload.PackageFingerprint,
            payload.LicenseRevision,
            lifecycleState,
            request.ImportedAtUtc,
            payload.NotBefore,
            payload.ExpiresAt,
            payload.GracePeriodDays,
            payload.Status));
        AppendAttestation(request, payload.LicenseRevision, "succeeded");

        return LicenseImportResult.Success(lifecycleState, payload.PluginId);
    }

    private LicenseImportResult Cancelled(LicenseImportRequest request, int? licenseRevision)
    {
        AppendAttestation(request, licenseRevision, LicenseRejectReasons.Cancelled);
        return LicenseImportResult.Failure(LicenseRejectReasons.Cancelled, "cancelled");
    }

    private void AppendAttestation(LicenseImportRequest request, int? licenseRevision, string result)
    {
        stateStore.AppendAttestation(new LicenseImportAttestation(
            SourceArtifactHash(request.ArtifactBytes),
            licenseRevision,
            request.ImportedAtUtc,
            result,
            request.CorrelationId));
    }

    private static string SourceArtifactHash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
