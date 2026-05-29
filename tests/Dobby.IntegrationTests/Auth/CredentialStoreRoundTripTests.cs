using Dobby.Configuration;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Shouldly;
using System.Security.Cryptography;

namespace Dobby.IntegrationTests.Auth;

public sealed class CredentialStoreRoundTripTests
{
    [Fact]
    public void Credential_store_writes_protected_pbkdf2_material_and_verifies_password_round_trip()
    {
        using var fixture = CredentialStoreFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var now = DateTimeOffset.UtcNow;
        fixture.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', @DisplayName, 1, 1, @Now, @Now);",
            new { UserId = "user-1", DisplayName = "Əli Məmmədov", Now = now.ToString("O") });
        var protector = new RecordingProtector();
        var store = new CredentialStore(
            fixture.Executor,
            protector,
            new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" }),
            fixture.ApplicationEntropy);

        store.CreateCredential("user-1", "correct horse battery staple", now).IsSuccess.ShouldBeTrue();

        var row = fixture.Executor.Query<CredentialRow>(
            "SELECT KdfMode, KdfVersion, PasswordHash, Salt, SaltBitLength FROM Credentials WHERE UserId = @UserId;",
            new { UserId = "user-1" }).Single();
        row.KdfMode.ShouldBe("Pbkdf2Sha256");
        row.KdfVersion.ShouldBe("PBKDF2-HMAC-SHA256:600000");
        row.SaltBitLength.ShouldBe(128);
        row.PasswordHash.Take(RecordingProtector.ProtectedPrefix.Length).SequenceEqual(RecordingProtector.ProtectedPrefix).ShouldBeTrue();
        row.Salt.Take(RecordingProtector.ProtectedPrefix.Length).SequenceEqual(RecordingProtector.ProtectedPrefix).ShouldBeTrue();
        protector.ProtectCalls.ShouldBe(2);
        protector.ProtectEntropies[0].ShouldBe(protector.ProtectEntropies[1]);
        store.VerifyPassword("user-1", "correct horse battery staple").Value.ShouldBeTrue();
        store.VerifyPassword("user-1", "wrong").Value.ShouldBeFalse();
    }

    [Fact]
    public void Credential_store_uses_user_scoped_entropy_for_protected_material()
    {
        using var fixture = CredentialStoreFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var now = DateTimeOffset.UtcNow;
        fixture.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', NULL, 1, 1, @Now, @Now);",
            new { UserId = "user-1", Now = now.ToString("O") });
        fixture.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'other', NULL, 0, 1, @Now, @Now);",
            new { UserId = "user-2", Now = now.ToString("O") });
        var protector = new RecordingProtector();
        var store = new CredentialStore(
            fixture.Executor,
            protector,
            new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" }),
            [1, 2, 3]);
        var storeWithDifferentApplicationEntropy = new CredentialStore(
            fixture.Executor,
            protector,
            new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" }),
            [9, 8, 7]);

        store.CreateCredential("user-1", "first password", now).IsSuccess.ShouldBeTrue();
        store.CreateCredential("user-2", "second password", now).IsSuccess.ShouldBeTrue();
        storeWithDifferentApplicationEntropy.RebindPassword("user-1", "updated first password", now).IsSuccess.ShouldBeTrue();

        protector.ProtectEntropies[0].ShouldNotBe(protector.ProtectEntropies[2]);
        protector.ProtectEntropies[0].ShouldNotBe(protector.ProtectEntropies[4]);
    }

    [Fact]
    public void Credential_store_rebind_replaces_protected_material_so_old_password_fails()
    {
        using var fixture = CredentialStoreFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var now = DateTimeOffset.UtcNow;
        fixture.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', NULL, 1, 1, @Now, @Now);",
            new { UserId = "user-1", Now = now.ToString("O") });
        var store = new CredentialStore(
            fixture.Executor,
            new RecordingProtector(),
            new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" }),
            fixture.ApplicationEntropy);
        store.CreateCredential("user-1", "old password", now).IsSuccess.ShouldBeTrue();

        store.RebindPassword("user-1", "new password", now.AddMinutes(1)).IsSuccess.ShouldBeTrue();

        store.VerifyPassword("user-1", "old password").Value.ShouldBeFalse();
        store.VerifyPassword("user-1", "new password").Value.ShouldBeTrue();
    }

    [Fact]
    public void Tampered_protected_credential_material_fails_closed_without_throwing()
    {
        using var fixture = CredentialStoreFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var now = DateTimeOffset.UtcNow;
        fixture.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', NULL, 1, 1, @Now, @Now);",
            new { UserId = "user-1", Now = now.ToString("O") });
        var store = new CredentialStore(
            fixture.Executor,
            new ThrowingUnprotectProtector(),
            new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" }),
            fixture.ApplicationEntropy);
        fixture.Executor.Execute(
            "INSERT INTO Credentials(CredentialId, UserId, KdfMode, KdfVersion, PasswordHash, Salt, SaltBitLength, CreatedAtUtc, UpdatedAtUtc, PasswordChangedAtUtc) VALUES (@CredentialId, @UserId, 'Pbkdf2Sha256', 'PBKDF2-HMAC-SHA256:600000', @PasswordHash, @Salt, 128, @Now, @Now, NULL);",
            new
            {
                CredentialId = Guid.CreateVersion7().ToString("D"),
                UserId = "user-1",
                PasswordHash = new byte[] { 1, 2, 3 },
                Salt = new byte[] { 4, 5, 6 },
                Now = now.ToString("O")
            });

        var result = store.VerifyPassword("user-1", "password");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Unauthorized);
    }

    [Fact]
    public void Credential_store_fails_closed_for_unknown_persisted_kdf_mode()
    {
        using var fixture = CredentialStoreFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var now = DateTimeOffset.UtcNow;
        fixture.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', NULL, 1, 1, @Now, @Now);",
            new { UserId = "user-1", Now = now.ToString("O") });
        var store = new CredentialStore(
            fixture.Executor,
            new RecordingProtector(),
            new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" }),
            fixture.ApplicationEntropy);
        store.CreateCredential("user-1", "correct horse battery staple", now).IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            "UPDATE Credentials SET KdfMode = 'UnknownKdf' WHERE UserId = @UserId;",
            new { UserId = "user-1" });

        var result = store.VerifyPassword("user-1", "correct horse battery staple");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Unauthorized);
    }

    [Fact]
    public void Credential_store_fails_closed_for_under_floor_persisted_iterations()
    {
        using var fixture = CredentialStoreFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var now = DateTimeOffset.UtcNow;
        fixture.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', NULL, 1, 1, @Now, @Now);",
            new { UserId = "user-1", Now = now.ToString("O") });
        var protector = new RecordingProtector();
        var salt = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            "correct horse battery staple",
            salt,
            1,
            HashAlgorithmName.SHA256,
            32);
        fixture.Executor.Execute(
            "INSERT INTO Credentials(CredentialId, UserId, KdfMode, KdfVersion, PasswordHash, Salt, SaltBitLength, CreatedAtUtc, UpdatedAtUtc, PasswordChangedAtUtc) VALUES (@CredentialId, @UserId, 'Pbkdf2Sha256', 'PBKDF2-HMAC-SHA256:1', @PasswordHash, @Salt, 128, @Now, @Now, NULL);",
            new
            {
                CredentialId = Guid.CreateVersion7().ToString("D"),
                UserId = "user-1",
                PasswordHash = protector.Protect(hash, []),
                Salt = protector.Protect(salt, []),
                Now = now.ToString("O")
            });
        var store = new CredentialStore(
            fixture.Executor,
            protector,
            new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" }),
            fixture.ApplicationEntropy);

        var result = store.VerifyPassword("user-1", "correct horse battery staple");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Unauthorized);
    }

    [Fact]
    public void Credential_store_fails_closed_for_under_floor_persisted_hash_length()
    {
        using var fixture = CredentialStoreFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var now = DateTimeOffset.UtcNow;
        fixture.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', NULL, 1, 1, @Now, @Now);",
            new { UserId = "user-1", Now = now.ToString("O") });
        var protector = new RecordingProtector();
        var salt = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        var shortHash = Rfc2898DeriveBytes.Pbkdf2(
            "correct horse battery staple",
            salt,
            600_000,
            HashAlgorithmName.SHA256,
            1);
        fixture.Executor.Execute(
            "INSERT INTO Credentials(CredentialId, UserId, KdfMode, KdfVersion, PasswordHash, Salt, SaltBitLength, CreatedAtUtc, UpdatedAtUtc, PasswordChangedAtUtc) VALUES (@CredentialId, @UserId, 'Pbkdf2Sha256', 'PBKDF2-HMAC-SHA256:600000', @PasswordHash, @Salt, 128, @Now, @Now, NULL);",
            new
            {
                CredentialId = Guid.CreateVersion7().ToString("D"),
                UserId = "user-1",
                PasswordHash = protector.Protect(shortHash, []),
                Salt = protector.Protect(salt, []),
                Now = now.ToString("O")
            });
        var store = new CredentialStore(
            fixture.Executor,
            protector,
            new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" }),
            fixture.ApplicationEntropy);

        var result = store.VerifyPassword("user-1", "correct horse battery staple");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Unauthorized);
    }

    private sealed class CredentialRow
    {
        public string KdfMode { get; init; } = string.Empty;

        public string? KdfVersion { get; init; }

        public byte[] PasswordHash { get; init; } = [];

        public byte[] Salt { get; init; } = [];

        public int SaltBitLength { get; init; }
    }

    private sealed class RecordingProtector : IProtectedDataProtector
    {
        public static readonly byte[] ProtectedPrefix = "protected:"u8.ToArray();

        public int ProtectCalls { get; private set; }

        public List<byte[]> ProtectEntropies { get; } = [];

        public byte[] Protect(byte[] plaintext, byte[] entropy)
        {
            ProtectCalls++;
            ProtectEntropies.Add(entropy.ToArray());
            return ProtectedPrefix.Concat(plaintext.Select(value => (byte)(value ^ 0x5A))).ToArray();
        }

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy)
        {
            protectedBlob.Take(ProtectedPrefix.Length).SequenceEqual(ProtectedPrefix).ShouldBeTrue();
            return protectedBlob.Skip(ProtectedPrefix.Length).Select(value => (byte)(value ^ 0x5A)).ToArray();
        }
    }

    private sealed class ThrowingUnprotectProtector : IProtectedDataProtector
    {
        public byte[] Protect(byte[] plaintext, byte[] entropy) => plaintext;

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy) => throw new InvalidOperationException("unwrap failure");
    }

    private sealed class CredentialStoreFixture : IDisposable
    {
        private CredentialStoreFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            Probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            Probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, Probe);
            Migrator = new HostPersistenceMigrator(Factory, Probe);
            Executor = new HostPersistenceCommandExecutor(Factory);
        }

        public string Root { get; }

        public ProtectedMaterialAvailabilityProbe Probe { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceMigrator Migrator { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public byte[] ApplicationEntropy => Probe.LoadRequired().ApplicationEntropy;

        public static CredentialStoreFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-credential-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new CredentialStoreFixture(root);
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
