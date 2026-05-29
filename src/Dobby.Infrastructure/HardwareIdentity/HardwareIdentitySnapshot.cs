namespace Dobby.Infrastructure.HardwareIdentity;

public sealed record HardwareIdentitySnapshot(
    string MachineGuid,
    string CpuId,
    string MotherboardSerial,
    string DiskSerial,
    string MacAddress);
