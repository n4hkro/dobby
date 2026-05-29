using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Dobby.Infrastructure.ProtectedStorage;
using Shouldly;

namespace Dobby.IntegrationTests.Infrastructure;

public sealed class ProtectedMaterialDpapiTests
{
    [Fact]
    public void Dpapi_scope_protectors_round_trip_with_their_declared_scopes()
    {
        var plaintext = RandomBytes(32);
        var entropy = RandomBytes(16);
        var localMachineProtector = new DpapiLocalMachineProtector();
        var currentUserProtector = new DpapiCurrentUserProtector();

        var localMachineBlob = localMachineProtector.Protect(plaintext, entropy);
        var currentUserBlob = currentUserProtector.Protect(plaintext, entropy);

        localMachineProtector.Unprotect(localMachineBlob, entropy).ShouldBe(plaintext);
        currentUserProtector.Unprotect(currentUserBlob, entropy).ShouldBe(plaintext);
        ProtectedData.Unprotect(localMachineBlob, entropy, DataProtectionScope.LocalMachine).ShouldBe(plaintext);
        ProtectedData.Unprotect(currentUserBlob, entropy, DataProtectionScope.CurrentUser).ShouldBe(plaintext);

        var wrongEntropy = RandomBytes(16);
        Should.Throw<CryptographicException>(() => localMachineProtector.Unprotect(localMachineBlob, wrongEntropy));
        Should.Throw<CryptographicException>(() => currentUserProtector.Unprotect(currentUserBlob, wrongEntropy));
    }

    [Fact]
    public void Dpapi_scope_protectors_fail_closed_for_malformed_blobs()
    {
        var malformedBlob = new byte[] { 1, 2, 3, 4 };
        var entropy = RandomBytes(16);

        Should.Throw<CryptographicException>(() =>
            new DpapiLocalMachineProtector().Unprotect(malformedBlob, entropy));
        Should.Throw<CryptographicException>(() =>
            new DpapiCurrentUserProtector().Unprotect(malformedBlob, entropy));
    }

    [Fact]
    public void Protected_material_directory_has_explicit_restricted_acl()
    {
        using var temp = new TempRoot();
        var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(temp.Path);
        probe.IsAvailable().ShouldBeTrue();

        var protectedDirectory = Path.Combine(temp.Path, "data", "protected");
        var security = new DirectoryInfo(protectedDirectory).GetAccessControl(AccessControlSections.Access);
        security.AreAccessRulesProtected.ShouldBeTrue("protected material must not rely on inherited temp/profile ACLs alone.");

        var currentUser = WindowsIdentity.GetCurrent().User;
        currentUser.ShouldNotBeNull();
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToArray();

        rules.ShouldContain(rule =>
            rule.IdentityReference == currentUser &&
            rule.AccessControlType == AccessControlType.Allow &&
            rule.FileSystemRights.HasFlag(FileSystemRights.FullControl));
    }

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    private sealed class TempRoot : IDisposable
    {
        public TempRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dobby-protected-integration-" + Guid.NewGuid().ToString("N"));
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
