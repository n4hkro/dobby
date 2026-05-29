using System.Text.Json;
using System.Text.Json.Serialization;
using Dobby.Core;
using Dobby.Core.Versioning;

namespace Dobby.Security.Authentication;

public sealed record RecoveryRequestPayload(
    string Type,
    string DobbyVersion,
    string RequestedAt,
    RecoveryDeviceIdentity DeviceIdentity,
    string? DeviceDisplayName,
    string RequestId);

public sealed class RecoveryRequestExporter(
    IDeviceFingerprintSource deviceFingerprintSource,
    IHostVersionProvider hostVersionProvider,
    TimeProvider timeProvider,
    string? deviceDisplayName = null)
{
    public Result<RecoveryRequestPayload> CreateRequest()
    {
        var fingerprint = deviceFingerprintSource.Current();
        if (!fingerprint.IsSuccess)
        {
            return Result.Failure<RecoveryRequestPayload>(fingerprint.ErrorCode!.Value);
        }

        return Result.Success(new RecoveryRequestPayload(
            "dobby.owner-recovery-request.v1",
            hostVersionProvider.Current.ToString(),
            timeProvider.GetUtcNow().ToString("O"),
            new RecoveryDeviceIdentity(
                fingerprint.Value.MachineGuid,
                fingerprint.Value.CpuId,
                fingerprint.Value.MotherboardSerial,
                fingerprint.Value.DiskSerial,
                fingerprint.Value.MacAddress),
            string.IsNullOrWhiteSpace(deviceDisplayName) ? null : deviceDisplayName,
            Guid.NewGuid().ToString("D")));
    }

    public static string Serialize(RecoveryRequestPayload payload)
    {
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }
}
