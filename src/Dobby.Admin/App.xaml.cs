using System.Windows;
using Dobby.Admin.Commands;

namespace Dobby.Admin;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0 && e.Args[0].Equals("--cli", StringComparison.Ordinal))
        {
            var result = AdminCommandHost.Run(e.Args);
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                Console.Out.WriteLine(result.StandardOutput);
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                Console.Error.WriteLine(result.StandardError);
            }

            Shutdown(result.ExitCode);
            return;
        }

        base.OnStartup(e);
    }
}
