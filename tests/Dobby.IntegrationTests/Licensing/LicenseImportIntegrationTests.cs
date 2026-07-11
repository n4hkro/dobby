using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.Licensing;
using Dobby.Security.CurrentContext;
using Dobby.Security.Trust;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Licensing;

public sealed class LicenseImportIntegrationTests
{
    [Fact]
    public void Valid_signed_license_import_persists_state_and_limited_attestation()
    {
        using var key = CreateKeyPair(out var publicKeyPem);
        var stateStore = new RecordingLicenseStateStore();
        var contextStore = new RecordingCurrentContextStore(new CurrentCustomerDeploymentContext("default", "default"));
        var service = CreateService(publicKeyPem, stateStore, contextStore);
        var artifact = CreateSignedArtifact(key);

        var result = service.Import(Request(artifact));

        result.IsSuccess.ShouldBeTrue();
        result.PluginId.ShouldBe("plugin.sample");
        stateStore.SavedStates.ShouldHaveSingleItem();
        var state = stateStore.SavedStates.Single();
        state.RevisionSeriesKey.ShouldBe(new RevisionSeriesKey("plugin.sample", "default", "default"));
        state.DeviceFingerprint.ShouldBe(new string('a', 64));
        state.PackageFingerprint.ShouldBe(new string('b', 64));
        state.SourceStatus.ShouldBe(LicenseArtifactStatus.Active);
        state.GracePeriodDays.ShouldBe(14);
        stateStore.Attestations.ShouldHaveSingleItem();
        stateStore.Attestations.Single().Result.ShouldBe("succeeded");
        stateStore.Attestations.Single().CorrelationId.ShouldBe("corr-integration");
        contextStore.SeedDefaultCalls.ShouldBe(0);
        contextStore.ProvisionCalls.ShouldBe(0);
    }

