using Microsoft.Extensions.Options;

namespace Dobby.Configuration;

public sealed class DobbyOptionsValidator : IValidateOptions<DobbyOptions>
{
    private static readonly int[] LoginThrottleFloor = [0, 0, 10, 30, 60];
    private static readonly HashSet<string> AllowedKdfModes = new(StringComparer.Ordinal)
    {
        "Argon2id",
        "Pbkdf2Sha256"
    };

    public ValidateOptionsResult Validate(string? name, DobbyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        ValidateRuntime(options.Runtime, failures);
        ValidateStorage(options.Runtime, options.Storage, failures);
        ValidateAuth(options.Auth, failures);
        ValidateSession(options.Session, failures);
        ValidateDiagnostics(options.Diagnostics, failures);
        ValidateAudit(options.Audit, failures);
        ValidateLocalization(options.Localization, failures);
        ValidatePlugins(options.Plugins, failures);
        ValidateLicensing(options.Licensing, failures);
        ValidateBranchShared(options.BranchShared, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRuntime(RuntimeOptions options, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.DataRoot))
        {
            failures.Add("Runtime.DataRoot is required.");
        }
        else
        {
            var expanded = Environment.ExpandEnvironmentVariables(options.DataRoot);
            if (expanded.StartsWith(@"\\", StringComparison.Ordinal) ||
                expanded.StartsWith("//", StringComparison.Ordinal))
            {
                failures.Add("Runtime.DataRoot must be a local path, not a network/share path.");
            }

            if (!Path.IsPathFullyQualified(expanded) && !IsProfileRelativePath(options.DataRoot))
            {
                failures.Add("Runtime.DataRoot must be an absolute or profile-relative local path.");
            }
        }

        if (!Enum.IsDefined(options.EnvironmentName))
        {
            failures.Add("Runtime.EnvironmentName must be Production, Development, or Test.");
        }
    }

    private static void ValidateStorage(RuntimeOptions runtimeOptions, StorageOptions options, List<string> failures)
    {
        if (!Enum.IsDefined(options.ProviderMode))
        {
            failures.Add("Storage.ProviderMode must be SQLite3MultipleCiphers or ProviderNeutralSkeleton.");
        }

        if (runtimeOptions.EnvironmentName == RuntimeEnvironmentName.Production &&
            options.ProviderMode != StorageProviderMode.SQLite3MultipleCiphers)
        {
            failures.Add("Storage.ProviderMode must be SQLite3MultipleCiphers in Production.");
        }

        if (!options.RequireDataAtRestProtection)
        {
            failures.Add("Storage.RequireDataAtRestProtection cannot be false.");
        }

        if (options.BusyTimeoutMilliseconds is < 100 or > 30000)
        {
            failures.Add("Storage.BusyTimeoutMilliseconds must be between 100 and 30000.");
        }
    }

    private static void ValidateAuth(AuthOptions options, List<string> failures)
    {
        if (!AllowedKdfModes.Contains(options.PasswordKdfMode))
        {
            failures.Add("Auth.PasswordKdfMode must be Argon2id or Pbkdf2Sha256; HumanApprovedPinRequired is a fail-closed stop condition after the recorded PBKDF2 selection.");
        }

        var throttleSteps = options.LoginThrottleStepsSeconds.Length == 0
            ? LoginThrottleFloor
            : options.LoginThrottleStepsSeconds;

        if (throttleSteps.Length != LoginThrottleFloor.Length)
        {
            failures.Add("Auth.LoginThrottleStepsSeconds must contain five steps.");
            return;
        }

        for (var i = 0; i < LoginThrottleFloor.Length; i++)
        {
            if (throttleSteps[i] < LoginThrottleFloor[i])
            {
                failures.Add("Auth.LoginThrottleStepsSeconds cannot be lowered below [0,0,10,30,60].");
                break;
            }
        }
    }

    private static void ValidateSession(SessionOptions options, List<string> failures)
    {
        if (options.AbsoluteTimeoutMinutes is < 15 or > 1440)
        {
            failures.Add("Session.AbsoluteTimeoutMinutes must be between 15 and 1440.");
        }

        if (options.IdleTimeoutMinutes is < 5 or > 480)
        {
            failures.Add("Session.IdleTimeoutMinutes must be between 5 and 480.");
        }
    }

    private static void ValidateDiagnostics(DiagnosticsOptions options, List<string> failures)
    {
        if (options.TechnicalLogRetentionDays < 30)
        {
            failures.Add("Diagnostics.TechnicalLogRetentionDays cannot be below 30 without an explicit documented support/privacy policy.");
        }

        if (options.TechnicalLogTotalSizeCapMegabytes is < 32 or > 2048)
        {
            failures.Add("Diagnostics.TechnicalLogTotalSizeCapMegabytes must be a finite cap between 32 and 2048.");
        }
    }

    private static void ValidateAudit(AuditOptions options, List<string> failures)
    {
        if (options.RetentionDays < 365)
        {
            failures.Add("Audit.RetentionDays cannot be below 365.");
        }
    }

    private static void ValidateLocalization(LocalizationOptions options, List<string> failures)
    {
        if (!options.DefaultCulture.Equals("az-AZ", StringComparison.Ordinal))
        {
            failures.Add("Localization.DefaultCulture must remain az-AZ unless ADR-0043 is amended.");
        }

        var supportedCultures = options.SupportedCultures.Length == 0
            ? new[] { "az-AZ", "en-US", "tr-TR" }
            : options.SupportedCultures;

        if (!supportedCultures.Contains("az-AZ", StringComparer.Ordinal))
        {
            failures.Add("Localization.SupportedCultures must include az-AZ.");
        }
    }

    private static void ValidatePlugins(PluginsOptions options, List<string> failures)
    {
        if (options.AllowPreLoginDiscovery)
        {
            failures.Add("Plugins.AllowPreLoginDiscovery cannot be true in P1.");
        }
    }

    private static void ValidateLicensing(LicensingOptions options, List<string> failures)
    {
        if (!options.ImportRequiresPermission)
        {
            failures.Add("Licensing.ImportRequiresPermission cannot be false.");
        }
    }

    private static void ValidateBranchShared(BranchSharedOptions options, List<string> failures)
    {
        if (options.RuntimeEnabled)
        {
            failures.Add("BranchShared.RuntimeEnabled must remain false in P1.");
        }
    }

    private static bool IsProfileRelativePath(string value)
    {
        return value.StartsWith("%LocalAppData%", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("%AppData%", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("%UserProfile%", StringComparison.OrdinalIgnoreCase);
    }
}
