using Dobby.Core;

namespace Dobby.Security.Authentication;

public interface IDeviceFingerprintSource
{
    Result<DeviceFingerprint> Current();
}
