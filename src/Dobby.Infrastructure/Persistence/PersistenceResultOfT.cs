namespace Dobby.Infrastructure.Persistence;

public sealed class PersistenceResult<T>
{
    private readonly T? value;
    private readonly PersistenceError? error;

    private PersistenceResult(bool isSuccess, T? value, PersistenceError? error)
    {
        IsSuccess = isSuccess;
        this.value = value;
        this.error = error;
    }

    public bool IsSuccess { get; }

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("A failed persistence result does not expose a value.");

    public PersistenceError Error => error
        ?? throw new InvalidOperationException("A successful persistence result does not expose an error.");

    public static PersistenceResult<T> Success(T value) => new(true, value, null);

    public static PersistenceResult<T> Failure(PersistenceError error) => new(false, default, error);
}
