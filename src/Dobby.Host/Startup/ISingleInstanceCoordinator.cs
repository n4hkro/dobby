namespace Dobby.Host.Startup;

public interface ISingleInstanceCoordinator
{
    SingleInstanceAcquireResult Acquire();

    void HandOffToFirstInstance();
}

public sealed record SingleInstanceAcquireResult(bool IsFirstInstance)
{
    public static SingleInstanceAcquireResult FirstInstance() => new(true);

    public static SingleInstanceAcquireResult SecondInstance() => new(false);
}
