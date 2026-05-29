namespace Dobby.Infrastructure.ProtectedStorage;

public interface IProtectedDataProtector
{
    byte[] Protect(byte[] plaintext, byte[] entropy);

    byte[] Unprotect(byte[] protectedBlob, byte[] entropy);
}
