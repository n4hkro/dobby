using Shouldly;
using System.Text.RegularExpressions;

namespace Dobby.IntegrationTests.Architecture;

public sealed class AdminToolStaticScanTests
{
    [Theory]
    [InlineData("tools/sign-plugin-package/sign-plugin-package.ps1", "sign-plugin-package")]
    [InlineData("tools/create-plugin-license/create-plugin-license.ps1", "create-plugin-license")]
    [InlineData("tools/create-recovery-response/create-recovery-response.ps1", "create-recovery-response")]
    [InlineData("tools/verify-plugin-package/verify-plugin-package.ps1", "verify-plugin-package")]
    public void Admin_tool_wrappers_invoke_canonical_cli_commands(string wrapperPath, string command)
    {
        var root = RepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, wrapperPath));

        script.ShouldContain("Dobby.Admin");
        script.ShouldContain("--cli " + command);
        script.ShouldContain("exit $LASTEXITCODE");
        script.ShouldNotContain("private-key", Case.Insensitive);
    }

    [Fact]
    public void Admin_app_routes_cli_before_opening_wpf_shell()
    {
        var root = RepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "src/Dobby.Admin/App.xaml.cs"));

        app.ShouldContain("AdminCommandHost.Run(e.Args)");
        app.ShouldContain("Shutdown(result.ExitCode)");
    }

    [Fact]
    public void Approved_cli_contract_table_matches_the_command_option_sets_declared_in_code()
    {
        var root = RepositoryRoot();
        var documented = SpecContract(Path.Combine(root, "docs/specs/admin-tool.md"));
        var declared = CodeContract(Path.Combine(root, "src/Dobby.Admin/Commands/AdminCommandHost.cs"));

        documented.Keys.Order(StringComparer.Ordinal).ShouldBe(
            declared.Keys.Order(StringComparer.Ordinal),
            "docs/specs/admin-tool.md must document exactly the commands the CLI declares");

        foreach (var (command, options) in documented)
        {
            options.Order(StringComparer.Ordinal).ShouldBe(
                declared[command].Order(StringComparer.Ordinal),
                $"'{command}' option set drifted between the approved contract table and AdminCommandHost; " +
                "an option accepted by code but absent from the table is an undocumented public surface, " +
                "and an option in the table but absent from code exits 64 for operators");
        }
    }

    private static Dictionary<string, string[]> SpecContract(string specPath)
    {
        var lines = File.ReadAllLines(specPath);
        var start = Array.FindIndex(lines, line => line.StartsWith("### Approved P1 CLI contract", StringComparison.Ordinal));
        start.ShouldBeGreaterThanOrEqualTo(0);

        var contract = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var index = start + 1; index < lines.Length && !lines[index].StartsWith("## ", StringComparison.Ordinal); index++)
        {
            var cells = lines[index].Split('|', StringSplitOptions.TrimEntries);
            if (cells.Length < 5 || !cells[1].StartsWith('`') || cells[1].Contains(' ', StringComparison.Ordinal))
            {
                continue;
            }

            contract[cells[1].Trim('`')] = Options(cells[2]).Concat(Options(cells[3])).ToArray();
        }

        return contract;
    }

    private static Dictionary<string, string[]> CodeContract(string hostPath)
    {
        var source = File.ReadAllText(hostPath);
        var start = source.IndexOf("CommandSpecs = new Dictionary<string, CommandSpec>", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var end = source.IndexOf("\n    };", start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start);

        var constants = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(hostPath))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("private const string ", StringComparison.Ordinal) || !trimmed.Contains('"'))
            {
                continue;
            }

            var name = trimmed.Split(' ')[3];
            var value = trimmed[(trimmed.IndexOf('"') + 1)..trimmed.LastIndexOf('"')];
            constants[name] = value;
        }

        var contract = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var body = source[start..end];
        var entries = Regex.Matches(body, @"^\s*\[(?<key>\w+)\] = new\(", RegexOptions.Multiline);
        entries.Count.ShouldBeGreaterThan(0, "AdminCommandHost must declare command specs as [Constant] = new(...) entries");

        for (var index = 0; index < entries.Count; index++)
        {
            var constantName = entries[index].Groups["key"].Value;
            var segmentStart = entries[index].Index + entries[index].Length;
            var segmentEnd = index + 1 < entries.Count ? entries[index + 1].Index : body.Length;

            constants.TryGetValue(constantName, out var command).ShouldBeTrue(
                $"command constant '{constantName}' must resolve to a literal command name");
            contract[command!] = body[segmentStart..segmentEnd]
                .Split('"', StringSplitOptions.None)
                .Where((_, position) => position % 2 == 1)
                .Where(token => token.StartsWith("--", StringComparison.Ordinal))
                .ToArray();
        }

        return contract;
    }

    private static IEnumerable<string> Options(string cell)
    {
        return cell
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim('`'))
            .Where(token => token.StartsWith("--", StringComparison.Ordinal));
    }

    private static string RepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "Dobby.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
