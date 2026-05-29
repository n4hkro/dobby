namespace Dobby.Infrastructure.ProtectedStorage;

public sealed record ProtectedMaterialSet(
    byte[] ApplicationEntropy,
    byte[] StableDeviceId,
    byte[] DbOpenSecret,
    byte[] ProtectedStableDeviceIdBlob);
