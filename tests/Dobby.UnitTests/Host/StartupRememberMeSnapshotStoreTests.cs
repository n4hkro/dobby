using Dobby.Security.Authentication;
using Shouldly;
using HostStartupRememberMeSnapshotStore = Dobby.Host.StartupRememberMeSnapshotStore;

namespace Dobby.UnitTests.Host;

public sealed class StartupRememberMeSnapshotStoreTests
{
    [Fact]
    public void Remember_me_snapshot_round_trips_saved_login_without_runtime_services()
    {
        using var temp = new TempFile();
        var saved = new RememberMeSavedLogin("user-1", "owner", "saved password", true);

        HostStartupRememberMeSnapshotStore.Save(temp.Path, saved);

        var contents = File.ReadAllText(temp.Path);
        contents.ShouldNotContain("saved password");
        var roundTrip = HostStartupRememberMeSnapshotStore.LoadSavedLogin(temp.Path);
        roundTrip.ShouldBe(saved);
    }

    [Fact]
    public void Remember_me_prefill_reads_identity_without_resolving_protected_secret()
    {
        using var temp = new TempFile();
        File.WriteAllText(
            temp.Path,
            """
            {"UserId":"user-1","MaskedUsername":"owner","ProtectedPasswordBlob":"not-base64","HasMaskedPasswordPrefill":true}
            """);

        var prefill = HostStartupRememberMeSnapshotStore.LoadSavedLoginPrefill(temp.Path);

        prefill.ShouldNotBeNull();
        prefill.UserId.ShouldBe("user-1");
        prefill.MaskedUsername.ShouldBe("owner");
        prefill.HasMaskedPasswordPrefill.ShouldBeTrue();
        HostStartupRememberMeSnapshotStore.LoadSavedLogin(temp.Path).ShouldBeNull();
    }

    [Fact]
    public void Missing_or_invalid_remember_me_snapshot_returns_no_saved_login()
    {
        using var temp = new TempFile();

        HostStartupRememberMeSnapshotStore.LoadSavedLogin(temp.Path).ShouldBeNull();
        File.WriteAllText(temp.Path, "{ invalid");

        HostStartupRememberMeSnapshotStore.LoadSavedLogin(temp.Path).ShouldBeNull();
    }

    [Fact]
    public void Clearing_remember_me_snapshot_removes_startup_prefill()
    {
        using var temp = new TempFile();
        HostStartupRememberMeSnapshotStore.Save(
            temp.Path,
            new RememberMeSavedLogin("user-1", "owner", "saved password", true));

        HostStartupRememberMeSnapshotStore.Clear(temp.Path);

        File.Exists(temp.Path).ShouldBeFalse();
        HostStartupRememberMeSnapshotStore.LoadSavedLogin(temp.Path).ShouldBeNull();
    }

    private sealed class TempFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"dobby-remember-me-startup-{Guid.NewGuid():N}.json");

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
