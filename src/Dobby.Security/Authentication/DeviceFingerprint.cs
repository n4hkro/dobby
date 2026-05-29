using System.Text.Json.Serialization;

namespace Dobby.Security.Authentication;

public sealed record DeviceFingerprint(
    string MachineGuid,
    string CpuId,
    string MotherboardSerial,
    string DiskSerial,
    string MacAddress);

public sealed record RecoveryDeviceIdentity(
    [property: JsonPropertyName("machineGuid")]
    string MachineGuid,
    [property: JsonPropertyName("cpuId")]
    string CpuId,
    [property: JsonPropertyName("motherboardSerial")]
    string MotherboardSerial,
    [property: JsonPropertyName("diskSerial")]
    string DiskSerial,
    [property: JsonPropertyName("macAddress")]
    string MacAddress);
