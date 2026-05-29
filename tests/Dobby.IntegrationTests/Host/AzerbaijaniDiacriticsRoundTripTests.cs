using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host.Settings;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Localization;
using Shouldly;
using System.Text;

namespace Dobby.IntegrationTests.Host;

public sealed class AzerbaijaniDiacriticsRoundTripTests
{
    [Fact]
    public void Azerbaijani_diacritics_round_trip_across_ui_storage_and_audit_supported_paths()
    {
        using var fixture = SettingsPreferenceFixture.Create();
        var source = new HostConfigurationSettingsPreferenceSource(
            fixture.Executor,
            SettingsPreferenceScope.ForAuthenticatedUser("əli"));
        var displayValue = "Şəki üzrə əməliyyat";

        source.Save(SettingsPreferences.Default with
        {
            ShellView = SettingsPreferences.Default.ShellView with
            {
                SidebarOpenNodeIds = [displayValue],
                LastActiveModuleRouteKey = RouteKey.Normalize("Dobby.Shell.Navigation.Home")
            }
        }).IsSuccess.ShouldBeTrue();

        var reloaded = source.Load().Value.ShellView;
        reloaded.SidebarOpenNodeIds.Single().ShouldBe(displayValue);
        new HostStringCatalog().GetString(HostResourceKeys.ShellDiagnosticsExport)
            .ShouldContain("İxrac", Case.Sensitive);

        var audit = new InMemoryAuditTrail();
        audit.Append(new AuditEvent(
            "shell.diacritics.roundtrip",
            "əli",
            "shell",
            AuditResult.Success,
            CorrelationId.New(),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["display"] = displayValue
            }));

        audit.Snapshot().Single().Details!["display"].ShouldBe(displayValue);
    }

    private sealed class SettingsPreferenceFixture : IDisposable
    {
        private SettingsPreferenceFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
        }

        public string Root { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public static SettingsPreferenceFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-diacritics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new SettingsPreferenceFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}

internal static class RouteKey
{
    public static string Normalize(string value)
    {
        return value.Normalize(NormalizationForm.FormC);
    }
}
