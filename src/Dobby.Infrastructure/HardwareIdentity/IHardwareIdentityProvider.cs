using Dobby.Core;

namespace Dobby.Infrastructure.HardwareIdentity;

public interface IHardwareIdentityProvider
{
    /// <summary>
    /// Reads the permitted hardware identity signal, or fails closed.
    /// </summary>
    /// <remarks>
    /// The return type is a <see cref="Result{T}"/> rather than a bare snapshot because
    /// `ADR-0063` § 5 requires an unavailable signal to fail closed instead of being
    /// normalized into a placeholder: a snapshot-returning signature can only report
    /// "unavailable" as a sentinel value, and a sentinel that reaches the derivation is a
    /// digest every affected machine would share.
    /// </remarks>
    Result<HardwareIdentitySnapshot> Read();
}