    [Fact]
    public void Customer_deployment_mismatch_fails_closed_without_state_mutation()
    {
        using var key = CreateKeyPair(out var publicKeyPem);
        var stateStore = new RecordingLicenseStateStore();
        var contextStore = new RecordingCurrentContextStore(new CurrentCustomerDeploymentContext("default", "default"));
        var service = CreateService(publicKeyPem, stateStore, contextStore);
        var artifact = CreateSignedArtifact(key, payload => payload["deploymentId"] = "other-deployment");

        var result = service.Import(Request(artifact));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe(LicenseRejectReasons.BindingMismatch);
        result.SupportCode.ShouldBe("context-binding-mismatch");
        stateStore.SavedStates.ShouldBeEmpty();
        stateStore.Attestations.ShouldHaveSingleItem();
        stateStore.Attestations.Single().Result.ShouldBe(LicenseRejectReasons.BindingMismatch);
        contextStore.SeedDefaultCalls.ShouldBe(0);
        contextStore.ProvisionCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, "missing-current-context")]
    [InlineData(2, "duplicate-current-context")]
    public void Missing_or_duplicate_context_fails_closed_before_license_state_mutation(
        int contextCount,
        string expectedSupportCode)
    {
        using var key = CreateKeyPair(out var publicKeyPem);
        var stateStore = new RecordingLicenseStateStore();
        var contexts = Enumerable.Range(0, contextCount)
            .Select(_ => new CurrentCustomerDeploymentContext("default", "default"))
            .ToArray();
        var contextStore = new RecordingCurrentContextStore(contexts);
        var service = CreateService(publicKeyPem, stateStore, contextStore);
        var artifact = CreateSignedArtifact(key);

        var result = service.Import(Request(artifact));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe(LicenseRejectReasons.BindingMismatch);
        result.SupportCode.ShouldBe(expectedSupportCode);
        stateStore.SavedStates.ShouldBeEmpty();
        contextStore.SeedDefaultCalls.ShouldBe(0);
        contextStore.ProvisionCalls.ShouldBe(0);
    }

    [Fact]
    public void License_import_and_runtime_mode_consume_the_same_security_owned_context()
    {
        using var key = CreateKeyPair(out var publicKeyPem);
        var stateStore = new RecordingLicenseStateStore();
        var contextStore = new RecordingCurrentContextStore(
            new CurrentCustomerDeploymentContext("customer-a", "deployment-a"));
        var contextService = new CurrentCustomerDeploymentContextService(contextStore);
        var importService = CreateService(publicKeyPem, stateStore, contextService);
        var artifact = CreateSignedArtifact(key, payload =>
        {
            payload["customerId"] = "customer-a";
            payload["deploymentId"] = "deployment-a";
        });

        var import = importService.Import(Request(artifact));
        var runtimeProvider = new LicenseRuntimeModeProvider(
            stateStore,
            contextService,
            () => new string('a', 64),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-18T00:00:00Z")));
        var mode = runtimeProvider.GetCurrentMode("plugin.sample", new string('b', 64));

        import.IsSuccess.ShouldBeTrue();
        mode.IsSuccess.ShouldBeTrue();
        mode.Value.ShouldBe(Dobby.Contracts.Licensing.PluginRuntimeMode.FullAccess);
        contextStore.SeedDefaultCalls.ShouldBe(0);
        contextStore.ProvisionCalls.ShouldBe(0);
    }

    private static LicenseImportService CreateService(
        byte[] publicKeyPem,
        ILicenseStateStore stateStore,
        ICurrentCustomerDeploymentContextStore contextStore)
    {
        return CreateService(
            publicKeyPem,
            stateStore,
            new CurrentCustomerDeploymentContextService(contextStore));
    }

    private static LicenseImportService CreateService(
        byte[] publicKeyPem,
        ILicenseStateStore stateStore,
        CurrentCustomerDeploymentContextService contextService)
    {
        var verifier = new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [PluginTrustPublicKeyProvider.ProductionResourceName] = publicKeyPem
            }));

        return new LicenseImportService(
            verifier,
            contextService,
            stateStore);
    }

    private static LicenseImportRequest Request(byte[] artifact)
    {
        return new LicenseImportRequest(
            "license.dobbylicense",
            artifact,
            () => new string('a', 64),
            _ => new string('b', 64),
            PermissionGranted: true,
            "corr-integration",
            DateTimeOffset.Parse("2026-06-18T00:00:00Z"));
    }

    private static ECDsa CreateKeyPair(out byte[] publicKeyPem)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        publicKeyPem = EncodePem(key.ExportSubjectPublicKeyInfo());
        return key;
    }

    private static byte[] CreateSignedArtifact(
        ECDsa key,
        Action<Dictionary<string, object?>>? mutate = null)
    {
        var payload = ValidPayload();
        mutate?.Invoke(payload);
        using var payloadDocument = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var canonicalPayload = LicenseCanonicalJsonWriter.Write(payloadDocument.RootElement);
        var signature = key.SignData(
            canonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        var artifactJson = "{\"payload\":" + Encoding.UTF8.GetString(canonicalPayload) +
            ",\"signature\":{\"algorithm\":\"ECDSA-P256-SHA256\",\"keyVersion\":1,\"value\":\"" +
            ToBase64Url(signature) +
            "\"}}";

        return Encoding.UTF8.GetBytes(artifactJson);
    }

    private static Dictionary<string, object?> ValidPayload()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 2,
            ["licenseId"] = "license-1",
            ["pluginId"] = "plugin.sample",
            ["customerId"] = "default",
            ["deploymentId"] = "default",
            ["deviceFingerprintAlgorithm"] = "v1",
            ["deviceFingerprint"] = new string('a', 64),
            ["packageFingerprint"] = new string('b', 64),
            ["licenseRevision"] = 7,
            ["issuedAt"] = "2026-06-18T00:00:00Z",
            ["notBefore"] = "2026-06-18T00:00:00Z",
            ["expiresAt"] = "2026-07-18T00:00:00Z",
            ["gracePeriodDays"] = 14,
            ["status"] = "active",
            ["keyVersion"] = 1
        };
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] EncodePem(byte[] der)
    {
        var base64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        return Encoding.UTF8.GetBytes($"-----BEGIN PUBLIC KEY-----\n{base64}\n-----END PUBLIC KEY-----\n");
    }

    private sealed class RecordingLicenseStateStore : ILicenseStateStore
    {
        public List<LicenseStateRecord> SavedStates { get; } = [];

        public List<LicenseImportAttestation> Attestations { get; } = [];

        public LicenseStateRecord? Read(RevisionSeriesKey key)
        {
            return SavedStates.LastOrDefault(state => state.RevisionSeriesKey == key);
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

    private sealed class RecordingCurrentContextStore(params CurrentCustomerDeploymentContext[] contexts)
        : ICurrentCustomerDeploymentContextStore
    {
        public int SeedDefaultCalls { get; private set; }

        public int ProvisionCalls { get; private set; }

        public IReadOnlyList<CurrentCustomerDeploymentContext> ReadActiveContexts()
        {
            return contexts;
        }

        public void SeedDefaultContext(CurrentCustomerDeploymentContext context)
        {
            SeedDefaultCalls++;
        }

        public void ProvisionContext(CurrentCustomerDeploymentContext context)
        {
            ProvisionCalls++;
        }
    }
}
