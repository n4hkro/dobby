using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.Admin.Commands;
using Dobby.Core;
using Dobby.Licensing;
using Dobby.Security.Authentication;
using Dobby.Security.Trust;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Admin;

public sealed class AdminCommandHostTests
{
    [Theory]
    [MemberData(nameof(InvalidOptionShapes))]
    public void Run_rejects_unknown_foreign_repeated_and_missing_option_shapes(string[] args)
    {
        var result = AdminCommandHost.Run(args);

        result.ExitCode.ShouldBe(64);
        result.StandardError.ShouldContain("option contract is not approved");
        result.StandardError.ShouldNotContain(" at ");
    }

    [Fact]
    public void Run_maps_profile_mismatch_invalid_timestamps_revision_and_output_conflicts_to_validation_failure()
    {
        using var fixture = TempFixture.Create();
        var output = fixture.Path("artifact.dobbyplugin");
        File.WriteAllText(output, "already exists");

        AdminCommandHost.Run([
            "--cli", "sign-plugin-package",
            "--input-path", fixture.Root,
            "--signing-profile", "dev-qa",
            "--trust-profile", "production",
            "--output-path", fixture.Path("out.dobbyplugin")
        ]).StandardError.ShouldContain("profile-mismatch");

        AdminCommandHost.Run([
            "--cli", "create-plugin-license",
            "--plugin-package-path", fixture.Path("package.dobbyplugin"),
            "--signing-profile", "dev-qa",
            "--customer-id", "customer",
            "--deployment-id", "deployment",
            "--device-binding-fixture-path", fixture.Path("device.json"),
            "--valid-from", "2026-06-18T00:00:00+00:00",
            "--valid-until", "2026-07-18T00:00:00Z",
            "--revision", "1",
            "--output-path", fixture.Path("license.dobbylicense")
        ]).StandardError.ShouldContain("invalid-timestamp");

        AdminCommandHost.Run([
            "--cli", "create-plugin-license",
            "--plugin-package-path", fixture.Path("package.dobbyplugin"),
            "--signing-profile", "dev-qa",
            "--customer-id", "customer",
            "--deployment-id", "deployment",
            "--device-binding-fixture-path", fixture.Path("device.json"),
            "--valid-from", "2026-06-18T00:00:00Z",
            "--valid-until", "2026-07-18T00:00:00Z",
            "--revision", "0",
            "--output-path", fixture.Path("license.dobbylicense")
        ]).StandardError.ShouldContain("invalid-revision");

        var conflict = AdminCommandHost.Run([
            "--cli", "sign-plugin-package",
            "--input-path", fixture.Root,
            "--signing-profile", "dev-qa",
            "--output-path", output
        ]);

        conflict.ExitCode.ShouldBe(65);
        conflict.StandardError.ShouldContain("output-conflict");
        conflict.StandardError.ShouldNotContain(fixture.Root);
    }

    [Fact]
    public void Run_returns_unavailable_signing_material_for_known_profile_without_default_material()
    {
        using var fixture = TempFixture.Create();
        File.WriteAllText(fixture.Path("request.dobbyrecoveryrequest"), RecoveryRequestJson());

        var result = AdminCommandHost.Run([
            "--cli", "create-recovery-response",
            "--request-path", fixture.Path("request.dobbyrecoveryrequest"),
            "--signing-profile", "dev-qa",
            "--output-path", fixture.Path("response.dobbyrecovery")
        ]);

        result.ExitCode.ShouldBe(66);
        result.StandardError.ShouldContain("signing-material-unavailable");
        result.StandardError.ShouldNotContain(fixture.Root);
    }

