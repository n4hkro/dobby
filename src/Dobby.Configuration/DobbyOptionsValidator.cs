using Microsoft.Extensions.Options;

namespace Dobby.Configuration;

public sealed class DobbyOptionsValidator : IValidateOptions<DobbyOptions>
{
    private static readonly int[] LoginThrottleFloor = [0, 0, 10, 30, 60];
    public static IReadOnlyList<string> SupportedPasswordKdfModes { get; } =
        Array.AsReadOnly(["Pbkdf2Sha256"]);
    private static readonly HashSet<string> AllowedKdfModes = new(
        SupportedPasswordKdfModes,
        StringComparer.Ordinal);

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
        ValidateProduct(options.Runtime, options.Product, failures);
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
            failures.Add("Auth.PasswordKdfMode must be Pbkdf2Sha256; Argon2id is not selectable until an approved implementation and parameter contract exist.");
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

        if (options.LoginThrottleFailureWindowSeconds is < 60 or > 86400)
        {
            failures.Add("Auth.LoginThrottleFailureWindowSeconds must be between 60 and 86400.");
        }

        if (options.LoginGlobalFailureThreshold is < 5 or > 100)
        {
            failures.Add("Auth.LoginGlobalFailureThreshold must be between 5 and 100.");
        }

        if (options.LoginGlobalBackoffSeconds is < 10 or > 3600)
        {
            failures.Add("Auth.LoginGlobalBackoffSeconds must be between 10 and 3600.");
        }

        if (options.MaxConcurrentPasswordVerifications is < 1 or > 8)
        {
            failures.Add("Auth.MaxConcurrentPasswordVerifications must be between 1 and 8.");
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
            failures.Add("Plugins.AllowPreLoginDiscovery cannot be true in Standalone mode.");
        }

        if (options.MaximumCompressedPackageMegabytes is < 1 or > 512 ||
            options.MaximumManifestKilobytes is < 1 or > 1024 ||
            options.MaximumEntryCount is < 1 or > 2048 ||
            options.MaximumEntryUncompressedMegabytes is < 1 or > 512 ||
            options.MaximumAggregateUncompressedMegabytes is < 1 or > 2048 ||
            options.MaximumAggregateUncompressedMegabytes < options.MaximumEntryUncompressedMegabytes ||
            options.MaximumExpansionRatio is < 1 or > 200)
        {
            failures.Add("Plugins package limits must be positive, internally consistent, and within hard safety ceilings.");
        }
    }

    private static void ValidateLicensing(LicensingOptions options, List<string> failures)
    {
        if (!options.ImportRequiresPermission)
        {
            failures.Add("Licensing.ImportRequiresPermission cannot be false.");
        }
    }

    private static void ValidateProduct(
        RuntimeOptions runtimeOptions,
        ProductOptions options,
        List<string> failures)
    {
        if (!Enum.IsDefined(options.ArtifactMode))
        {
            failures.Add("Product.ArtifactMode must be Installed or PortableSupport.");
        }

        var supportContact = options.SupportContact?.Trim() ?? string.Empty;
        var legalLine = options.LegalLine?.Trim() ?? string.Empty;
        if (runtimeOptions.EnvironmentName == RuntimeEnvironmentName.Production &&
            supportContact.Length == 0)
        {
            failures.Add("Product.SupportContact is required in Production.");
        }
        else if (supportContact.Length > 0 && !IsProductionSafeSupportContact(supportContact))
        {
            failures.Add("Product.SupportContact must be an absolute HTTPS URI without placeholder values.");
        }

        if (runtimeOptions.EnvironmentName == RuntimeEnvironmentName.Production &&
            legalLine.Length == 0)
        {
            failures.Add("Product.LegalLine is required in Production.");
        }
        else if (ContainsPlaceholder(legalLine))
        {
            failures.Add("Product.LegalLine cannot contain placeholder values.");
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

    private static bool IsProductionSafeSupportContact(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(uri.Host) &&
            !uri.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase) &&
            !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
            !uri.Host.Contains("example.", StringComparison.OrdinalIgnoreCase) &&
            !ContainsPlaceholder(value);
    }

    private static bool ContainsPlaceholder(string value)
    {
        return value.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("changeme", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("todo", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("{support", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("example.invalid", StringComparison.OrdinalIgnoreCase);
    }
}
