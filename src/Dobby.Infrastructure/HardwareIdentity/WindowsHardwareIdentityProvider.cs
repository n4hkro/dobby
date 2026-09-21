#if WINDOWS
using System.Security;
using Microsoft.Win32;
#endif
using Dobby.Core;

namespace Dobby.Infrastructure.HardwareIdentity;

/// <summary>
/// Reads the one hardware property permitted as a device binding input.
/// </summary>
/// <remarks>
/// Scope is deliberately minimal (`ADR-0058` § `Owner recovery v2`, `ADR-0063` §
/// `Canonical derivation`): the registry `MachineGuid` and nothing else. The WMI reads for
/// `Win32_Processor`, `Win32_BaseBoard` and `Win32_DiskDrive` plus the network adapter MAC
/// address were removed -- they populated the superseded `ADR-0039` 5-source fuzzy/quorum
/// fingerprint, had no consumer (`HostLoginRuntimeFactory` uses only `MachineGuid`), and ran
/// three synchronous WMI queries on every login path evaluation, which `AC-014` measures.
///
/// Do not add hardware components here. Broadening the collected set would rebuild the quorum
/// binding that `ADR-0058` supersedes and would widen the license binding derivation that
/// `ADR-0063` closes under `schemaVersion: 3`; both require a successor ADR.
///
/// <para>
/// The read is <em>fail-closed and typed</em>, per `ADR-0063` § 5: "mövcud olmayan signal
/// fail-closed olmalıdır, boş string kimi normalize edilib digest-ə daxil edilməməlidir".
/// This adapter previously collapsed three distinct machine states -- key absent, value
/// absent, value present -- into one `string.Empty` sentinel, and left two more states
/// (`SecurityException` from an ACL-restricted `SOFTWARE\Microsoft\Cryptography` key,
/// `IOException` from a key marked for deletion) as escaping exceptions: `?.` does not catch,
/// nothing on the call path from <c>HostLoginRuntimeFactory</c> through
/// <c>LicenseRuntimeModeProvider</c> catches, and there is no `DispatcherUnhandledException`
/// handler, so an ACL-restricted key crashed the WPF dispatcher with a message naming the
/// registry path -- the disclosure `ADR-0063` § 4 forbids. The sentinel only ever failed
/// closed by luck, through the downstream <c>LicenseDeviceFingerprintV1.Compute</c> guard.
/// </para>
/// <para>
/// Failure codes are deliberately adapter-level (`NotFound` / `Unauthorized` / `IoFailed`)
/// and are <em>not</em> `LicenseBindingMismatch`: this is a hardware adapter and licensing is
/// only one of its possible consumers, so the licensing collapse to a single non-disclosing
/// code belongs at the licensing boundary (`HostLoginRuntimeFactory`
/// § `CreateCurrentLicenseDeviceFingerprint`), where `ADR-0063` § 4 places it. The distinction
/// never reaches a user-visible message, an audit payload or a diagnostics export.
/// </para>
/// </remarks>
public sealed class WindowsHardwareIdentityProvider : IHardwareIdentityProvider
{
#if WINDOWS
    private const string CryptographyKeyPath = @"SOFTWARE\Microsoft\Cryptography";
    private const string MachineGuidValueName = "MachineGuid";

    private readonly Func<string?> _machineGuidReader;

    public WindowsHardwareIdentityProvider()
        : this(ReadMachineGuidFromRegistry)
    {
    }

    /// <summary>
    /// Test seam for the raw registry read.
    /// </summary>
    /// <remarks>
    /// Deliberately `internal` (visible only to `Dobby.UnitTests`): the absent-value and
    /// ACL-restricted paths cannot be provoked on a developer or CI machine -- the real
    /// `MachineGuid` is present and readable -- so without a seam those branches carry zero
    /// runtime proof and `ADR-0063`'s fail-closed obligation is closed by source reading
    /// alone, which `NFR-027` does not accept. Production composition uses the parameterless
    /// constructor; a seam reachable from production would itself be a binding-substitution
    /// surface.
    /// </remarks>
    internal WindowsHardwareIdentityProvider(Func<string?> machineGuidReader)
    {
        _machineGuidReader = machineGuidReader;
    }
#endif

    public Result<HardwareIdentitySnapshot> Read()
    {
#if WINDOWS
        string? rawMachineGuid;
        try
        {
            rawMachineGuid = _machineGuidReader();
        }
        catch (SecurityException)
        {
            // The key exists but this process may not read it (restricted ACL, hardened
            // image, or a policy-locked hive). Fail closed with a typed result rather than
            // letting the exception -- whose message names the registry path -- escape onto
            // the dispatcher.
            return Result.Failure<HardwareIdentitySnapshot>(ErrorCode.Unauthorized);
        }
        catch (UnauthorizedAccessException)
        {
            return Result.Failure<HardwareIdentitySnapshot>(ErrorCode.Unauthorized);
        }
        catch (IOException)
        {
            // `RegistryKey.GetValue` throws this when the key has been marked for deletion.
            return Result.Failure<HardwareIdentitySnapshot>(ErrorCode.IoFailed);
        }

        if (string.IsNullOrWhiteSpace(rawMachineGuid))
        {
            // Absent key, absent value and blank value are one outcome for the caller but
            // must never be one *input*: normalizing any of them to `string.Empty` and
            // hashing it would make every machine in that state share a binding digest.
            return Result.Failure<HardwareIdentitySnapshot>(ErrorCode.NotFound);
        }

        // Normalization is not applied here on purpose: `ADR-0063` § 1 assigns trim /
        // `ToLowerInvariant` / Unicode `FormC` to the derivation (`LicenseDeviceFingerprintV1`),
        // so that the canonical input is defined in exactly one place. This adapter reports
        // the raw value it read.
        return Result.Success(new HardwareIdentitySnapshot(rawMachineGuid));
#else
        throw new PlatformNotSupportedException("Windows hardware identity is available only in the Windows adapter slice.");
#endif
    }

#if WINDOWS
    private static string? ReadMachineGuidFromRegistry()
    {
        using var cryptographyKey = Registry.LocalMachine.OpenSubKey(CryptographyKeyPath);

        return cryptographyKey?.GetValue(MachineGuidValueName)?.ToString();
    }
#endif
}
