namespace Dobby.Infrastructure.ProtectedStorage;

public interface IProtectedMaterialSource
{
    ProtectedMaterialSet LoadRequired();
}
