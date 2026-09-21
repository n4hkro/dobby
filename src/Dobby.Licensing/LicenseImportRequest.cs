using Dobby.Core;

namespace Dobby.Licensing;

/// <remarks>
/// <see cref="CurrentDeviceFingerprintProvider"/> returns a <see cref="Result{T}"/>
/// because the current device binding digest is not always derivable -- the hardware
/// signal may be absent or unreadable -- and `ADR-0063` § 5 requires that state to fail
/// closed rather than be normalized into a placeholder value. A `Func&lt;string&gt;` seam
/// could only report it as a sentinel string (which would then be compared against the
/// artifact digest) or as a thrown exception (which escaped the import call entirely).
/// </remarks>
public sealed record LicenseImportRequest(
    string FileName,
    byte[] ArtifactBytes,
    Func<Result<string>> CurrentDeviceFingerprintProvider,
    Func<string, string> InstalledPackageFingerprintResolver,
    bool PermissionGranted,
    string CorrelationId,
    DateTimeOffset ImportedAtUtc,
    CancellationToken CancellationToken = default,
    string ActorId = "unknown");
