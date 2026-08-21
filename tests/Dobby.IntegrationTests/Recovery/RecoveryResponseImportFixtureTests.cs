using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.IntegrationTests.Recovery;

public sealed class RecoveryResponseImportFixtureTests
{
    [Fact]
    public void Legacy_v1_response_fixtures_are_rejected_without_silent_compatibility_fallback()
    {
        foreach (var path in Directory.EnumerateFiles(
            FixtureDirectory(),
            "*.TEST-ONLY.dobbyrecovery",
            SearchOption.TopDirectoryOnly))
        {
            Should.Throw<InvalidOperationException>(() =>
                RecoveryResponseImporter.ReadEnvelope(File.ReadAllText(path)));
        }
    }

    private static string FixtureDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return Path.Combine(
            (current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln")).FullName,
            "tests",
            "Dobby.IntegrationTests",
            "Recovery",
            "Fixtures",
            "TEST-ONLY");
    }
}
