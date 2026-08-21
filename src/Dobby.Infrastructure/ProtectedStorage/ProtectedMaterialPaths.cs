using System.IO;

namespace Dobby.Infrastructure.ProtectedStorage;

public sealed record ProtectedMaterialPaths(
    string ProtectedDirectory,
    string ApplicationEntropyPath,
    string StableDeviceIdPath,
    string DbOpenSecretPath,
    string DeviceRecoveryPrivateKeyPath,
    string InitializedMarkerPath)
{
    public static ProtectedMaterialPaths FromDataRoot(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);

        var protectedDirectory = Path.Combine(
            Environment.ExpandEnvironmentVariables(dataRoot),
            "data",
            "protected");

        return new ProtectedMaterialPaths(
            protectedDirectory,
            Path.Combine(protectedDirectory, "app-entropy.v1.dpapi"),
            Path.Combine(protectedDirectory, "stable-device-id.v1.dpapi"),
            Path.Combine(protectedDirectory, "db-open-secret.v1.dpapi"),
            Path.Combine(protectedDirectory, "device-recovery-private-key.p256.v1.dpapi"),
            Path.Combine(protectedDirectory, "protected-material.initialized.json"));
    }
}
