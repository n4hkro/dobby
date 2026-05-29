#if WINDOWS
using System.Management;
using System.Net.NetworkInformation;
using Microsoft.Win32;
#endif

namespace Dobby.Infrastructure.HardwareIdentity;

public sealed class WindowsHardwareIdentityProvider : IHardwareIdentityProvider
{
    public HardwareIdentitySnapshot Read()
    {
#if WINDOWS
        return new HardwareIdentitySnapshot(
            ReadMachineGuid(),
            ReadWmiProperty("Win32_Processor", "ProcessorId"),
            ReadWmiProperty("Win32_BaseBoard", "SerialNumber"),
            ReadWmiProperty("Win32_DiskDrive", "SerialNumber"),
            ReadMacAddress());
#else
        throw new PlatformNotSupportedException("Windows hardware identity is available only in the Windows adapter slice.");
#endif
    }

#if WINDOWS
    private static string ReadMachineGuid()
    {
        return Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")?.GetValue("MachineGuid")?.ToString() ?? string.Empty;
    }

    private static string ReadWmiProperty(string className, string propertyName)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
        foreach (var item in searcher.Get().OfType<ManagementObject>())
        {
            var value = item[propertyName]?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string ReadMacAddress()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .Select(adapter => adapter.GetPhysicalAddress().ToString())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
#endif
}
