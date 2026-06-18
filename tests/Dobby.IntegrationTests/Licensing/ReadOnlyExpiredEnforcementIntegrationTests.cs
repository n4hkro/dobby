using System.Text.RegularExpressions;
using Shouldly;

namespace Dobby.IntegrationTests.Licensing;

public sealed class ReadOnlyExpiredEnforcementIntegrationTests
{
    [Fact]
    public void Readonly_expired_matrix_has_gate_tests_or_explicit_absent_surface_proof()
    {
        var root = RepoRoot();
        var sourceFiles = SourceFiles(root).ToArray();

        File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Plugins", "HostPluginPackageInstallCoordinator.cs"))
            .ShouldContain("PluginRuntimeMode.ReadOnlyExpired");
        File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Application", "Printing", "PrintTargetClassifier.cs"))
            .ShouldContain("EnsureReadOnlyExpiredOutputAllowed");

        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*PluginStorage\w*")
            .ShouldBeEmpty("plugin storage write has contract/SDK helpers only; add a ReadOnlyExpired gate test when a concrete owner appears.");
        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*(PluginCommand|PluginAction)\w*(Mutation|Mutator|Executor|Coordinator)\w*")
            .ShouldBeEmpty("plugin command/action mutation has no concrete owner yet; add a ReadOnlyExpired gate test when one appears.");
        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*PluginBackground(Task|Operation)\w*(Starter|Coordinator|Runner)\w*")
            .ShouldBeEmpty("plugin background task start has no concrete owner yet; add a ReadOnlyExpired gate test when one appears.");
        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*Plugin(Domain)?Import\w*(Coordinator|Service|Writer)\w*")
            .ShouldBeEmpty("plugin/domain import has no concrete owner yet; add a ReadOnlyExpired gate test when one appears.");
        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*Plugin(Domain)?Export\w*(Coordinator|Service|Writer)\w*")
            .ShouldBeEmpty("plugin/domain export has no concrete owner yet; add a ReadOnlyExpired gate test when one appears.");
        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*PluginReport\w*(Coordinator|Service|Writer)\w*")
            .ShouldBeEmpty("plugin/domain report output has no concrete owner yet; add a ReadOnlyExpired gate test when one appears.");
        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*PluginFile(Output|Export)\w*(Coordinator|Service|Writer)\w*")
            .ShouldBeEmpty("plugin/domain file output has no concrete owner yet; add a ReadOnlyExpired gate test when one appears.");
    }

    private static IEnumerable<string> ConcreteSourceOwners(IEnumerable<FileInfo> sourceFiles, string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        return sourceFiles
            .Where(file => regex.IsMatch(File.ReadAllText(file.FullName)))
            .Select(file => file.FullName)
            .ToArray();
    }

    private static IEnumerable<FileInfo> SourceFiles(DirectoryInfo root)
    {
        return root.GetDirectories("src").Single()
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file =>
                !file.FullName.Contains($"{Path.DirectorySeparatorChar}Dobby.Contracts{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !file.FullName.Contains($"{Path.DirectorySeparatorChar}Dobby.SDK{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static DirectoryInfo RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Dobby.sln")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
