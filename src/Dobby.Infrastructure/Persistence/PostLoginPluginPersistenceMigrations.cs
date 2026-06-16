using System.Reflection;
using System.Text;

namespace Dobby.Infrastructure.Persistence;

public static class PostLoginPluginPersistenceMigrations
{
    public const string MigrationId = "0002_postlogin_bootstrap";
    private const string ResourceName = "Dobby.Infrastructure.Persistence.Migrations.0002_postlogin_bootstrap.sql";

    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [MigrationId] = LoadSql()
    };

    private static string LoadSql()
    {
        using var stream = typeof(PostLoginPluginPersistenceMigrations).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded post-login migration resource '{ResourceName}'.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        return reader.ReadToEnd();
    }
}
