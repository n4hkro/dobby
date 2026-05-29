namespace Dobby.Admin.Commands;

public static class AdminCommandHost
{
    private static readonly HashSet<string> KnownCommands = new(StringComparer.Ordinal)
    {
        "sign-plugin-package",
        "create-plugin-license",
        "create-recovery-response"
    };

    public static AdminCommandResult Run(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count < 2 || args[0] != "--cli")
        {
            return new AdminCommandResult(64, string.Empty, "Usage: Dobby.Admin.exe --cli <command>");
        }

        var command = args[1];
        if (!KnownCommands.Contains(command))
        {
            return new AdminCommandResult(64, string.Empty, $"Unknown admin command: {command}");
        }

        if (args.Count > 2)
        {
            return new AdminCommandResult(64, string.Empty, "Admin command option contract is not approved for the skeleton.");
        }

        return new AdminCommandResult(0, $"{command}: ready", string.Empty);
    }
}

public sealed record AdminCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
