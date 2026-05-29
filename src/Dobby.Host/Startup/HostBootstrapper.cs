using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Diagnostics;
using Dobby.Host;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Extensions.Logging;
using System.IO;

namespace Dobby.Host.Startup;

public static class HostBootstrapper
{
    public static StartupPlan DescribeStartupPlan()
    {
        return new StartupPlan(
            [
                "single-instance",
                "configuration",
                "local-diagnostics",
                "login-window"
            ],
            [
                "shell-first-paint",
                "plugin-discovery",
                "plugin-validation",
                "plugin-activation"
            ]);
    }

    public static HostBootstrapResult Bootstrap(HostBootstrapOptions? options = null)
    {
        options ??= new HostBootstrapOptions();

        var correlationId = CorrelationId.New();
        var now = DateTimeOffset.UtcNow;
        var milestones = new List<StartupMilestone>();
        milestones.Add(new StartupMilestone(StartupMilestoneNames.HostBuilt, now, correlationId.ToString()));

        options.PreLoginInstrumentation?.RecordAllowedWorkloadBeforeLoginVisible("single-instance");
        var singleInstanceCoordinator = options.SingleInstanceCoordinatorFactory?.Invoke();
        if (singleInstanceCoordinator is not null)
        {
            var singleInstance = singleInstanceCoordinator.Acquire();
            if (!singleInstance.IsFirstInstance)
            {
                singleInstanceCoordinator.HandOffToFirstInstance();
                return new HostBootstrapResult(milestones);
            }
        }

        options.PreLoginInstrumentation?.RecordAllowedWorkloadBeforeLoginVisible("configuration");
        var configuration = HostConfigurationSources.Build(new HostConfigurationSourceOptions(
            options.PackagedAppSettingsPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            options.RuntimeConfigDirectory ?? DefaultRuntimeConfigDirectory(),
            options.EnvironmentName ?? "Production",
            options.EnvironmentVariables));

        var dobbyOptions = HostConfigurationSources.BindDobbyOptions(configuration);
        ValidateOptions(dobbyOptions);
        milestones.Add(new StartupMilestone(StartupMilestoneNames.ConfigLoaded, DateTimeOffset.UtcNow, correlationId.ToString()));

        options.PreLoginInstrumentation?.RecordAllowedWorkloadBeforeLoginVisible("local-diagnostics");
        using var diagnosticsLogger = LocalDiagnosticsConfigurator.CreateLogger(
            dobbyOptions.Runtime,
            dobbyOptions.Diagnostics,
            configuration);
        using var loggerFactory = new SerilogLoggerFactory(diagnosticsLogger, dispose: false);

        foreach (var sectionName in HostConfigurationSources.FindUnknownDobbySections(configuration))
        {
            diagnosticsLogger.Warning("Unknown Dobby configuration section ignored: {DobbySection}", sectionName);
        }

        var protectedMaterialProbe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(dobbyOptions.Runtime.DataRoot);
        new RuntimeFolderPreparer(
                options.ProtectedMaterialPrerequisiteAvailable ?? protectedMaterialProbe.IsAvailable,
                loggerFactory.CreateLogger<RuntimeFolderPreparer>())
            .Prepare(dobbyOptions.Runtime, correlationId);

        options.PreLoginInstrumentation?.RecordAllowedWorkloadBeforeLoginVisible("auth-session-storage-open");
        var persistenceOpened = options.PreLoginPersistenceOpen?.Invoke(dobbyOptions)
            ?? OpenPreLoginPersistence(dobbyOptions, protectedMaterialProbe);
        if (!persistenceOpened)
        {
            throw new InvalidOperationException("Pre-login persistence did not open; startup fails closed before LoginVisible.");
        }

        milestones.Add(new StartupMilestone(StartupMilestoneNames.PersistenceOpened, DateTimeOffset.UtcNow, correlationId.ToString()));
        options.PreLoginInstrumentation?.RecordPreAuthMetadataComplete();
        milestones.Add(new StartupMilestone(StartupMilestoneNames.LoginVisible, DateTimeOffset.UtcNow, correlationId.ToString()));

        return new HostBootstrapResult(milestones);
    }

    public static void RecordDiagnosticsExportCreated(IAuditTrail auditTrail, string packagePath)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        auditTrail.Append(new AuditEvent(
            AuditEventName.DiagnosticsExportCreated,
            "system",
            "diagnostics-export",
            AuditResult.Success,
            CorrelationId.New(),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>
            {
                ["packageName"] = Path.GetFileName(packagePath)
            }));
    }

    private static void ValidateOptions(DobbyOptions options)
    {
        var result = new DobbyOptionsValidator().Validate(Options.DefaultName, options);
        if (result.Failed)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(DobbyOptions), result.Failures);
        }
    }

    private static string DefaultRuntimeConfigDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Dobby", "config");
    }

    private static bool OpenPreLoginPersistence(
        DobbyOptions options,
        ProtectedMaterialAvailabilityProbe protectedMaterialProbe)
    {
        var opener = new PreLoginPersistenceOpener(protectedMaterialProbe);
        var persistenceOptions = new HostPersistenceOptions(
            options.Runtime.DataRoot,
            options.Storage.ProviderMode.ToString(),
            options.Storage.RequireDataAtRestProtection,
            options.Storage.BusyTimeoutMilliseconds);

        return opener.Open(persistenceOptions).IsSuccess;
    }
}
