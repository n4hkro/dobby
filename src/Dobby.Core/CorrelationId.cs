namespace Dobby.Core;

public readonly record struct CorrelationId(Guid Value)
{
    public static CorrelationId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("D");
}

public interface ICorrelationIdGenerator
{
    CorrelationId New();
}

public sealed class CorrelationIdGenerator : ICorrelationIdGenerator
{
    public CorrelationId New() => CorrelationId.New();
}

public interface ICorrelationContextAccessor
{
    CorrelationId? Current { get; set; }
}

public sealed class CorrelationContextAccessor : ICorrelationContextAccessor
{
    private static readonly AsyncLocal<CorrelationId?> CurrentCorrelationId = new();

    public CorrelationId? Current
    {
        get => CurrentCorrelationId.Value;
        set
        {
            var existing = CurrentCorrelationId.Value;
            if (existing is { } existingId &&
                value is { } newId &&
                existingId != newId)
            {
                throw new InvalidOperationException("A correlation id is already active for the current scope.");
            }

            CurrentCorrelationId.Value = value;
        }
    }

    public IDisposable PushScope(CorrelationId id)
    {
        var previous = CurrentCorrelationId.Value;
        if (previous is not null)
        {
            throw new InvalidOperationException("A correlation id is already active for the current scope.");
        }

        CurrentCorrelationId.Value = id;
        return new Scope(previous);
    }

    private sealed class Scope(CorrelationId? previous) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            CurrentCorrelationId.Value = previous;
            disposed = true;
        }
    }
}
