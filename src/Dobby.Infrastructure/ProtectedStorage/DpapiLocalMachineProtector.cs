using System.Security.Cryptography;

namespace Dobby.Infrastructure.ProtectedStorage;

public sealed class DpapiLocalMachineProtector : IProtectedDataProtector
{
    public byte[] Protect(byte[] plaintext, byte[] entropy)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(entropy);

#if WINDOWS
        return ProtectedData.Protect(plaintext, entropy, DataProtectionScope.LocalMachine);
#else
        throw new PlatformNotSupportedException("DPAPI LocalMachine protection is only available in the Windows adapter slice.");
#endif
    }

    public byte[] Unprotect(byte[] protectedBlob, byte[] entropy)
    {
        ArgumentNullException.ThrowIfNull(protectedBlob);
        ArgumentNullException.ThrowIfNull(entropy);

#if WINDOWS
        return ProtectedData.Unprotect(protectedBlob, entropy, DataProtectionScope.LocalMachine);
#else
        throw new PlatformNotSupportedException("DPAPI LocalMachine protection is only available in the Windows adapter slice.");
#endif
    }
}
