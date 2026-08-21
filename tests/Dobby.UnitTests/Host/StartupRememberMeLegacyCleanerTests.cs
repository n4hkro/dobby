using Dobby.Host;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class StartupRememberMeLegacyCleanerTests
{
    [Fact]
    public void Cleanup_deletes_legacy_password_snapshot_and_username_snapshot_without_replacement()
    {
        using var temp = new TempDirectory();
        var legacyPasswordPath = Path.Combine(temp.Path, "startup-remember-me.json");
        var obsoleteUsernamePath = Path.Combine(temp.Path, "startup-remembered-account.json");
        File.WriteAllText(legacyPasswordPath, "{\"ProtectedPasswordBlob\":\"secret\"}");
        File.WriteAllText(obsoleteUsernamePath, "{\"UsernameNormalized\":\"owner\"}");

        StartupRememberMeLegacyCleaner.Clear(legacyPasswordPath, obsoleteUsernamePath);

        File.Exists(legacyPasswordPath).ShouldBeFalse();
        File.Exists(obsoleteUsernamePath).ShouldBeFalse();
        Directory.GetFiles(temp.Path).ShouldBeEmpty();
    }

    [Fact]
    public void Cleanup_is_idempotent_for_missing_files()
    {
        using var temp = new TempDirectory();

        Should.NotThrow(() => StartupRememberMeLegacyCleaner.Clear(
            Path.Combine(temp.Path, "missing-legacy.json"),
            Path.Combine(temp.Path, "missing-obsolete.json")));
    }

    [Fact]
    public void Startup_and_security_contracts_do_not_serialize_or_resolve_remembered_passwords()
    {
        var root = RepoRoot();
        var serializerContext = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Host",
            "HostStartupJsonSerializerContext.cs"));
        var loginViewModel = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Host",
            "Login",
            "LoginViewModel.cs"));
        var rememberedAccount = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Security",
            "Authentication",
            "RememberMeSlotService.cs"));

        serializerContext.ShouldNotContain("Remember", Case.Insensitive);
        serializerContext.ShouldNotContain("Password", Case.Insensitive);
        loginViewModel.ShouldNotContain("passwordResolver");
        loginViewModel.ShouldNotContain("SavedPasswordPlaceholder");
        rememberedAccount.ShouldNotContain("string Password");
        rememberedAccount.ShouldNotContain("IProtectedDataProtector");
        rememberedAccount.ShouldNotContain("ProtectedRememberMeBlob");
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"dobby-remember-me-cleaner-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
