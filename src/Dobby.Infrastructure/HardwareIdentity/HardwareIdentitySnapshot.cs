namespace Dobby.Infrastructure.HardwareIdentity;

/// <summary>
/// The single hardware property Dobby is permitted to read for device binding.
/// </summary>
/// <remarks>
/// This snapshot deliberately carries <em>only</em> <see cref="MachineGuid"/>. It previously
/// also carried a processor id, baseboard serial, disk serial and MAC address -- the 5-source
/// fuzzy/quorum hardware fingerprint that `ADR-0039` defined and that `ADR-0058` superseded
/// ("Hardware properties may be collected only as support-safe risk signals. They are not
/// authentication factors, quorum hashes, encryption keys, or recovery binding authority.").
/// Four of those five values were read on every login and then discarded unused, so removing
/// them changes no behavior while deleting a collection surface that no current authority
/// permits.
///
/// `ADR-0063` § `Canonical derivation` fixes the license binding inputs as exactly
/// `machineGuid` plus the protected stable device id, and forbids fuzzy quorum, partial match
/// and "M-of-N" behavior. Adding another hardware component here would reintroduce the
/// superseded quorum model and requires a successor ADR, not an edit to this record.
/// </remarks>
public sealed record HardwareIdentitySnapshot(string MachineGuid);
