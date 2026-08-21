using System.Security.Cryptography;
using System.Text;
using Dobby.Admin.Commands;
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
    public void Import_file_checks_permission_before_reading_selected_file()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            "dobby-license-missing-" + Guid.NewGuid().ToString("N") + ".dobbylicense");
        var licenseStateStore = new InMemoryLicenseStateStore();
        var audit = new InMemoryAuditTrail();
        var coordinator = CreateCoordinator(publicKeyPem, licenseStateStore, audit, isOwner: false);

        var result = coordinator.ImportFile("managed", missingPath, CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe(LicenseRejectReasons.PermissionDenied);
        licenseStateStore.State.ShouldBeNull();
        audit.Snapshot().Select(evt => evt.EventName).ShouldBe([
            AuditEventName.LicenseImportAttempted,
            AuditEventName.LicenseImportRejected
        ]);
        audit.Snapshot().Last().Details!["reasonCode"].ShouldBe(LicenseRejectReasons.PermissionDenied);
    }

    [Fact]
    public async Task Import_file_async_rejects_when_cancelled_before_file_read()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var artifact = LicenseArtifactFixtureBuilder.CreateSignedArtifact(key);
        var path = WriteLicenseFile(artifact.Bytes);
        try
        {
            var licenseStateStore = new InMemoryLicenseStateStore();
            var coordinator = CreateCoordinator(publicKeyPem, licenseStateStore, new InMemoryAuditTrail(), isOwner: true);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var result = await coordinator.ImportFileAsync("owner", path, CorrelationId.New(), cancellation.Token);

            result.IsSuccess.ShouldBeFalse();
            result.ReasonCode.ShouldBe(LicenseRejectReasons.Cancelled);
            licenseStateStore.State.ShouldBeNull();
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
        LicenseArtifactFixtureBuilder.CorruptSignature(artifact);
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

    [Fact]
    public void Import_file_accepts_admin_generated_license_with_package_fingerprint_binding()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var root = Path.Combine(Path.GetTempPath(), "dobby-admin-license-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var packagePath = Path.Combine(root, "plugin.sample.dobbyplugin");
            var bindingPath = Path.Combine(root, "device.json");
            var licensePath = Path.Combine(root, "sample.dobbylicense");
            File.WriteAllText(packagePath, "package-bytes", new UTF8Encoding(false));
            File.WriteAllText(bindingPath, """{"deviceFingerprint":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""", new UTF8Encoding(false));

            var adminResult = AdminCommandHost.Run([
                "--cli",
                "create-plugin-license",
                "--plugin-package-path",
                packagePath,
                "--signing-profile",
                "production",
                "--customer-id",
                "default",
                "--deployment-id",
                "default",
                "--device-binding-fixture-path",
                bindingPath,
                "--valid-from",
                "2026-06-18T00:00:00Z",
                "--valid-until",
                "2026-07-18T00:00:00Z",
                "--revision",
                "8",
                "--output-path",
                licensePath
            ], new InMemoryAdminSigningMaterialProvider("production", key));
            adminResult.ExitCode.ShouldBe(0, adminResult.StandardError);
            var packageFingerprint = Sha256File(packagePath);
            var installedStore = new InMemoryInstalledPluginStateStore(new InstalledPluginState(
                "plugin.sample",
                "1.0.0",
                packageFingerprint,
                "Sample",
                "Sample plugin",
                IsEnabled: true,
                ValidationSummary: "verified",
                RuntimePath: "runtime",
                StatusSummary: "Enabled",
                InstalledAtUtc: DateTimeOffset.Parse("2026-06-18T00:00:00Z"),
                UpdatedAtUtc: DateTimeOffset.Parse("2026-06-18T00:00:00Z")));
            var licenseStateStore = new InMemoryLicenseStateStore();
            var coordinator = CreateCoordinator(
                publicKeyPem,
                licenseStateStore,
                new InMemoryAuditTrail(),
                isOwner: true,
                installedStore);

            var result = coordinator.ImportFile("owner", licensePath, CorrelationId.New());

            result.IsSuccess.ShouldBeTrue();
            result.PluginId.ShouldBe("plugin.sample");
            licenseStateStore.State.ShouldNotBeNull();
            licenseStateStore.State.PackageFingerprint.ShouldBe(packageFingerprint);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
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
                    [PluginTrustPublicKeyProvider.ProductionLicenseResourceName] = publicKeyPem
                },
                ArtifactSignaturePurposes.PluginLicense)),
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

        public void ProvisionContext(CurrentCustomerDeploymentContext context)
        {
            throw new InvalidOperationException("License import must not provision current context.");
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

    private sealed class InMemoryAdminSigningMaterialProvider(string profile, ECDsa key) : IAdminSigningMaterialProvider
    {
        public AdminSigningMaterialResult Resolve(string signingProfile, string purpose)
        {
            return signingProfile.Equals(profile, StringComparison.Ordinal)
                ? AdminSigningMaterialResult.Success(new AdminSigningMaterial(
                    signingProfile,
                    purpose,
                    profile.Equals("production", StringComparison.Ordinal)
                        ? PluginTrustPublicKeyProvider.ProductionPluginLicenseKeyId
                        : PluginTrustPublicKeyProvider.DevQaPluginLicenseKeyId,
                    key))
                : AdminSigningMaterialResult.Unavailable("signing-material-unavailable");
        }
    }

    private static string Sha256File(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private sealed class InMemoryInstalledPluginStateStore : IInstalledPluginStateStore
    {
        private readonly InstalledPluginState state;

        public InMemoryInstalledPluginStateStore()
            : this(new InstalledPluginState(
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
            UpdatedAtUtc: DateTimeOffset.Parse("2026-06-18T00:00:00Z")))
        {
        }

        public InMemoryInstalledPluginStateStore(InstalledPluginState state)
        {
            this.state = state;
        }

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
