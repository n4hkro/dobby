using System.Security.Cryptography;
using System.Collections.Concurrent;
using Dobby.Security.CurrentContext;

namespace Dobby.Licensing;

public sealed class LicenseImportService
{
    private static readonly ConcurrentDictionary<RevisionSeriesKey, object> CommitGates = new();
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
            return LicenseImportResult.Failure(LicenseRejectReasons.PermissionDenied, "permission-denied");
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        var parse = LicenseArtifactParser.Parse(request.FileName, request.ArtifactBytes);
        if (!parse.IsSuccess)
        {
            return LicenseImportResult.Failure(parse.ReasonCode, parse.ReasonCode);
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        var signature = signatureVerifier.Verify(parse.Artifact!.Signature, parse.Artifact.CanonicalPayload);
        if (!signature.IsSuccess)
        {
            return LicenseImportResult.Failure(signature.ReasonCode, signature.SupportCode);
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        var payloadValidation = LicensePayloadValidator.Validate(parse.Artifact);
        if (!payloadValidation.IsSuccess)
        {
            return LicenseImportResult.Failure(payloadValidation.ReasonCode, payloadValidation.ReasonCode);
        }

        var payload = payloadValidation.Payload!;
        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        var context = currentContextService.ValidateMatches(payload.CustomerId, payload.DeploymentId);
        if (!context.IsSuccess)
        {
            return LicenseImportResult.Failure(context.ReasonCode, context.SupportCode);
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        // A non-derivable current digest and a mismatching one are one typed reject with
        // one reason code: `ADR-0063` § 4 requires the mismatch outcome to be
        // non-disclosing, so this surface must not let a caller distinguish "wrong device"
        // from "this device's hardware signal is unreadable". Both leave last-known-good
        // state untouched, because this returns before any state write below.
        var currentDeviceFingerprint = request.CurrentDeviceFingerprintProvider();
        if (!currentDeviceFingerprint.IsSuccess ||
            !LicenseDeviceFingerprintV1.Matches(currentDeviceFingerprint.Value, payload.DeviceKeyHash))
        {
            return LicenseImportResult.Failure(LicenseRejectReasons.BindingMismatch, "device-binding-mismatch");
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        var installedPackageFingerprint = request.InstalledPackageFingerprintResolver(payload.PluginId);
        if (!payload.PackageFingerprint.Equals(installedPackageFingerprint, StringComparison.Ordinal))
        {
            return LicenseImportResult.Failure(LicenseRejectReasons.PackageMismatch, "package-binding-mismatch");
        }

        if (request.CancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        var key = new RevisionSeriesKey(payload.PluginId, payload.CustomerId, payload.DeploymentId);
        var lifecycleState = LicenseRuntimeModeService.EvaluateLifecycle(payload, request.ImportedAtUtc);
        var state = new LicenseStateRecord(
            key,
            payload.PluginId,
            payload.CustomerId,
            payload.DeploymentId,
            payload.DeviceKeyHash,
            payload.PackageFingerprint,
            payload.LicenseRevision,
            lifecycleState,
            request.ImportedAtUtc,
            payload.NotBefore,
            payload.ExpiresAt,
            payload.GracePeriodDays,
            SourceStatus: null,
            LicenseId: payload.LicenseId,
            RevisionSeriesId: payload.RevisionSeriesId,
            KeyId: payload.KeyId,
            IssuedAtUtc: payload.IssuedAt,
            SourceIntent: payload.Intent);
        var attestation = new LicenseImportAttestation(
            SourceArtifactHash(request.ArtifactBytes),
            payload.LicenseRevision,
            request.ImportedAtUtc,
            "succeeded",
            request.CorrelationId);
        LicenseImportCommitResult commit;
        lock (CommitGates.GetOrAdd(key, static _ => new object()))
        {
            if (request.CancellationToken.IsCancellationRequested)
            {
                return Cancelled();
            }

            commit = stateStore.CommitSuccessfulImport(
                state,
                attestation,
                new LicenseImportCommitMetadata(request.ActorId, request.CorrelationId));
        }

        return commit.Outcome switch
        {
            LicenseImportCommitOutcome.Committed => LicenseImportResult.Success(
                lifecycleState,
                payload.PluginId,
                commit.SuccessAuditAlreadyPresent),
            LicenseImportCommitOutcome.AlreadyCurrent => LicenseImportResult.Success(
                lifecycleState,
                payload.PluginId,
                auditCommitted: true),
            _ => LicenseImportResult.Failure(LicenseRejectReasons.OlderRevision, "older-or-conflicting-revision")
        };
    }

    private static LicenseImportResult Cancelled()
    {
        return LicenseImportResult.Failure(LicenseRejectReasons.Cancelled, "cancelled");
    }

    private static string SourceArtifactHash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
