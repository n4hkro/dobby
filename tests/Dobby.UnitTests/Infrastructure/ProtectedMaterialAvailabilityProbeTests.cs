using System.IO;
using System.Security.Cryptography;
using System.Text;
using Dobby.Infrastructure.ProtectedStorage;
using Shouldly;

namespace Dobby.UnitTests.Infrastructure;

public sealed class ProtectedMaterialAvailabilityProbeTests
{
    [Fact]
    public void Clean_first_run_creates_required_protected_material_and_metadata_only_marker()
    {
        using var temp = new TempRoot();
        var probe = CreateProbe(temp.Path);

        probe.IsAvailable().ShouldBeTrue();
        var material = probe.LoadRequired();
        var secondLoad = probe.LoadRequired();

        material.ApplicationEntropy.ShouldNotBeEmpty();
        material.ApplicationEntropy.Length.ShouldBe(32);
        material.StableDeviceId.ShouldNotBeEmpty();
        material.DbOpenSecret.ShouldNotBeEmpty();
        material.ProtectedStableDeviceIdBlob.ShouldNotBeEmpty();
        material.StableDeviceId.ShouldBe(secondLoad.StableDeviceId);
        material.DbOpenSecret.ShouldBe(secondLoad.DbOpenSecret);

        var protectedDirectory = Path.Combine(temp.Path, "data", "protected");
        File.Exists(Path.Combine(protectedDirectory, "app-entropy.v1.dpapi")).ShouldBeTrue();
        File.Exists(Path.Combine(protectedDirectory, "stable-device-id.v1.dpapi")).ShouldBeTrue();
        File.Exists(Path.Combine(protectedDirectory, "db-open-secret.v1.dpapi")).ShouldBeTrue();

        var marker = File.ReadAllText(Path.Combine(protectedDirectory, "protected-material.initialized.json"));
        marker.ShouldContain("protectionVersion");
        marker.ShouldContain("algorithmVersion");
        marker.ShouldContain("createdAtUtc");
        marker.ShouldNotContain(temp.Path);
        marker.ShouldNotContain("entropy", Case.Insensitive);
        marker.ShouldNotContain("secret", Case.Insensitive);
    }

    [Fact]
    public void Initialized_runtime_fails_closed_when_material_is_missing_or_malformed()
    {
        using var temp = new TempRoot();
        var probe = CreateProbe(temp.Path);
        probe.IsAvailable().ShouldBeTrue();

        var protectedDirectory = Path.Combine(temp.Path, "data", "protected");
        File.Delete(Path.Combine(protectedDirectory, "db-open-secret.v1.dpapi"));

        probe.IsAvailable().ShouldBeFalse();
        File.Exists(Path.Combine(protectedDirectory, "db-open-secret.v1.dpapi")).ShouldBeFalse();

        File.WriteAllText(Path.Combine(protectedDirectory, "db-open-secret.v1.dpapi"), "not protected material");
        probe.IsAvailable().ShouldBeFalse();
    }

    [Fact]
    public void Protected_material_without_initialized_marker_fails_closed()
    {
        using var temp = new TempRoot();
        var probe = CreateProbe(temp.Path);
        probe.IsAvailable().ShouldBeTrue();

        var protectedDirectory = Path.Combine(temp.Path, "data", "protected");
        var markerPath = Path.Combine(protectedDirectory, "protected-material.initialized.json");
        File.Delete(markerPath);

        probe.IsAvailable().ShouldBeFalse();
        File.Exists(markerPath).ShouldBeFalse();
    }

    [Fact]
    public void Protected_blobs_do_not_persist_raw_material_bytes()
    {
        using var temp = new TempRoot();
        var probe = CreateProbe(temp.Path);
        probe.IsAvailable().ShouldBeTrue();
        var material = probe.LoadRequired();

        var protectedDirectory = Path.Combine(temp.Path, "data", "protected");
        var protectedEntropy = File.ReadAllBytes(Path.Combine(protectedDirectory, "app-entropy.v1.dpapi"));
        var protectedStableDeviceId = File.ReadAllBytes(Path.Combine(protectedDirectory, "stable-device-id.v1.dpapi"));
        var protectedDbSecret = File.ReadAllBytes(Path.Combine(protectedDirectory, "db-open-secret.v1.dpapi"));

        protectedEntropy.ShouldNotBe(material.ApplicationEntropy);
        protectedStableDeviceId.ShouldNotBe(material.StableDeviceId);
        protectedDbSecret.ShouldNotBe(material.DbOpenSecret);
    }

    [Fact]
    public void Db_open_secret_must_be_bound_to_application_entropy()
    {
        using var temp = new TempRoot();
        var protector = new RecordingProtector();
        var probe = CreateProbe(temp.Path, protector);
        probe.IsAvailable().ShouldBeTrue();

        var protectedDirectory = Path.Combine(temp.Path, "data", "protected");
        var dbOpenSecretPath = Path.Combine(protectedDirectory, "db-open-secret.v1.dpapi");
        var staticPurposeOnlyBlob = protector.Protect(
            RandomBytes(32),
            "Dobby.DbOpenSecret.v1"u8.ToArray());
        File.WriteAllBytes(dbOpenSecretPath, staticPurposeOnlyBlob);

        probe.IsAvailable().ShouldBeFalse(
            "DBOpenSecret must require the current AppEntropySecret as optional entropy, not only a static purpose label.");
    }

    private static ProtectedMaterialAvailabilityProbe CreateProbe(string dataRoot, IProtectedDataProtector? protector = null)
    {
        return new ProtectedMaterialAvailabilityProbe(
            ProtectedMaterialPaths.FromDataRoot(dataRoot),
            protector ?? new RecordingProtector());
    }

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    private sealed class RecordingProtector : IProtectedDataProtector
    {
        private static readonly byte[] Prefix = Encoding.ASCII.GetBytes("dobby-test-protected-v1:");

        public byte[] Protect(byte[] plaintext, byte[] entropy)
        {
            ArgumentNullException.ThrowIfNull(plaintext);
            ArgumentNullException.ThrowIfNull(entropy);

            var entropyHash = SHA256.HashData(entropy);
            var protectedBlob = new byte[Prefix.Length + entropyHash.Length + plaintext.Length];
            Prefix.CopyTo(protectedBlob, 0);
            entropyHash.CopyTo(protectedBlob, Prefix.Length);
            for (var i = 0; i < plaintext.Length; i++)
            {
                protectedBlob[Prefix.Length + entropyHash.Length + i] = (byte)(plaintext[i] ^ 0xA5);
            }

            return protectedBlob;
        }

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy)
        {
            ArgumentNullException.ThrowIfNull(protectedBlob);
            ArgumentNullException.ThrowIfNull(entropy);

            var entropyHash = SHA256.HashData(entropy);
            if (protectedBlob.Length < Prefix.Length + entropyHash.Length ||
                !protectedBlob.AsSpan(0, Prefix.Length).SequenceEqual(Prefix) ||
                !protectedBlob.AsSpan(Prefix.Length, entropyHash.Length).SequenceEqual(entropyHash))
            {
                throw new InvalidOperationException("Protected test blob is malformed or bound to different entropy.");
            }

            var plaintextLength = protectedBlob.Length - Prefix.Length - entropyHash.Length;
            var plaintext = new byte[plaintextLength];
            for (var i = 0; i < plaintext.Length; i++)
            {
                plaintext[i] = (byte)(protectedBlob[Prefix.Length + entropyHash.Length + i] ^ 0xA5);
            }

            return plaintext;
        }
    }

    private sealed class TempRoot : IDisposable
    {
        public TempRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dobby-protected-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
