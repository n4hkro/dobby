using System.Globalization;
using System.Collections;
using System.Reflection;
using System.Resources;
using Microsoft.Extensions.Localization;

namespace Dobby.Localization;

public sealed class HostStringCatalog(
    IHostStringLocalizerAdapter? localizerAdapter = null,
    Action<string>? missingKeyRecorder = null)
{
    private static readonly IReadOnlyDictionary<string, string> AzAzStrings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [HostResourceKeys.Home] = "Ana səhifə",
        [HostResourceKeys.Plugins] = "Pluginlər",
        [HostResourceKeys.Settings] = "Tənzimləmələr",
        [HostResourceKeys.HomeTitle] = "Ana səhifə",
        [HostResourceKeys.PluginsTitle] = "Pluginlər",
        [HostResourceKeys.SettingsTitle] = "Tənzimləmələr",
        [HostResourceKeys.LoginTitle] = "Daxil ol",
        [HostResourceKeys.LoginUsername] = "İstifadəçi adı",
        [HostResourceKeys.LoginDisplayName] = "Göstərilən ad",
        [HostResourceKeys.LoginPassword] = "Şifrə",
        [HostResourceKeys.LoginRememberMe] = "Məni yadda saxla",
        [HostResourceKeys.LoginSubmit] = "Daxil ol",
        [HostResourceKeys.LoginThrottleCountdown] = "Yenidən cəhd üçün gözləyin",
        [HostResourceKeys.LoginRecoveryExport] = "Bərpa sorğusu ixrac et",
        [HostResourceKeys.LoginRecoveryImport] = "Bərpa cavabı idxal et",
        [HostResourceKeys.LoginInvalidCredentials] = "İstifadəçi adı və ya şifrə yanlışdır",
        [HostResourceKeys.LoginDeactivated] = "Bu istifadəçi aktiv deyil",
        [HostResourceKeys.LoginGenericFailure] = "Daxil olmaq mümkün olmadı",
        [HostResourceKeys.LockTitle] = "Kilidlənib",
        [HostResourceKeys.LockUnlock] = "Kilidi aç",
        [HostResourceKeys.LockLogout] = "Çıxış",
        [HostResourceKeys.RecoveryTitle] = "Owner bərpası",
        [HostResourceKeys.RecoveryResetTitle] = "Owner şifrəsini sıfırla",
        [HostResourceKeys.RecoveryResetNewPassword] = "Yeni şifrə",
        [HostResourceKeys.RecoveryResetConfirm] = "Təsdiqlə",
        [HostResourceKeys.RecoveryResetCancel] = "Ləğv et",
        [HostResourceKeys.RecoveryOpenFolder] = "Qovluğu aç",
        [HostResourceKeys.RecoveryCopyPath] = "Yolu kopyala",
        [HostResourceKeys.RecoveryExportSuccess] = "Bərpa sorğusu yaradıldı",
        [HostResourceKeys.RecoveryImportSuccess] = "Bərpa tamamlandı",
        [HostResourceKeys.RecoveryImportSignatureFailed] = "Bərpa imzası doğrulanmadı",
        [HostResourceKeys.RecoveryImportExpired] = "Bərpa cavabının vaxtı bitib",
        [HostResourceKeys.RecoveryImportReplay] = "Bərpa cavabı artıq istifadə edilib",
        [HostResourceKeys.RecoveryImportDeviceMismatch] = "Bu bərpa cavabı bu cihaz üçün deyil",
        [HostResourceKeys.RecoveryImportOwnerMissing] = "Owner tapılmadı",
        [HostResourceKeys.RecoveryImportCancelled] = "Bərpa ləğv edildi",
        [HostResourceKeys.SettingsAppearance] = "Görünüş",
        [HostResourceKeys.SettingsAccount] = "Hesab",
        [HostResourceKeys.SettingsSecuritySession] = "Təhlükəsizlik və sessiya",
        [HostResourceKeys.SettingsUserManagement] = "İstifadəçilər",
        [HostResourceKeys.SettingsAbout] = "Haqqında",
        [HostResourceKeys.UserManagementPermissionDenied] = "Bu əməliyyat üçün icazə yoxdur",
        [HostResourceKeys.AboutOwnerStatusRegistered] = "Owner qeydiyyatdadır",
        [HostResourceKeys.ShellBrand] = "Dobby",
        [HostResourceKeys.ShellSidebarToggle] = "Yan paneli dəyiş",
        [HostResourceKeys.ShellThemeToggle] = "Mövzunu dəyiş",
        [HostResourceKeys.ShellStatusReady] = "Hazırdır",
        [HostResourceKeys.ShellWorkSessions] = "İş sessiyaları",
        [HostResourceKeys.ShellWorkSessionNavigationWarning] = "Açıq iş sessiyası var",
        [HostResourceKeys.ShellWorkSessionStay] = "Qal",
        [HostResourceKeys.ShellWorkSessionMinimize] = "Yığ",
        [HostResourceKeys.ShellWorkSessionClose] = "Bağla",
        [HostResourceKeys.ShellMinimizedWorkStrip] = "Yığılmış işlər",
        [HostResourceKeys.ShellThirdBar] = "Üçüncü panel",
        [HostResourceKeys.ShellWorkspaceNewTab] = "Yeni Home tab",
        [HostResourceKeys.ShellWorkspaceCloseTab] = "Tabı bağla",
        [HostResourceKeys.ShellWindowMinimize] = "Pəncərəni yığ",
        [HostResourceKeys.ShellWindowMaximize] = "Pəncərəni böyüt",
        [HostResourceKeys.ShellWindowRestore] = "Pəncərəni bərpa et",
        [HostResourceKeys.ShellWindowClose] = "Tətbiqi bağla",
        [HostResourceKeys.ShellWindowCloseConfirmationTitle] = "Tətbiqi bağla",
        [HostResourceKeys.ShellWindowCloseConfirmationMessage] = "Dobby tətbiqini bağlamaq istəyirsiniz?",
        [HostResourceKeys.ShellSettingsHeading] = "Tənzimləmələr",
        [HostResourceKeys.ShellAboutHeading] = "Haqqında",
        [HostResourceKeys.ShellBackgroundOperation] = "Fon əməliyyatı",
        [HostResourceKeys.ShellDashboardCasesOverview] = "Cases Overview",
        [HostResourceKeys.ShellDashboardPipelines] = "Pipelines",
        [HostResourceKeys.ShellDashboardInProgress] = "Davam edir",
        [HostResourceKeys.ShellDashboardCompleted] = "Tamamlandı",
        [HostResourceKeys.ShellDashboardExpired] = "Vaxtı keçib",
        [HostResourceKeys.ShellDashboardNewCases] = "Yeni işlər",
        [HostResourceKeys.ShellDashboardPendingInfo] = "Məlumat gözləyir",
        [HostResourceKeys.ShellDashboardClosed] = "Bağlı",
        [HostResourceKeys.ShellDashboardTopDrivers] = "Top iş səbəbləri",
        [HostResourceKeys.ShellDashboardCaseMetrics] = "İş metrikaları",
        [HostResourceKeys.ShellDiagnosticsExport] = "İxrac et",
        [HostResourceKeys.ShellDiagnosticsExported] = "Diaqnostika ixrac edildi",
        [HostResourceKeys.PluginsManagerHeading] = "Pluginlər",
        [HostResourceKeys.PluginsManagerInstall] = "Plugin quraşdır",
        [HostResourceKeys.PluginsManagerEnable] = "Aktiv et",
        [HostResourceKeys.PluginsManagerDisable] = "Deaktiv et",
        [HostResourceKeys.PluginsManagerUninstall] = "Sil",
        [HostResourceKeys.PluginsManagerEmpty] = "Quraşdırılmış plugin yoxdur",
        [HostResourceKeys.PluginsManagerRestartRequired] = "Yenidən başlatma tələb olunur",
        [HostResourceKeys.PluginsManagerInstallBusy] = "Plugin quraşdırılır",
        [HostResourceKeys.PluginsManagerEnableBusy] = "Plugin aktiv edilir",
        [HostResourceKeys.PluginsManagerDisableBusy] = "Plugin deaktiv edilir",
        [HostResourceKeys.PluginsManagerUninstallBusy] = "Plugin silinir",
        [HostResourceKeys.PluginsManagerOperationDenied] = "Bu plugin əməliyyatı üçün icazə yoxdur",
        [HostResourceKeys.PluginsManagerUninstallConfirmationTitle] = "Plugini sil",
        [HostResourceKeys.PluginsManagerUninstallConfirmationMessage] = "{0} pluginini silmək istəyirsiniz? Bəli plugin datasını silir, Xeyr plugin datasını saxlayır, Ləğv et əməliyyatı dayandırır.",
        [HostResourceKeys.LicenseHeading] = "Plugin lisenziyası",
        [HostResourceKeys.LicenseStatusLabel] = "Status",
        [HostResourceKeys.LicenseListLabel] = "Lisenziyalar",
        [HostResourceKeys.LicenseDetailLabel] = "Detallar",
        [HostResourceKeys.LicenseNoPlugin] = "Quraşdırılmış plugin yoxdur",
        [HostResourceKeys.LicenseImportAction] = "Lisenziya idxal et",
        [HostResourceKeys.LicenseImportSuccess] = "Lisenziya idxal edildi",
        [HostResourceKeys.LicenseImportRejected] = "Lisenziya idxalı qəbul edilmədi",
        [HostResourceKeys.LicenseImportBusy] = "Lisenziya idxal olunur",
        [HostResourceKeys.LicenseImportPermissionDenied] = "Lisenziya idxalı üçün icazə yoxdur",
        [HostResourceKeys.LicenseModeFullAccess] = "Tam giriş",
        [HostResourceKeys.LicenseModeReadOnlyExpired] = "Yalnız oxuma",
        [HostResourceKeys.LicenseModeDenied] = "Bloklanıb",
        [HostResourceKeys.ShellPasswordChanged] = "Şifrə dəyişdirildi",
        [HostResourceKeys.ShellProfileAccountInfo] = "Profil və hesab məlumatı",
        [HostResourceKeys.ShellProfileChangePassword] = "Şifrəni dəyiş",
        [HostResourceKeys.ShellProfileManualLock] = "Kilidlə",
        [HostResourceKeys.ShellProfileLogout] = "Çıxış",
        [HostResourceKeys.ShellFeedbackLoading] = "Yüklənir",
        [HostResourceKeys.ShellFeedbackEmpty] = "Məlumat yoxdur",
        [HostResourceKeys.ShellFeedbackSuccess] = "Uğurlu",
        [HostResourceKeys.ShellFeedbackError] = "Xəta",
        [HostResourceKeys.ShellFeedbackConfirmation] = "Təsdiq",
        [HostResourceKeys.ShellNotificationToast] = "Qısa bildiriş",
        [HostResourceKeys.ShellNotificationBanner] = "Banner bildirişi",
        [HostResourceKeys.ShellNotificationModal] = "Modal bildiriş",
        [HostResourceKeys.AboutAppVersion] = "Tətbiq versiyası",
        [HostResourceKeys.AboutBuildNumber] = "Yığma nömrəsi",
        [HostResourceKeys.AboutBuildDate] = "Yığma tarixi",
        [HostResourceKeys.AboutHostEdition] = "Host buraxılışı",
        [HostResourceKeys.AboutHostChannel] = "Kanal",
        [HostResourceKeys.AboutDeviceCode] = "Cihaz kodu",
        [HostResourceKeys.AboutPlugins] = "Pluginlər",
        [HostResourceKeys.AboutPluginsEmpty] = "Quraşdırılmış plugin yoxdur",
        [HostResourceKeys.AboutSupportContact] = "Dəstək əlaqəsi",
        [HostResourceKeys.AboutLegalLine] = "Hüquqi qeyd",
        [HostResourceKeys.AboutOwnerIdentity] = "Owner statusu",
        [HostResourceKeys.AuditRetentionSweep] = "Audit saxlanma təmizliyi"
    };

    public CultureInfo DefaultCulture { get; } = CultureInfo.GetCultureInfo("az-AZ");

    public static HostStringCatalog CreateResourceBacked(Action<string>? missingKeyRecorder = null)
    {
        return new HostStringCatalog(
            new MicrosoftHostStringLocalizerAdapter(new ResourceHostStringLocalizer()),
            missingKeyRecorder);
    }

    public string GetString(string key, CultureInfo? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        culture ??= DefaultCulture;

        var localized = localizerAdapter?.GetString(key, culture);
        if (localized is { ResourceNotFound: false } && !string.IsNullOrEmpty(localized.Value))
        {
            return localized.Value;
        }

        if (culture.Name.Equals("az-AZ", StringComparison.OrdinalIgnoreCase) &&
            AzAzStrings.TryGetValue(key, out var value))
        {
            return value;
        }

        missingKeyRecorder?.Invoke(key);
        return $"[[{key}]]";
    }
}

