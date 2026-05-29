namespace Dobby.Infrastructure.Persistence;

public sealed class PersistenceResult
{
    private readonly PersistenceError? error;

    private PersistenceResult(bool isSuccess, PersistenceError? error)
    {
        IsSuccess = isSuccess;
        this.error = error;
    }

    public bool IsSuccess { get; }

    public PersistenceError Error => error
        ?? throw new InvalidOperationException("A successful persistence result does not expose an error.");

    public static PersistenceResult Success() => new(true, null);

    public static PersistenceResult Failure(PersistenceError error) => new(false, error);
}
