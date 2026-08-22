namespace Dobby.Infrastructure.Persistence;

public enum PersistenceErrorCategory
{
    Validation,
    Concurrency,
    Schema,
    Provider,
    Io,
    Serialization,
    Query,
    Unknown
}

public sealed record PersistenceError(
    string Code,
    string Message,
    PersistenceErrorCategory Category,
    bool IsRetryable)
{
    public const string ConstraintViolation = nameof(ConstraintViolation);
    public const string TransactionConflict = nameof(TransactionConflict);
    public const string SchemaMismatch = nameof(SchemaMismatch);
    public const string MigrationFailed = nameof(MigrationFailed);
    public const string ProviderUnavailable = nameof(ProviderUnavailable);
    public const string IoFailed = nameof(IoFailed);
    public const string SerializationFailed = nameof(SerializationFailed);
    public const string QueryFailed = nameof(QueryFailed);
    public const string Unknown = nameof(Unknown);

    private static readonly HashSet<string> AllowedCodes = new(StringComparer.Ordinal)
    {
        ConstraintViolation,
        TransactionConflict,
        SchemaMismatch,
        MigrationFailed,
        ProviderUnavailable,
        IoFailed,
        SerializationFailed,
        QueryFailed,
        Unknown
    };

    public static PersistenceError Create(string code, string? diagnosticDetail = null)
    {
        if (!AllowedCodes.Contains(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "PersistenceError code is not approved for Sprint 4.");
        }

        return code switch
        {
            ConstraintViolation => Safe(code, "The persistence operation violated a data constraint.", PersistenceErrorCategory.Validation, false),
            TransactionConflict => Safe(code, "The persistence operation conflicted with another transaction.", PersistenceErrorCategory.Concurrency, true),
            SchemaMismatch => Safe(code, "The persistence schema is not compatible.", PersistenceErrorCategory.Schema, false),
            MigrationFailed => Safe(code, "The persistence migration could not be completed.", PersistenceErrorCategory.Schema, false),
            ProviderUnavailable => Safe(code, "The encrypted persistence provider is unavailable.", PersistenceErrorCategory.Provider, true),
            IoFailed => Safe(code, "The persistence I/O operation could not be completed.", PersistenceErrorCategory.Io, true),
            SerializationFailed => Safe(code, "Persistence data could not be serialized safely.", PersistenceErrorCategory.Serialization, false),
            QueryFailed => Safe(code, "The persistence query could not be completed.", PersistenceErrorCategory.Query, true),
            _ => Safe(code, "The persistence operation failed.", PersistenceErrorCategory.Unknown, false)
        };
    }

    private static PersistenceError Safe(
        string code,
        string message,
        PersistenceErrorCategory category,
        bool retryable) => new(code, message, category, retryable);
}
