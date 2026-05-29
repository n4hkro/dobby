using Dobby.Diagnostics;
using Dobby.Host.Startup;
using Shouldly;

namespace Dobby.IntegrationTests.Startup;

public sealed class PreLoginAllowedWorkloadTests
{
    [Fact]
    public void Startup_plan_keeps_heavy_workloads_after_login_visible()
    {
        var plan = HostBootstrapper.DescribeStartupPlan();

        plan.PreLoginSteps.ShouldBe(["single-instance", "configuration", "local-diagnostics", "login-window"]);
        plan.PreLoginSteps.ShouldNotContain("plugin-discovery");
        plan.PreLoginSteps.ShouldNotContain("online-check");
        plan.PreLoginSteps.ShouldNotContain("update-check");
        plan.PreLoginSteps.ShouldNotContain("import-export");
        plan.PreLoginSteps.ShouldNotContain("heavy-migration");
        plan.PreLoginSteps.ShouldNotContain("hosted-service-start");
        plan.PostLoginSteps.ShouldContain("plugin-discovery");
        plan.PostLoginSteps.ShouldContain("plugin-validation");
        plan.PostLoginSteps.ShouldContain("plugin-activation");
    }

    [Fact]
    public void Bootstrap_instrumentation_proves_forbidden_workloads_do_not_run_before_login_visible()
    {
        using var temp = new TemporaryDirectory();
        var runtimeRoot = Path.Combine(temp.Path, "runtime-root");
        var appsettings = Path.Combine(temp.Path, "appsettings.json");
        WriteBootstrapAppSettings(appsettings, runtimeRoot);
        var instrumentation = new PreLoginInstrumentation();

        var result = HostBootstrapper.Bootstrap(new HostBootstrapOptions(
            ShowMainWindow: false,
            PackagedAppSettingsPath: appsettings,
            RuntimeConfigDirectory: Path.Combine(temp.Path, "config"),
            EnvironmentName: "Test",
            PreLoginInstrumentation: instrumentation));

        result.Milestones.Select(milestone => milestone.Name).ShouldContain(StartupMilestoneNames.LoginVisible);
        instrumentation.AllowedWorkloadsBeforeLoginVisible.ShouldBe([
            "single-instance",
            "configuration",
            "local-diagnostics",
            "auth-session-storage-open",
            "pre-auth-metadata"
        ]);
        instrumentation.ForbiddenWorkloadsBeforeLoginVisible.ShouldBeEmpty();

        foreach (var forbiddenWorkload in PreLoginInstrumentation.ForbiddenWorkloadNames)
        {
            instrumentation.AllowedWorkloadsBeforeLoginVisible.ShouldNotContain(forbiddenWorkload);
        }
    }

    [Fact]
    public void Instrumentation_flags_forbidden_pre_login_workload_attempts()
    {
        var instrumentation = new PreLoginInstrumentation();

        instrumentation.RecordAllowedWorkloadBeforeLoginVisible("plugin-discovery");
        instrumentation.RecordForbiddenWorkloadBeforeLoginVisible("hosted-service-start");

        instrumentation.ForbiddenWorkloadsBeforeLoginVisible.ShouldBe(["plugin-discovery", "hosted-service-start"]);
    }

    private static void WriteBootstrapAppSettings(string appsettingsPath, string runtimeRoot)
    {
        File.WriteAllText(appsettingsPath, $$"""
            {
              "Dobby": {
                "Runtime": { "DataRoot": "{{runtimeRoot.Replace("\\", "\\\\")}}", "EnvironmentName": "Test" },
                "Storage": { "ProviderMode": "SQLite3MultipleCiphers", "RequireDataAtRestProtection": true, "BusyTimeoutMilliseconds": 5000 },
                "Auth": { "LoginThrottleStepsSeconds": [0, 0, 10, 30, 60], "PasswordKdfMode": "Pbkdf2Sha256" },
                "Session": { "AbsoluteTimeoutMinutes": 480, "IdleTimeoutMinutes": 30, "AutoLockEnabledByDefault": false },
                "Diagnostics": { "TechnicalLogRetentionDays": 30, "TechnicalLogTotalSizeCapMegabytes": 256 },
                "Audit": { "RetentionDays": 365 },
                "Localization": { "DefaultCulture": "az-AZ", "SupportedCultures": ["az-AZ", "en-US", "tr-TR"] },
                "Plugins": { "AllowPreLoginDiscovery": false },
                "Licensing": { "ImportRequiresPermission": true },
                "BranchShared": { "RuntimeEnabled": false }
              },
              "Serilog": { "MinimumLevel": "Information" }
            }
            """);
    }
}
