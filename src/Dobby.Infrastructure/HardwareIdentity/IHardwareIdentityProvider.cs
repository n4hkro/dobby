namespace Dobby.Infrastructure.HardwareIdentity;

public interface IHardwareIdentityProvider
{
    HardwareIdentitySnapshot Read();
}
