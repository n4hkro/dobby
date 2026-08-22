using System.IO;
using Dobby.Core;

namespace Dobby.Host.Startup;

public interface IRuntimeTemporaryOutputRecovery
{
    Result<int> Recover();
}

public sealed class RuntimeTemporaryOutputRecovery(string temporaryRoot)
    : IRuntimeTemporaryOutputRecovery
{
    public Result<int> Recover()
    {
        try
        {
            var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(temporaryRoot));
            Directory.CreateDirectory(root);
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            {
                return Result.Failure<int>(ErrorCode.IoFailed);
            }

            var recovered = 0;
            foreach (var path in EnumerateSafeFiles(root))
            {
                var canonical = Path.GetFullPath(path);
                if (!IsUnderRoot(canonical, root) ||
                    (File.GetAttributes(canonical) & FileAttributes.ReparsePoint) != 0)
                {
                    return Result.Failure<int>(ErrorCode.IoFailed);
                }
                if (!IsRecoverableTemporaryName(Path.GetFileName(canonical)))
                {
                    continue;
                }

                File.Delete(canonical);
                recovered++;
            }
            return Result.Success(recovered);
        }
        catch (Exception exception) when (exception is
            IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidOperationException)
        {
            return Result.Failure<int>(ErrorCode.IoFailed);
        }
    }

    private static bool IsRecoverableTemporaryName(string fileName) =>
        fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".partial", StringComparison.OrdinalIgnoreCase) ||
        fileName.Contains(".partial.", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnderRoot(string path, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateSafeFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new UnauthorizedAccessException(
                        "Temporary recovery does not traverse reparse points.");
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                }
                else
                {
                    yield return entry;
                }
            }
        }
    }
}
