using Dobby.Licensing;
using Dobby.Security.CurrentContext;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicenseImportServiceTests
{
    [Fact]
    public void Import_success_persists_state_and_limited_attestation()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var store = new InMemoryLicenseStateStore();
        var service = CreateService(publicKeyPem, store);
        var artifact = LicenseArtifactFixtureBuilder.CreateSignedArtifact(key);

        var result = service.Import(Request(artifact.Bytes));

        result.IsSuccess.ShouldBeTrue();
        result.ReasonCode.ShouldBeEmpty();
        result.PluginId.ShouldBe("plugin.sample");
        store.SavedStates.ShouldHaveSingleItem();
        store.SavedStates[0].RevisionSeriesKey.ShouldBe(new RevisionSeriesKey("plugin.sample", "default", "default"));
        store.SavedStates[0].LicenseRevision.ShouldBe(7);
        store.SavedStates[0].DeviceFingerprint.ShouldBe(new string('a', 64));
        store.SavedStates[0].PackageFingerprint.ShouldBe(new string('b', 64));
        store.Attestations.ShouldHaveSingleItem();
        store.Attestations[0].SourceArtifactHash.ShouldNotBeEmpty();
        store.Attestations[0].LicenseRevision.ShouldBe(7);
        store.Attestations[0].Result.ShouldBe("succeeded");
        store.Attestations[0].CorrelationId.ShouldBe("corr-1");
    }

    [Fact]
    public void Import_denies_without_permission_and_does_not_mutate_license_state()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var store = new InMemoryLicenseStateStore();
        var service = CreateService(publicKeyPem, store);
        var artifact = LicenseArtifactFixtureBuilder.CreateSignedArtifact(key);

        var result = service.Import(Request(artifact.Bytes, permissionGranted: false));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("permission-denied");
        store.SavedStates.ShouldBeEmpty();
        store.Attestations.ShouldHaveSingleItem();
        store.Attestations[0].Result.ShouldBe("permission-denied");
    }

    [Fact]
    public void Import_stops_at_signature_failure_before_payload_semantics()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var store = new InMemoryLicenseStateStore();
        var service = CreateService(publicKeyPem, store);
        var artifact = LicenseArtifactFixtureBuilder.CreateSignedArtifact(
            key,
            payload => payload["status"] = "grace");
        artifact.Bytes[^8] = (byte)'X';

        var result = service.Import(Request(artifact.Bytes));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-signature");
        store.SavedStates.ShouldBeEmpty();
        store.Attestations.ShouldHaveSingleItem();
        store.Attestations[0].Result.ShouldBe("invalid-signature");
    }

    [Fact]
    public void Import_rejects_customer_deployment_mismatch_without_state_mutation()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var store = new InMemoryLicenseStateStore();
        var service = CreateService(publicKeyPem, store);
        var artifact = LicenseArtifactFixtureBuilder.CreateSignedArtifact(
            key,
            payload => payload["customerId"] = "customer-x");

        var result = service.Import(Request(artifact.Bytes));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("binding-mismatch");
        store.SavedStates.ShouldBeEmpty();
    }

    [Fact]
    public void Import_rejects_device_mismatch_without_disclosing_detail()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var store = new InMemoryLicenseStateStore();
        var service = CreateService(publicKeyPem, store);
        var artifact = LicenseArtifactFixtureBuilder.CreateSignedArtifact(key);

        var result = service.Import(Request(artifact.Bytes, deviceFingerprint: new string('c', 64)));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("binding-mismatch");
        result.SupportCode.ShouldBe("device-binding-mismatch");
        store.SavedStates.ShouldBeEmpty();
    }

    [Fact]
    public void Import_rejects_package_mismatch_without_state_mutation()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var store = new InMemoryLicenseStateStore();
        var service = CreateService(publicKeyPem, store);
        var artifact = LicenseArtifactFixtureBuilder.CreateSignedArtifact(key);

        var result = service.Import(Request(artifact.Bytes, packageFingerprint: new string('c', 64)));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("package-mismatch");
        store.SavedStates.ShouldBeEmpty();
    }

    [Fact]
    public void Import_rejects_older_or_equal_revision_for_revision_series()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var store = new InMemoryLicenseStateStore();
        store.ExistingState = new LicenseStateRecord(
            new RevisionSeriesKey("plugin.sample", "default", "default"),
            "plugin.sample",
            "default",
            "default",
            new string('a', 64),
            new string('b', 64),
            7,
            LicenseLifecycleState.Active,
            new DateTimeOffset(2026, 06, 17, 0, 0, 0, TimeSpan.Zero));
        var service = CreateService(publicKeyPem, store);
        var artifact = LicenseArtifactFixtureBuilder.CreateSignedArtifact(key);

        var result = service.Import(Request(artifact.Bytes));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("older-revision");
        store.SavedStates.ShouldBeEmpty();
    }

    private static LicenseImportService CreateService(byte[] publicKeyPem, InMemoryLicenseStateStore store)
    {
        var verifier = new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionResourceName] = publicKeyPem
            }));
        var contextStore = new InMemoryCurrentContextStore(new CurrentCustomerDeploymentContext("default", "default"));
        return new LicenseImportService(
            verifier,
            new CurrentCustomerDeploymentContextService(contextStore),
            store);
    }

    private static LicenseImportRequest Request(
        byte[] artifactBytes,
        bool permissionGranted = true,
        string? deviceFingerprint = null,
        string? packageFingerprint = null)
    {
        return new LicenseImportRequest(
            "license.dobbylicense",
            artifactBytes,
            () => deviceFingerprint ?? new string('a', 64),
            _ => packageFingerprint ?? new string('b', 64),
            permissionGranted,
            "corr-1",
            new DateTimeOffset(2026, 06, 18, 0, 0, 0, TimeSpan.Zero));
    }

    private sealed class InMemoryLicenseStateStore : ILicenseStateStore
    {
        public LicenseStateRecord? ExistingState { get; set; }

        public List<LicenseStateRecord> SavedStates { get; } = [];

        public List<LicenseImportAttestation> Attestations { get; } = [];

        public LicenseStateRecord? Read(RevisionSeriesKey key)
        {
            return ExistingState?.RevisionSeriesKey == key ? ExistingState : null;
        }

        public void Save(LicenseStateRecord state)
        {
            SavedStates.Add(state);
        }

        public void AppendAttestation(LicenseImportAttestation attestation)
        {
            Attestations.Add(attestation);
        }
    }

    private sealed class InMemoryCurrentContextStore : ICurrentCustomerDeploymentContextStore
    {
        private readonly IReadOnlyList<CurrentCustomerDeploymentContext> contexts;

        public InMemoryCurrentContextStore(params CurrentCustomerDeploymentContext[] contexts)
        {
            this.contexts = contexts;
        }

        public IReadOnlyList<CurrentCustomerDeploymentContext> ReadActiveContexts()
        {
            return contexts;
        }

        public void SeedDefaultContext(CurrentCustomerDeploymentContext context)
        {
            throw new InvalidOperationException("Import path must not seed current context.");
        }
    }
}
