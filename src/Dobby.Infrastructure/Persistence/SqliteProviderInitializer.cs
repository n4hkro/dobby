using System.IO;
using System.Security.Cryptography;
using SQLitePCL;

namespace Dobby.Infrastructure.Persistence;

internal static class SqliteProviderInitializer
{
    private const string ApprovedNativeDllSha256 = "3744689bf8427b40b222c1ec046ebc5e4f08c5c9898d56573112006ff2c4def8";
    private static readonly object SyncRoot = new();
    private static bool initialized;
    private static string? initializedPath;
    private static IntPtr nativeHandle;

    public static PersistenceResult EnsureInitialized(string nativeDllPath)
    {
        lock (SyncRoot)
        {
            var hashResult = VerifyApprovedNativeDll(nativeDllPath);
            if (!hashResult.IsSuccess)
            {
                return hashResult;
            }

            if (initialized)
            {
                if (!Path.GetFullPath(nativeDllPath).Equals(initializedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return PersistenceResult.Failure(PersistenceError.Create(
                        PersistenceError.ProviderUnavailable,
                        "Approved native SQLite provider DLL path changed after initialization."));
                }

                return PersistenceResult.Success();
            }

            try
            {
                nativeHandle = NativeLibrary.Load(nativeDllPath);
                SQLite3Provider_dynamic_cdecl.Setup(nativeDllPath, NativeLibrary.Setup(nativeHandle));
                raw.SetProvider(new SQLite3Provider_dynamic_cdecl());
                initializedPath = Path.GetFullPath(nativeDllPath);
                initialized = true;
                return PersistenceResult.Success();
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
            {
                return PersistenceResult.Failure(PersistenceError.Create(
                    PersistenceError.ProviderUnavailable,
                    "Approved native SQLite provider could not be initialized."));
            }
        }
    }

    private static PersistenceResult VerifyApprovedNativeDll(string nativeDllPath)
    {
        if (!File.Exists(nativeDllPath))
        {
            return PersistenceResult.Failure(PersistenceError.Create(
                PersistenceError.ProviderUnavailable,
                "Approved native SQLite provider DLL is missing."));
        }

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nativeDllPath))).ToLowerInvariant();
        if (!hash.Equals(ApprovedNativeDllSha256, StringComparison.Ordinal))
        {
            return PersistenceResult.Failure(PersistenceError.Create(
                PersistenceError.ProviderUnavailable,
                "Approved native SQLite provider DLL hash mismatch."));
        }

        return PersistenceResult.Success();
    }
}
