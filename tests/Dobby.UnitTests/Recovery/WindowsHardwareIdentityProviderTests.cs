using System.Security;
using Dobby.Core;
using Dobby.Infrastructure.HardwareIdentity;
using Shouldly;

namespace Dobby.UnitTests.Recovery;

public sealed class WindowsHardwareIdentityProviderTests
{
    [Fact]
    public void Provider_is_constructible_for_windows_adapter_slice()
    {
        var provider = new WindowsHardwareIdentityProvider();

        provider.ShouldBeAssignableTo<IHardwareIdentityProvider>();
    }

    [Fact]
    public void Present_machine_guid_is_returned_verbatim_without_adapter_side_normalization()
    {
        // `ADR-0063` § 1 assigns trim / `ToLowerInvariant` / Unicode `FormC` to the
        // derivation (`LicenseDeviceFingerprintV1`), so the canonical input is defined in
        // exactly one place. If this adapter also normalized, two normalizers would have to
        // be kept identical forever, and a divergence between them would silently change
        // every device's binding digest -- the failure `algorithmVersion` exists to make
        // explicit and impossible to do quietly.
        var provider = new WindowsHardwareIdentityProvider(() => "  AB-CD  ");

        var snapshot = provider.Read();

        snapshot.IsSuccess.ShouldBeTrue();
        snapshot.Value.MachineGuid.ShouldBe("  AB-CD  ");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Absent_or_blank_machine_guid_fails_closed_instead_of_normalizing_to_a_placeholder(
        string? rawMachineGuid)
    {
        // `ADR-0063` § 5: "movcud olmayan signal fail-closed olmalidir, bos string kimi
        // normalize edilib digest-e daxil edilmemelidir". This is the obligation with teeth:
        // an absent registry key, an absent value and a blank value are three distinct
        // machine states, and hashing any of them as `string.Empty` would give every machine
        // in that state the same binding digest -- one license artifact validating on all of
        // them. The adapter used to return `string.Empty` here and relied on a downstream
        // `Compute` guard to refuse it, which is fail-closed by luck rather than by contract.
        var provider = new WindowsHardwareIdentityProvider(() => rawMachineGuid);

        var snapshot = provider.Read();

        snapshot.IsSuccess.ShouldBeFalse();
        snapshot.ErrorCode.ShouldBe(ErrorCode.NotFound);
    }

    [Fact]
    public void Acl_restricted_cryptography_key_fails_closed_as_a_typed_result()
    {
        // The registry key `SOFTWARE\Microsoft\Cryptography` can be readable-by-default and
        // still be unreadable on a hardened image or a policy-locked hive; `OpenSubKey`
        // throws `SecurityException` then. The old adapter chained `?.` operators, which do
        // not catch, and nothing from `HostLoginRuntimeFactory` through
        // `LicenseRuntimeModeProvider` catches either -- with no
        // `DispatcherUnhandledException` handler, a real machine state crashed the WPF
        // dispatcher. This test is the reason the seam exists: the state cannot be produced
        // on a developer or CI machine, so without it the branch is unprovable.
        var provider = new WindowsHardwareIdentityProvider(
            () => throw new SecurityException("Requested registry access is not allowed."));

        var snapshot = provider.Read();

        snapshot.IsSuccess.ShouldBeFalse();
        snapshot.ErrorCode.ShouldBe(ErrorCode.Unauthorized);
    }

    [Fact]
    public void Unauthorized_access_to_the_cryptography_key_fails_closed_as_a_typed_result()
    {
        // `RegistryKey` surfaces denial as `SecurityException` on open and
        // `UnauthorizedAccessException` on some value reads. Catching only the first would
        // leave the second escaping, so both are asserted; they are the same outcome for the
        // caller and must not be distinguishable by it.
        var provider = new WindowsHardwareIdentityProvider(
            () => throw new UnauthorizedAccessException("Access to the registry key is denied."));

        var snapshot = provider.Read();

        snapshot.IsSuccess.ShouldBeFalse();
        snapshot.ErrorCode.ShouldBe(ErrorCode.Unauthorized);
    }

    [Fact]
    public void Registry_io_failure_fails_closed_as_a_typed_result()
    {
        // `GetValue` throws `IOException` when the key has been marked for deletion. Same
        // reasoning as above: fail closed with a typed result, never escape.
        var provider = new WindowsHardwareIdentityProvider(
            () => throw new IOException("The key has been marked for deletion."));

        var snapshot = provider.Read();

        snapshot.IsSuccess.ShouldBeFalse();
        snapshot.ErrorCode.ShouldBe(ErrorCode.IoFailed);
    }

    [Fact]
    public void Failure_results_disclose_neither_the_registry_path_nor_the_raw_value()
    {
        // `ADR-0063` § 4: "Uygunsuzluq mesaji non-disclosing-dir: gozlenilen/faktiki digest,
        // raw material, registry yolu ve exception detail user-visible mesajda, audit-de ve
        // diagnostics export-unda gorunmur." The old code satisfied this only because the
        // exception was never caught -- it went straight to a crash dialog carrying
        // `SOFTWARE\Microsoft\Cryptography` and the .NET exception text. A typed `ErrorCode`
        // carries no message at all, which is what makes the guarantee structural rather
        // than a matter of message discipline; this test pins that the code stays a bare
        // enumeration value and never grows a detail-bearing payload.
        var failures = new[]
        {
            new WindowsHardwareIdentityProvider(() => null).Read(),
            new WindowsHardwareIdentityProvider(
                () => throw new SecurityException(@"Cannot read HKLM\SOFTWARE\Microsoft\Cryptography")).Read(),
            new WindowsHardwareIdentityProvider(
                () => throw new IOException(@"HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid")).Read()
        };

        foreach (var failure in failures)
        {
            failure.IsSuccess.ShouldBeFalse();
            var rendered = failure.ErrorCode!.Value.ToString();
            rendered.ShouldNotContain("Cryptography");
            rendered.ShouldNotContain("MachineGuid");
            rendered.ShouldNotContain("HKLM");
            rendered.ShouldNotContain("SOFTWARE");
        }
    }

    [Fact]
    public void Hardware_identity_snapshot_carries_only_the_machine_guid()
    {
        // ADR-0058 § `Owner recovery v2` supersedes ADR-0039's 5-source fuzzy/quorum
        // hardware fingerprint, and ADR-0063 § `Canonical derivation` fixes the license
        // binding inputs as exactly `machineGuid` plus the protected stable device id --
        // forbidding fuzzy quorum, partial match and "M-of-N" behavior.
        //
        // The snapshot previously carried a processor id, baseboard serial, disk serial and
        // MAC address alongside the MachineGuid. Nothing consumed them: the sole caller uses
        // only MachineGuid, and no risk-signal consumer exists anywhere in the repository.
        // A snapshot member is therefore the cheapest way to rebuild the superseded quorum
        // model -- the value would be collected, then be available to any later derivation
        // change. This test pins the collected set so widening it is a deliberate,
        // ADR-bearing act rather than an unnoticed field addition.
        var members = typeof(HardwareIdentitySnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .Where(name => name != "EqualityContract")
            .ToArray();

        members.ShouldBe([nameof(HardwareIdentitySnapshot.MachineGuid)]);
    }

    [Fact]
    public void Hardware_identity_adapter_runs_no_wmi_query()
    {
        // The three synchronous WMI queries (`Win32_Processor`, `Win32_BaseBoard`,
        // `Win32_DiskDrive`) ran on every login path evaluation while feeding nothing, and
        // `AC-014` measures login latency. They were also the entire approval basis for the
        // `System.Management` pin, which ADR-0058 superseded. Source-text scan is the right
        // instrument here: the assertion is about which platform APIs this adapter is allowed
        // to reach at all, which is an architecture-shape obligation (NFR-027 permits
        // source-text assertions for exactly that, and this closes no runtime gate).
        // Comment lines are excluded deliberately: the adapter's own XML docs name the removed
        // WMI classes to explain why they must not come back, and a scan that counted prose
        // would either fail on that explanation or pressure someone to delete it.
        var adapterCode = string.Join(
            '\n',
            File.ReadAllLines(HardwareIdentityAdapterPath())
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        adapterCode.ShouldNotContain("ManagementObjectSearcher");
        adapterCode.ShouldNotContain("using System.Management");
        adapterCode.ShouldNotContain("Win32_Processor");
        adapterCode.ShouldNotContain("Win32_BaseBoard");
        adapterCode.ShouldNotContain("Win32_DiskDrive");

        // The MAC address was the fifth source of the same superseded `ADR-0039` set. It did
        // not come from WMI, so the class-name assertions above would not have caught its
        // return: `NetworkInterface.GetAllNetworkInterfaces()` lives in the BCL and needs no
        // package pin. Guarding only the three WMI classes would have left the cheapest
        // re-entry path for the quorum model open.
        adapterCode.ShouldNotContain("NetworkInterface");
        adapterCode.ShouldNotContain("GetPhysicalAddress");
    }

    private static string HardwareIdentityAdapterPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        var root = current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");

        return Path.Combine(
            root.FullName,
            "src",
            "Dobby.Infrastructure",
            "HardwareIdentity",
            "WindowsHardwareIdentityProvider.cs");
    }
}
