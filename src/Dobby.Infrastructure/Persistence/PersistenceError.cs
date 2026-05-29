namespace Dobby.Infrastructure.Persistence;

public sealed record PersistenceError(string Code, string Message)
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

    public static PersistenceError Create(string code, string message)
    {
        if (!AllowedCodes.Contains(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "PersistenceError code is not approved for Sprint 4.");
        }

        return new PersistenceError(code, message);
    }
}
