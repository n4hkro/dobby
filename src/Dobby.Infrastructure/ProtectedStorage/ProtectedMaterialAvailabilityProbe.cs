using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace Dobby.Infrastructure.ProtectedStorage;

public sealed class ProtectedMaterialAvailabilityProbe : IProtectedMaterialSource
{
    private static readonly byte[] NoAdditionalEntropy = [];
    private static readonly byte[] StableDeviceEntropy = "Dobby.StableDeviceId.v1"u8.ToArray();

    private readonly ProtectedMaterialPaths paths;
    private readonly IProtectedDataProtector localMachineProtector;

    public ProtectedMaterialAvailabilityProbe(
        ProtectedMaterialPaths paths,
        IProtectedDataProtector localMachineProtector)
    {
        this.paths = paths;
        this.localMachineProtector = localMachineProtector;
    }

    public static ProtectedMaterialAvailabilityProbe CreateForDataRoot(string dataRoot)
    {
#if WINDOWS
        return new ProtectedMaterialAvailabilityProbe(
            ProtectedMaterialPaths.FromDataRoot(dataRoot),
            new DpapiLocalMachineProtector());
#else
        throw new PlatformNotSupportedException("Default protected material probing requires the Windows DPAPI adapter slice.");
#endif
    }

    public bool IsAvailable()
    {
        try
        {
            if (File.Exists(paths.InitializedMarkerPath))
            {
                _ = LoadRequired();
                return true;
            }

            if (AnyProtectedMaterialExists())
            {
                throw new InvalidOperationException("Protected material exists without the initialized marker.");
            }

            CreateInitialMaterial();
            _ = LoadRequired();
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public ProtectedMaterialSet LoadRequired()
    {
        if (!File.Exists(paths.InitializedMarkerPath))
        {
            throw new InvalidOperationException("Protected material initialized marker is missing.");
        }

        ValidateMarker(paths.InitializedMarkerPath);

        var applicationEntropy = LoadProtected(paths.ApplicationEntropyPath, NoAdditionalEntropy);
        var stableDeviceId = LoadProtected(paths.StableDeviceIdPath, StableDeviceEntropy);
        var dbOpenSecret = LoadProtected(paths.DbOpenSecretPath, applicationEntropy);
        var protectedStableBlob = File.ReadAllBytes(paths.StableDeviceIdPath);

        return new ProtectedMaterialSet(applicationEntropy, stableDeviceId, dbOpenSecret, protectedStableBlob);
    }

    private void CreateInitialMaterial()
    {
        Directory.CreateDirectory(paths.ProtectedDirectory);
        ApplyRestrictedDirectoryAcl(paths.ProtectedDirectory);

        var applicationEntropy = RandomBytes(32);
        CreateProtectedFile(paths.ApplicationEntropyPath, applicationEntropy, NoAdditionalEntropy);
        CreateProtectedFile(paths.StableDeviceIdPath, RandomBytes(32), StableDeviceEntropy);
        CreateProtectedFile(paths.DbOpenSecretPath, RandomBytes(32), applicationEntropy);

        var marker = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["algorithmVersion"] = "random-32-v1",
            ["createdAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["protectionVersion"] = "dpapi-local-machine-v1"
        };

        File.WriteAllText(
            paths.InitializedMarkerPath,
            JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true }));
    }

    private bool AnyProtectedMaterialExists()
    {
        return File.Exists(paths.ApplicationEntropyPath) ||
            File.Exists(paths.StableDeviceIdPath) ||
            File.Exists(paths.DbOpenSecretPath);
    }

    private void CreateProtectedFile(string path, byte[] plaintext, byte[] entropy)
    {
        if (File.Exists(path))
        {
            return;
        }

        var protectedBlob = localMachineProtector.Protect(plaintext, entropy);
        File.WriteAllBytes(path, protectedBlob);
    }

    private static void ApplyRestrictedDirectoryAcl(string protectedDirectory)
    {
#if WINDOWS
        var directoryInfo = new DirectoryInfo(protectedDirectory);
        var security = directoryInfo.GetAccessControl(AccessControlSections.Access);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Current Windows user SID is unavailable.");

        security.ResetAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.FullControl,
            inheritance,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            inheritance,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            inheritance,
            PropagationFlags.None,
            AccessControlType.Allow));

        directoryInfo.SetAccessControl(security);
#endif
    }

    private byte[] LoadProtected(string path, byte[] entropy)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Protected material is missing: {Path.GetFileName(path)}.");
        }

        var protectedBlob = File.ReadAllBytes(path);
        if (protectedBlob.Length == 0)
        {
            throw new InvalidOperationException($"Protected material is empty: {Path.GetFileName(path)}.");
        }

        return localMachineProtector.Unprotect(protectedBlob, entropy);
    }

    private static void ValidateMarker(string markerPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(markerPath));
        var root = document.RootElement;
        RequireMarkerProperty(root, "protectionVersion");
        RequireMarkerProperty(root, "algorithmVersion");
        RequireMarkerProperty(root, "createdAtUtc");
    }

    private static void RequireMarkerProperty(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Protected material marker is missing {propertyName}.");
        }
    }

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }
}
