using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dobby.Host;

public static class StartupRememberMeSnapshotStore
{
    private const string FileName = "startup-remember-me.json";
    private static readonly byte[] PasswordPayloadPrefix = "Dobby:StartupRememberMePassword:v1\0"u8.ToArray();
    private static readonly byte[] SnapshotEntropy = SHA256.HashData(
        Encoding.UTF8.GetBytes("Dobby:StartupRememberMeSnapshot:v1"));

    public static string DefaultPath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = string.IsNullOrWhiteSpace(localAppData)
                ? Path.Combine(AppContext.BaseDirectory, "Dobby")
                : Path.Combine(localAppData, "Dobby");
            return Path.Combine(root, FileName);
        }
    }

    public static RememberMeSavedLogin? LoadSavedLogin()
    {
        return LoadSavedLogin(DefaultPath);
    }

    public static StartupRememberMePrefill? LoadSavedLoginPrefill()
    {
        return LoadSavedLoginPrefill(DefaultPath);
    }

    public static StartupRememberMePrefill? LoadSavedLoginPrefill(string path)
    {
        try
        {
            var snapshot = LoadSnapshot(path, clearInvalid: true);
            return snapshot is null
                ? null
                : new StartupRememberMePrefill(
                    snapshot.UserId,
                    snapshot.MaskedUsername,
                    snapshot.HasMaskedPasswordPrefill);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    public static RememberMeSavedLogin? LoadSavedLogin(string path)
    {
        try
        {
            var snapshot = LoadSnapshot(path, clearInvalid: true);
            if (snapshot is null)
            {
                return null;
            }

            var protectedBlob = Convert.FromBase64String(snapshot.ProtectedPasswordBlob);
            var plaintext = new DpapiCurrentUserProtector().Unprotect(protectedBlob, SnapshotEntropy);
            if (!plaintext.AsSpan().StartsWith(PasswordPayloadPrefix))
            {
                Clear(path);
                return null;
            }

            var savedCredential = Encoding.UTF8.GetString(plaintext.AsSpan(PasswordPayloadPrefix.Length));
            return new RememberMeSavedLogin(
                snapshot.UserId,
                snapshot.MaskedUsername,
                savedCredential,
                snapshot.HasMaskedPasswordPrefill);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    private static StartupRememberMeSnapshot? LoadSnapshot(string path, bool clearInvalid)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize(
            File.ReadAllBytes(path),
            HostStartupJsonSerializerContext.Default.StartupRememberMeSnapshot);
        if (snapshot is null ||
            string.IsNullOrWhiteSpace(snapshot.UserId) ||
            string.IsNullOrWhiteSpace(snapshot.MaskedUsername) ||
            string.IsNullOrWhiteSpace(snapshot.ProtectedPasswordBlob))
        {
            if (clearInvalid)
            {
                Clear(path);
            }

            return null;
        }

        return snapshot;
    }

    public static void Save(RememberMeSavedLogin? savedLogin)
    {
        if (savedLogin is null)
        {
            Clear();
            return;
        }

        Save(DefaultPath, savedLogin);
    }

    public static void Save(string path, RememberMeSavedLogin savedLogin)
    {
        ArgumentNullException.ThrowIfNull(savedLogin);

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = PasswordPayloadPrefix
                .Concat(Encoding.UTF8.GetBytes(savedLogin.Password))
                .ToArray();
            var protectedBlob = new DpapiCurrentUserProtector().Protect(payload, SnapshotEntropy);
            var snapshot = new StartupRememberMeSnapshot(
                savedLogin.UserId,
                savedLogin.MaskedUsername,
                Convert.ToBase64String(protectedBlob),
                savedLogin.HasMaskedPasswordPrefill);
            File.WriteAllBytes(
                path,
                JsonSerializer.SerializeToUtf8Bytes(
                    snapshot,
                    HostStartupJsonSerializerContext.Default.StartupRememberMeSnapshot));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    }

    public static void Clear()
    {
        Clear(DefaultPath);
    }

    public static void Clear(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    }

}

public sealed record StartupRememberMePrefill(
    string UserId,
    string MaskedUsername,
    bool HasMaskedPasswordPrefill);
