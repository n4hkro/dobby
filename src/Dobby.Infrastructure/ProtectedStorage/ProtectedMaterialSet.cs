namespace Dobby.Infrastructure.ProtectedStorage;

public sealed record ProtectedMaterialSet(
    byte[] ApplicationEntropy,
    byte[] StableDeviceId,
    byte[] DbOpenSecret,
    byte[] ProtectedStableDeviceIdBlob,
    byte[]? DeviceRecoveryPrivateKeyPkcs8 = null,
    byte[]? DeviceRecoveryPublicKeySpki = null);
