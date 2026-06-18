using Dobby.Audit;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Host.Plugins;
using Dobby.Licensing;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authorization;
using Dobby.Security.CurrentContext;
using Dobby.Security.Trust;
using Dobby.UnitTests.Licensing;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class LicenseImportCoordinatorTests
{
    [Fact]
    public void Import_file_persists_valid_license_for_installed_package()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var artifact = LicenseArtifactFixtureBuilder.CreateSignedArtifact(key);
        var path = WriteLicenseFile(artifact.Bytes);
        try
        {
            var licenseStateStore = new InMemoryLicenseStateStore();
            var audit = new InMemoryAuditTrail();
            var coordinator = CreateCoordinator(publicKeyPem, licenseStateStore, audit, isOwner: true);

            var result = coordinator.ImportFile("owner", path, CorrelationId.New());

            result.IsSuccess.ShouldBeTrue();
            result.LifecycleState.ShouldBe(LicenseLifecycleState.Active);
            licenseStateStore.State.ShouldNotBeNull();
            licenseStateStore.State.PluginId.ShouldBe("plugin.sample");
            audit.Snapshot().Select(evt => evt.EventName).ShouldBe([
                AuditEventName.LicenseImportAttempted,
                AuditEventName.LicenseImportSucceeded
            ]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_file_denies_without_license_import_permission()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var artifact = LicenseArtifactFixtureBuilder.CreateSignedArtifact(key);
        var path = WriteLicenseFile(artifact.Bytes);
        try
        {
            var licenseStateStore = new InMemoryLicenseStateStore();
            var audit = new InMemoryAuditTrail();
            var coordinator = CreateCoordinator(publicKeyPem, licenseStateStore, audit, isOwner: false);

            var result = coordinator.ImportFile("managed", path, CorrelationId.New());

            result.IsSuccess.ShouldBeFalse();
            result.ReasonCode.ShouldBe(LicenseRejectReasons.PermissionDenied);
            licenseStateStore.State.ShouldBeNull();
            audit.Snapshot().Select(evt => evt.EventName).ShouldBe([
                AuditEventName.LicenseImportAttempted,
                AuditEventName.LicenseImportRejected
            ]);
            audit.Snapshot().Last().Details!["reasonCode"].ShouldBe(LicenseRejectReasons.PermissionDenied);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_file_does_not_resolve_installed_package_before_signature_success()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var artifact = LicenseArtifactFixtureBuilder.CreateSignedArtifact(key);
        artifact.Bytes[^8] = artifact.Bytes[^8] == (byte)'X' ? (byte)'Y' : (byte)'X';
        var path = WriteLicenseFile(artifact.Bytes);
        try
        {
            var licenseStateStore = new InMemoryLicenseStateStore();
            var installedStore = new InMemoryInstalledPluginStateStore();
            var deviceFingerprintCalls = 0;
            var coordinator = CreateCoordinator(
                publicKeyPem,
                licenseStateStore,
                new InMemoryAuditTrail(),
                isOwner: true,
                installedStore,
                () =>
                {
                    deviceFingerprintCalls++;
                    return new string('a', 64);
                });

            var result = coordinator.ImportFile("owner", path, CorrelationId.New());

            result.IsSuccess.ShouldBeFalse();
            result.ReasonCode.ShouldBe(LicenseRejectReasons.InvalidSignature);
            deviceFingerprintCalls.ShouldBe(0);
            installedStore.SnapshotCalls.ShouldBe(0);
            licenseStateStore.State.ShouldBeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static LicenseImportCoordinator CreateCoordinator(
        byte[] publicKeyPem,
        InMemoryLicenseStateStore licenseStateStore,
        IAuditTrail audit,
        bool isOwner,
        InMemoryInstalledPluginStateStore? installedStore = null,
        Func<string>? currentDeviceFingerprintProvider = null)
    {
        var currentContextService = new CurrentCustomerDeploymentContextService(new CurrentContextStore());
        var importService = new LicenseImportService(
            new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider(
                DobbyPluginTrustProfile.Production,
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    [PluginTrustPublicKeyProvider.ProductionResourceName] = publicKeyPem
                })),
            currentContextService,
            licenseStateStore);
        var authorization = new OperationAuthorizationPolicy(
            OperationTargetRegistry.CreateDefault(),
            new StaticPermissionReader(isOwner));

        return new LicenseImportCoordinator(
            importService,
            installedStore ?? new InMemoryInstalledPluginStateStore(),
            authorization,
            currentDeviceFingerprintProvider ?? (() => new string('a', 64)),
            audit,
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-18T00:00:00Z")));
    }

    private static string WriteLicenseFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), "dobby-license-" + Guid.NewGuid().ToString("N") + ".dobbylicense");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private sealed class CurrentContextStore : ICurrentCustomerDeploymentContextStore
    {
        public IReadOnlyList<CurrentCustomerDeploymentContext> ReadActiveContexts()
        {
            return [new CurrentCustomerDeploymentContext("default", "default")];
        }

        public void SeedDefaultContext(CurrentCustomerDeploymentContext context)
        {
        }
    }

    private sealed class InMemoryLicenseStateStore : ILicenseStateStore
    {
        public LicenseStateRecord? State { get; private set; }

        public LicenseStateRecord? Read(RevisionSeriesKey key)
        {
            return State is not null && State.RevisionSeriesKey == key ? State : null;
        }

        public void Save(LicenseStateRecord state)
        {
            State = state;
        }

        public void AppendAttestation(LicenseImportAttestation attestation)
        {
        }
    }

    private sealed class InMemoryInstalledPluginStateStore : IInstalledPluginStateStore
    {
        private readonly InstalledPluginState state = new(
            "plugin.sample",
            "1.0.0",
            new string('b', 64),
            "Sample",
            "Sample plugin",
            IsEnabled: true,
            ValidationSummary: "verified",
            RuntimePath: "runtime",
            StatusSummary: "Enabled",
            InstalledAtUtc: DateTimeOffset.Parse("2026-06-18T00:00:00Z"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-06-18T00:00:00Z"));

        public Result Upsert(InstalledPluginState state) => Result.Success();

        public Result<InstalledPluginState?> Get(string pluginId)
        {
            return Result.Success<InstalledPluginState?>(
                state.PluginId.Equals(pluginId, StringComparison.Ordinal) ? state : null);
        }

        public Result Remove(string pluginId) => Result.Success();

        public Result SetEnabled(string pluginId, bool isEnabled, string statusSummary) => Result.Success();

        public int SnapshotCalls { get; private set; }

        public Result<IReadOnlyList<InstalledPluginState>> Snapshot()
        {
            SnapshotCalls++;
            return Result.Success<IReadOnlyList<InstalledPluginState>>([state]);
        }
    }

    private sealed class StaticPermissionReader(bool isOwner) : IPermissionGrantReader
    {
        public Result<UserPermissionState> LoadUserState(string userId)
        {
            return Result.Success(new UserPermissionState(userId, isOwner, IsActive: true));
        }

        public Result<bool> HasGrant(string userId, OperationKey key)
        {
            return Result.Success(false);
        }
    }
}