    [Fact]
    public void Create_recovery_response_writes_host_compatible_envelope_through_concrete_verifier()
    {
        using var fixture = TempFixture.Create();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyPem = EncodePem(key.ExportSubjectPublicKeyInfo());
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-20T10:00:00Z"));
        var requestPath = fixture.Path("request.dobbyrecoveryrequest");
        var outputPath = fixture.Path("response.dobbyrecovery");
        var reportPath = fixture.Path("response.report.json");
        File.WriteAllText(requestPath, RecoveryRequestJson());

        var result = AdminCommandHost.Run([
            "--cli", "create-recovery-response",
            "--request-path", requestPath,
            "--signing-profile", "dev-qa",
            "--output-path", outputPath,
            "--report-path", reportPath
        ], new InMemorySigningMaterialProvider("dev-qa", key), time);

        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldContain("create-recovery-response");
        result.StandardOutput.ShouldNotContain(fixture.Root);
        File.Exists(outputPath).ShouldBeTrue();
        File.Exists(reportPath).ShouldBeTrue();

        var responseJson = File.ReadAllText(outputPath);
        responseJson.ShouldNotContain("targetUserId");
        File.ReadAllText(reportPath).ShouldNotContain("machine", Case.Insensitive);
        var envelope = RecoveryResponseImporter.ReadEnvelope(responseJson);
        envelope.Signature.ShouldContain("=");
        envelope.Signature.ShouldNotContain("-");
        envelope.Signature.ShouldNotContain("_");

        var publicKeyProvider = new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.DevQa,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [PluginTrustPublicKeyProvider.DevQaResourceName] = publicKeyPem
            });
        var importer = new RecoveryResponseImporter(
            new VendorRecoveryPublicKey(publicKeyProvider),
            new StaticDeviceFingerprint(new DeviceFingerprint("machine", "cpu", "board", "disk", "mac")));

        var verify = importer.Verify(envelope, time.GetUtcNow());

        verify.IsSuccess.ShouldBeTrue(verify.IsSuccess ? null : verify.ErrorCode.ToString());
    }

    [Fact]
    public void Create_plugin_license_writes_importable_license_with_injected_material()
    {
        using var fixture = TempFixture.Create();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyPem = EncodePem(key.ExportSubjectPublicKeyInfo());
        var packagePath = fixture.Path("package.dobbyplugin");
        var bindingPath = fixture.Path("device.json");
        var outputPath = fixture.Path("sample.dobbylicense");
        File.WriteAllText(packagePath, "package-bytes");
        File.WriteAllText(bindingPath, """{"deviceFingerprint":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""");

        var result = AdminCommandHost.Run([
            "--cli", "create-plugin-license",
            "--plugin-package-path", packagePath,
            "--signing-profile", "dev-qa",
            "--customer-id", "customer",
            "--deployment-id", "deployment",
            "--device-binding-fixture-path", bindingPath,
            "--valid-from", "2026-06-20T00:00:00Z",
            "--valid-until", "2026-07-20T00:00:00Z",
            "--revision", "3",
            "--output-path", outputPath
        ], new InMemorySigningMaterialProvider("dev-qa", key));

        result.ExitCode.ShouldBe(0, result.StandardError);
        var report = File.ReadAllText(outputPath + ".report.json");
        report.ShouldNotContain("\"customer\"");
        report.ShouldNotContain("\"deployment\"");
        report.ShouldContain("customerIdHash");
        report.ShouldContain("deploymentIdHash");
        var parsed = LicenseArtifactParser.Parse(Path.GetFileName(outputPath), File.ReadAllBytes(outputPath));
        parsed.IsSuccess.ShouldBeTrue(parsed.ReasonCode);
        LicensePayloadValidator.Validate(parsed.Artifact!).IsSuccess.ShouldBeTrue();
        new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider(
                DobbyPluginTrustProfile.DevQa,
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    [PluginTrustPublicKeyProvider.DevQaResourceName] = publicKeyPem
                }))
            .Verify(parsed.Artifact!.Signature, parsed.Artifact.CanonicalPayload)
            .IsSuccess.ShouldBeTrue();
    }

    public static IEnumerable<object[]> InvalidOptionShapes()
    {
        yield return [new[] { "--cli", "create-recovery-response", "--unapproved-option" }];
        yield return [new[] { "--cli", "create-recovery-response", "--request-path" }];
        yield return [new[] { "--cli", "create-recovery-response", "--request-path", "a", "--request-path", "b", "--signing-profile", "dev-qa", "--output-path", "out" }];
        yield return [new[] { "--cli", "create-recovery-response", "--plugin-package-path", "pkg", "--signing-profile", "dev-qa", "--output-path", "out" }];
        yield return [new[] { "--cli", "create-recovery-response", "--request-path", "a", "--signing-profile", "dev-qa", "--output-path", "out", "--overwrite", "true" }];
    }

    private static string RecoveryRequestJson()
    {
        return """
            {
              "type": "dobby.owner-recovery-request.v1",
              "dobbyVersion": "1.0.0",
              "requestedAt": "2026-06-20T09:00:00Z",
              "deviceIdentity": {
                "machineGuid": "machine",
                "cpuId": "cpu",
                "motherboardSerial": "board",
                "diskSerial": "disk",
                "macAddress": "mac"
              },
              "deviceDisplayName": "Owner laptop",
              "requestId": "request-1"
            }
            """;
    }

    private static byte[] EncodePem(byte[] der)
    {
        var base64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        return Encoding.UTF8.GetBytes($"-----BEGIN PUBLIC KEY-----\n{base64}\n-----END PUBLIC KEY-----\n");
    }

    private sealed class InMemorySigningMaterialProvider(string profile, ECDsa key) : IAdminSigningMaterialProvider
    {
        public AdminSigningMaterialResult Resolve(string signingProfile)
        {
            return signingProfile.Equals(profile, StringComparison.Ordinal)
                ? AdminSigningMaterialResult.Success(new AdminSigningMaterial(signingProfile, key))
                : AdminSigningMaterialResult.Unavailable("signing-material-unavailable");
        }
    }

    private sealed class StaticDeviceFingerprint(DeviceFingerprint fingerprint) : IDeviceFingerprintSource
    {
        public Result<DeviceFingerprint> Current() => Result.Success(fingerprint);
    }

    private sealed class TempFixture : IDisposable
    {
        private TempFixture(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TempFixture Create()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dobby-admin-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempFixture(root);
        }

        public string Path(string fileName)
        {
            return System.IO.Path.Combine(Root, fileName);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
