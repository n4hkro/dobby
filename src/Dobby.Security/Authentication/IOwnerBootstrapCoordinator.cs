using Dobby.Core;

namespace Dobby.Security.Authentication;

public interface IOwnerBootstrapCoordinator
{
    Result BootstrapFirstOwner(string username, string? displayName, string password, CorrelationId correlationId);
}
