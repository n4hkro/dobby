using Dobby.Localization;
using Shouldly;

namespace Dobby.UnitTests.Localization;

public sealed class HostLoginRecoveryStringCatalogTests
{
    public static TheoryData<string> Keys => new()
    {
        HostResourceKeys.LoginTitle,
        HostResourceKeys.LoginUsername,
        HostResourceKeys.LoginPassword,
        HostResourceKeys.LoginThrottleCountdown,
        HostResourceKeys.LoginRecoveryExport,
        HostResourceKeys.LoginRecoveryImport,
        "Dobby.Host.Login.InvalidCredentials",
        "Dobby.Host.Login.Deactivated",
        "Dobby.Host.Login.GenericFailure",
        HostResourceKeys.LockTitle,
        "Dobby.Host.Recovery.Title",
        "Dobby.Host.Recovery.OpenFolder",
        "Dobby.Host.Recovery.CopyPath",
        HostResourceKeys.RecoveryImportSignatureFailed,
        HostResourceKeys.RecoveryImportCancelled
    };

    [Theory]
    [MemberData(nameof(Keys))]
    public void Host_catalog_contains_sprint5_login_lock_and_recovery_keys(string key)
    {
        var value = new HostStringCatalog().GetString(key);

        value.ShouldNotBe($"[[{key}]]");
    }
}
