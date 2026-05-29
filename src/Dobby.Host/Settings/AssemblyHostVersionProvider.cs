using System.Reflection;
using Dobby.Core.Versioning;

namespace Dobby.Host.Settings;

public sealed class AssemblyHostVersionProvider(Assembly assembly) : IHostVersionProvider
{
    public HostVersion Current { get; } = Resolve(assembly);

    private static HostVersion Resolve(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var canonical = informationalVersion.Split(['-', '+'], 2)[0];
            if (HostVersion.TryParse(canonical, out var hostVersion))
            {
                return hostVersion;
            }
        }

        var assemblyVersion = assembly.GetName().Version;
        return assemblyVersion is null
            ? new HostVersion(0, 0, 0)
            : new HostVersion(
                Math.Max(assemblyVersion.Major, 0),
                Math.Max(assemblyVersion.Minor, 0),
                Math.Max(assemblyVersion.Build, 0));
    }
}
