using System.Reflection;
using System.Text;

namespace Dobby.Infrastructure.Persistence;

public static class PostLoginPluginPersistenceMigrations
{
    public const string MigrationId = "0002_postlogin_bootstrap";
    public const string LicenseStateMigrationId = "0003_license_state";
    public const string LicenseLifecycleInputsMigrationId = "0004_license_lifecycle_inputs";
    public const string PluginLifecycleStateMigrationId = "0005_plugin_lifecycle_state";
    public const string PluginStorageValuesMigrationId = "0006_plugin_storage_values";
    public const string LicenseV3StateMigrationId = "0007_license_v3_state";
    public const string PluginInstallOperationsMigrationId = "0008_plugin_install_operations";
    private const string BootstrapResourceName = "Dobby.Infrastructure.Persistence.Migrations.0002_postlogin_bootstrap.sql";
    private const string LicenseStateResourceName = "Dobby.Infrastructure.Persistence.Migrations.0003_license_state.sql";
    private const string LicenseLifecycleInputsResourceName = "Dobby.Infrastructure.Persistence.Migrations.0004_license_lifecycle_inputs.sql";
    private const string PluginLifecycleStateResourceName = "Dobby.Infrastructure.Persistence.Migrations.0005_plugin_lifecycle_state.sql";
    private const string PluginStorageValuesResourceName = "Dobby.Infrastructure.Persistence.Migrations.0006_plugin_storage_values.sql";
    private const string LicenseV3StateResourceName = "Dobby.Infrastructure.Persistence.Migrations.0007_license_v3_state.sql";
    private const string PluginInstallOperationsResourceName = "Dobby.Infrastructure.Persistence.Migrations.0008_plugin_install_operations.sql";

    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [MigrationId] = LoadSql(BootstrapResourceName),
        [LicenseStateMigrationId] = LoadSql(LicenseStateResourceName),
        [LicenseLifecycleInputsMigrationId] = LoadSql(LicenseLifecycleInputsResourceName),
        [PluginLifecycleStateMigrationId] = LoadSql(PluginLifecycleStateResourceName),
        [PluginStorageValuesMigrationId] = LoadSql(PluginStorageValuesResourceName),
        [LicenseV3StateMigrationId] = LoadSql(LicenseV3StateResourceName),
        [PluginInstallOperationsMigrationId] = LoadSql(PluginInstallOperationsResourceName)
    };

    private static string LoadSql(string resourceName)
    {
        using var stream = typeof(PostLoginPluginPersistenceMigrations).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded post-login migration resource '{resourceName}'.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        return reader.ReadToEnd();
    }
}