public interface IHostStringLocalizerAdapter
{
    LocalizedHostString GetString(string key, CultureInfo culture);
}

public sealed record LocalizedHostString(string Value, bool ResourceNotFound);

public sealed class MicrosoftHostStringLocalizerAdapter(IStringLocalizer<HostStringCatalog> localizer)
    : IHostStringLocalizerAdapter
{
    public LocalizedHostString GetString(string key, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(culture);

        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = culture;
            var localized = localizer[key];
            return new LocalizedHostString(localized.Value, localized.ResourceNotFound);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}

internal sealed class ResourceHostStringLocalizer : IStringLocalizer<HostStringCatalog>
{
    public LocalizedString this[string name] => GetString(name, []);

    public LocalizedString this[string name, params object[] arguments] => GetString(name, arguments);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return HostResourceKeys.Sprint7VisibleKeys
            .Select(key => this[key]);
    }

    private static LocalizedString GetString(string name, object[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var value = FindString(name, CultureInfo.CurrentUICulture);
        if (value is null)
        {
            return new LocalizedString(name, name, resourceNotFound: true);
        }

        if (arguments.Length > 0)
        {
            value = string.Format(CultureInfo.CurrentUICulture, value, arguments);
        }

        return new LocalizedString(name, value, resourceNotFound: false);
    }

    private static string? FindString(string name, CultureInfo culture)
    {
        foreach (var candidate in CandidateCultures(culture))
        {
            var resourceName = $"Dobby.Localization.Resources.Host.{candidate.Name}.resources";
            Assembly satellite;
            try
            {
                satellite = typeof(HostStringCatalog).Assembly.GetSatelliteAssembly(candidate);
            }
            catch (FileNotFoundException)
            {
                continue;
            }

            using var stream = satellite.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }

            using var reader = new ResourceReader(stream);
            foreach (DictionaryEntry entry in reader)
            {
                if (entry.Key is string key &&
                    key.Equals(name, StringComparison.Ordinal) &&
                    entry.Value is string value)
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static IEnumerable<CultureInfo> CandidateCultures(CultureInfo culture)
    {
        if (!string.IsNullOrWhiteSpace(culture.Name))
        {
            yield return culture;
        }

        if (!culture.Name.Equals("az-AZ", StringComparison.OrdinalIgnoreCase))
        {
            yield return CultureInfo.GetCultureInfo("az-AZ");
        }
    }
}
