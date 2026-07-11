# Qalan Proqram Hazırlığı İşləri

Bu fayl əvvəlki 158 maddəlik auditdən qalan işlərin işçi statusudur. Sətir nömrələri plan/review istinadları üçün sabit saxlanılır; bu icra dalında bağlanan sətirlər `İCRA OLUNUB` kimi qalır və silinmir.

Status mənası:

- `İCRA OLUNUB`: bu icra dalında davranış production kodu və yerli yoxlama sübutu ilə bağlanıb; sıra nömrələrini plan/review istinadları üçün sabit saxlamaq məqsədilə sətir silinmir.
- `QİSMƏN`: əsas hissələrdən bəziləri var, amma UI, wiring, runtime enforcement, final artefakt və ya davranış yarımçıqdır.
- `İCRA OLUNMAYIB`: production implementasiya tapılmadı və ya yalnız plan/test səviyyəsindədir.

## Qalan Maddələr

| № | Status | İş | Sübut və boşluq |
|---:|---|---|---|
| 1 | İCRA OLUNUB | Plugin-visible storage contract-ları və scope-lar. | `IPluginStorage`, `IPluginStorageCompartment`, SDK JSON extension-ları, host-owned `HostPluginStorage` backend-i, append-only `0006_plugin_storage_values` migration-u və authenticated `PluginHostServices` wiring-i mövcuddur; `HostPluginStorageTests`, `PluginStorageContractTests` və persistence migration testləri keçib. |
| 2 | QİSMƏN | Host runtime qovluqları LocalAppData altında ayrılmalıdır. | `RuntimeFolderLayout` və `RuntimeFolderPreparer` var; amma siyahıda ayrıca `staging` və `diagnostics` yoxdur, implementasiya `data/config/logs/plugins/licenses/recovery/backup/temp` ilə fərqlənir. |
| 3 | QİSMƏN | Append-only audit boundary, taxonomy, severity, retention. | `AuditEvent`, `AuditEventName`, `DurableAuditTrail`, `AuditRetentionSweeper` var; production severity modeli/sahəsi tapılmadı. |
| 4 | QİSMƏN | Database WAL, journal, temp və backup davranışları. | WAL və temp guardrail-ləri var; backup yalnız folder səviyyəsindədir, real backup/checkpoint davranışı tapılmadı. |
| 5 | QİSMƏN | Tətbiq login-first açılmalıdır. | `App.xaml.cs` əvvəl `LoginWindow` göstərir; amma `LoginRuntimePreparer` post-login runtime taskını autentikasiyadan əvvəl hazırlaya bilir və bəzi background warmup/audit işləri sərhədi zəiflədir. |
| 6 | QİSMƏN | Display name validation. | `DisplayNameValidator` var, amma əsasən trim/null normalization edir; ciddi validation qaydaları yoxdur. |
| 7 | QİSMƏN | Password policy validation. | `PasswordPolicyValidator` var, amma nonblank və max-length səviyyəsindədir; daha geniş policy yoxdur. |
| 8 | QİSMƏN | KDF policy resolver və password hashing floors. | `KdfPolicyResolver` və PBKDF2 hasher var; config validator `Argon2id`-ə icazə verir, resolver onu implement etmir. |
| 9 | İCRA OLUNUB | Lock pəncərəsi və Lock ViewModel. | `LockWindow.xaml`, production `LockViewModel`/`LockViewModelActions` və `LockRuntimeWorkflow` birgə unlock/logout feedback yolunu təmin edir; `LockViewModelTests` və session runtime testləri guardrail kimi qalır. |
| 10 | İCRA OLUNUB | Password change flow. | `SelfServicePasswordChangeCoordinator`, `AccountSettingsModel.ChangePassword`, profile `ChangePasswordButton` və settings `SettingsChangePasswordButton` eyni localized password-change action yoluna bağlıdır; unattached fallback fail-closed və action-availability static testləri keçib. |
| 11 | QİSMƏN | Pre-login instrumentation. | `LoginStartupTimingTrace` və `PreLoginInstrumentation` var; WPF startup path-da workload instrumentation tam bağlanmayıb. |
| 12 | İCRA OLUNUB | User Management ekranı. | `MainWindow.xaml` `UserManagementSettingsSurface` və `UserManagementActionList` render edir; `ShellViewModel` icazəsiz istifadəçilər üçün category/surface/action list-i non-disclosing gizlədir, owner service protections backend-də qalır. |
| 13 | İCRA OLUNUB | Settings navigation tree. | `SettingsNavigationModel`, `ShellViewModel.SetSettingsAndAbout` və `MainWindow.xaml` `SettingsCategoryList` Appearance/Account/Security/User Management/About tree-sini permission-gated render edir; snapshot/static/unit testləri bunu tələb edir. |
| 14 | İCRA OLUNUB | Appearance settings. | `AppearanceSettingsModel`, `AppearanceSettingsSurface`, `AppearanceThemeSelector` və toolbar `ThemeToggle_Click` eyni persisted theme preference yolunu istifadə edir; `ShellThemeAndSettingsIntegrationTests` sync-i yoxlayır. |
| 15 | İCRA OLUNUB | Account settings. | `AccountSettingsSurface` account/profile text və `SettingsChangePasswordButton` self-service password-change action-ını göstərir; lock/logout ayrıca profile/session menu row 20-də bağlanıb. |
| 16 | İCRA OLUNUB | Security/Session settings. | `SecuritySessionSettingsSurface` auto-lock toggle/minute input/save action-ını render edir; `SecuritySessionSettingsModel` preference-i persist edir və authenticated session runtime saved user preference-i consume edir. |
| 17 | İCRA OLUNUB | About info provider və About ekranı. | `AboutInfoProvider`, `AboutSettingsModel`, `BuildAboutFields` və `AboutSettingsSurface` version/build/channel/device/plugins/support/legal/owner sahələrini render edir; About altında license import/status management göstərilmir. |
| 18 | İCRA OLUNUB | Settings, About və User Management lokalizasiya açarları. | `HostResourceKeys`, `HostStringCatalog`, `Host.az-AZ.resx`, `Host.en-US.resx` və `Host.tr-TR.resx` Settings/About/User Management/profile action açarlarının eyni key setini saxlayır; parity və non-Azerbaijani visible-string testləri keçib. |
| 19 | İCRA OLUNUB | MainWindow layout: Homebar/TopToolbar, Sidebar, Third Bar, Main Content, status/progress, notification/feedback, minimized strip. | `MainWindow.xaml` production `TopToolbar`, `Sidebar`, `ThirdBar`, `MainContent`, `ShellStatusBar`, `ShellNotificationHost`, `ShellFeedbackHost`, `MinimizedWorkSessionStrip` səthlərini render edir; `ShellSnapshotTests`, `ShellUiStaticScanTests`, `ShellKeyboardSmokeTests` və `ShellLayoutStateTests` guardrail-ləri keçib. |
| 20 | İCRA OLUNUB | Profile/session menu. | `ProfileMenu` XAML və `ChangePassword_Click`, `ManualLock_Click`, `Logout_Click`, `DiagnosticsExport_Click` handler-ləri backend session/auth/diagnostics yollarına bağlıdır; busy/denied/error/success feedback açarları və smoke/integration testləri var. |
| 21 | İCRA OLUNUB | ShellRouteHost. | `Navigation_Click`, workspace tab restore/close və pending work-session continuation `ShellRouteHost.ActivateOrHomeAsync(...)` yolundan keçir; unknown/stale route fallback Home regressiyaları `ShellViewModelNavigationTests` və `ShellViewModelWorkspaceTabTests` ilə örtülüb. |
| 22 | İCRA OLUNUB | Sidebar state. | `SidebarToggle_Click`, `SidebarNodeToggle_Click`, `ShellLayoutPreference.SidebarCollapsed/OpenNodeIds` və preference export/persist guardrail-ləri production XAML ilə bağlıdır; collapse/open-node testləri keçib. |
| 23 | İCRA OLUNUB | Third Bar state. | `ThirdBar` və `ThirdBarNavigationList` render olunur, `ThirdBarRouteOrder` preference-i navigation composer/viewmodel testləri ilə təsdiqlənir. |
| 24 | İCRA OLUNUB | Homebar/TopToolbar state. | `TopToolbar`/`CenteredTopToolbar` built-in və plugin ordering/hidden preference-lərini render edir; `TopToolbarOrder`/hidden-state unit və static/snapshot testləri keçib. |
| 25 | İCRA OLUNUB | Main Content host. | `MainContent` production hostu built-in settings/about/dashboard və `PluginPageHost` `ContentControl` səthini ehtiva edir; static və snapshot testləri `MainContent`/`PluginPageHost` renderini tələb edir. |
| 26 | İCRA OLUNUB | Bottom status və progress surface. | `ShellStatusBar` `StatusText`, background operation owner/status/progress binding-ləri ilə görünür; `ShellBackgroundOperationCoordinator` və snapshot/static yoxlamaları status/progress səthini örtür. |
| 27 | İCRA OLUNUB | Notification və feedback state-ləri. | `ShellNotificationHost` və `ShellFeedbackHost` localized notification/feedback state-lərini render edir, profile/diagnostics action feedback paths və keyboard smoke testləri ilə reachable/non-overlap sübutu var. |
| 28 | İCRA OLUNUB | Minimized work-session strip. | `MinimizedWorkSessionStrip` minimized session metadata və restore action-larını render edir; restore label localization, focus traversal və smoke/static guardrail-ləri keçib. |
| 29 | İCRA OLUNUB | Appearance Settings ilə toolbar theme switch sync. | `ThemeToggle_Click`, `AppearanceThemeSelector_SelectionChanged`, `ApplyThemePreference`, `AppearanceSettingsModel` və `StartupThemeSnapshotStore` eyni persisted theme state-i yeniləyir; toolbar/settings sync integration testləri keçib. |
| 30 | İCRA OLUNUB | Lokalizasiya sistemi. | `az-AZ` default catalog, missing-key diagnostics, `Host.en-US.resx` və `Host.tr-TR.resx` eyni resource key seti ilə mövcuddur; visible English/Turkish strings Azerbaijani fallback-ları reuse etmir. |
| 31 | QİSMƏN | Lock/logout/plugin lifecycle zamanı work session davranışı. | `RequestLogout`, `RequestPluginDisable`, `RequestPluginUninstall` və `HandleLock` snapshot qoruma davranışı var; plugin lifecycle action UI/flow-ları növbəti plugin-management scope-larında tam bağlanmalıdır. |
| 32 | İCRA OLUNUB | Authenticated shell daxilindən diagnostics export action. | `DiagnosticsExportButton` authenticated profile menu-dadır, `DiagnosticsExport_Click` `ShellDiagnosticsExportCoordinator`/diagnostics package builder yolunu çağırır, `diagnostics.export.created` audit semantics və no-upload behavior `ShellDiagnosticsExportActionTests` ilə təsdiqlənir. |
| 33 | İCRA OLUNUB | Plugin page render registry. | Production `MainWindow` `PluginPageRenderRegistry.CreateDefault()` və konkret `PluginPageHost` istifadə edir; yalnız host-owned `PluginTextPageModel`/`PluginActionPageModel` render olunur, unknown model safe unavailable fallback-a düşür və action fault-ları host diagnostics sərhədinə ötürülür. |
| 34 | İCRA OLUNUB | Plugin page-lərin Main Content daxilində göstərilməsi. | `Navigation_Click` `ShellRouteHost`/`PluginPageRouteCoordinator` yolundan keçir; load-before-activate, active/deactivated lifecycle phase, unload-failure same-route reactivation, digər route retry, tab/route rollback və cleanup davranışları unit/integration testləri ilə örtülüb. |
| 35 | İCRA OLUNUB | Plugin background operation snapshot-ları shell status/progress səthinə ötürülməlidir. | `BackgroundOperationOwnerKind.Plugin` running/intermediate/completed snapshot-ları host əməliyyatları ilə eyni `ShellBackgroundOperationCoordinator` və `ShellStatusBar` status/progress səthindən keçir; ayrıca plugin progress kanalı yaradılmayıb. |
| 36 | İCRA OLUNUB | Plugin exception boundary. | Activation hook, page load/activate/deactivate/unload, page action və plugin-scoped event handler fault-ları host-owned boundary-lərlə safe `ErrorCode`-lara map olunur; host event handler fault-ları propagate edir, plugin fault-ları isə isolate/attribute olunur. Persistence failure real runtime registration/subscription rollback-u edir və tracker yalnız atomic status commit-dən sonra işarələnir. |
| 37 | İCRA OLUNUB | Plugin fault state-ləri user-facing/non-disclosing göstərilməlidir. | `Faulted`/`Rejected`/`Skipped` safe status summary-ləri authoritative state və metadata cache-də atomik persist olunur və manager surface-də yenidən oxunur; diagnostics redacted file sink-ə yazılır, raw exception message/stack trace user surface-ə çıxmır. |
| 38 | QİSMƏN | Customer/deployment context. | `CurrentCustomerDeploymentContextService`, `HostCustomerDeploymentContextStore` var; yalnız `default/default`, real provisioning yoxdur. |
| 39 | QİSMƏN | `Plugins > License` import ekranı və coordinator. | Coordinator, VM surface və file picker var; `MainWindow.xaml`-də License UI/import click handler yoxdur. |
| 40 | QİSMƏN | License file picker. | `WindowsLicenseFilePicker.PickLicenseFile()` var; production UI-dan çağırılmır. |
| 41 | İCRA OLUNMAYIB | ReadOnlyExpired mutation əməliyyatları bloklanmalıdır. | `OperationAuthorizationPolicy` license mode-a baxmır; ümumi mutation enforcement boundary tapılmadı. |
| 42 | İCRA OLUNMAYIB | ReadOnlyExpired plugin storage write bloklanmalıdır. | Yalnız storage contracts var; storage backend və mode-aware write wrapper yoxdur. |
| 43 | İCRA OLUNMAYIB | ReadOnlyExpired background task start bloklanmalıdır. | Background operation reporter var, amma start API/enforcement yoxdur. |
| 44 | QİSMƏN | ReadOnlyExpired `.dobbyplugin` import/install bloklanmalıdır. | `HostPluginPackageInstallCoordinator` ReadOnlyExpired üçün `LicenseReadOnly` qaytarır; görünən UI action yoxdur və Denied/new-plugin/global-mode boşluqları qalır. |
| 45 | İCRA OLUNMAYIB | ReadOnlyExpired domain import/export/report/file output bloklanmalıdır. | Domain import/export/report/file-output policy call site tapılmadı. |
| 46 | QİSMƏN | ReadOnlyExpired print-to-file/PDF/virtual/unknown print target-ları bloklanmalıdır. | `PrintOutputPolicy` və classifier var; production print flow-da policy çağırışı tapılmadı. |
| 47 | QİSMƏN | ReadOnlyExpired fiziki printerə çap yalnız təsdiqlənəndə icazəli olmalıdır. | Classifier physical printer qaytara bilir; print execution/confirmation wiring yoxdur. |
| 48 | QİSMƏN | License status və rejection mesajları UI-da göstərilməlidir. | VM text/resources/presenter surface var; XAML render yoxdur və rejection detail generikdir. |
| 49 | QİSMƏN | Plugins Manager ekranı. | `MainWindow.xaml` plugins manager panelini və presenter data load surface-ini render edir; installer/lifecycle action wiring və fault/rejection UX hələ ayrıca plugin-management scope-da qalır. |
| 50 | QİSMƏN | Plugins Manager list view. | `ShellPluginManagerItem`, metadata cache snapshot və XAML list/details surface var; license/trust/fault fields və lifecycle action state-ləri tam bağlanmayıb. |
| 51 | QİSMƏN | Plugins Manager detail view. | `PluginsManagerSelectedDetailText` var; yalnız summary string-dir, detail UI yoxdur. |
| 52 | QİSMƏN | Plugin install action. | `HostPluginPackageInstallCoordinator.InstallPackage` və file picker var; manager install button/handler wiring yoxdur. |
| 53 | QİSMƏN | Plugin enable action. | `HostPluginLifecycleCoordinator.EnablePlugin` və VM enable flag var; UI action wiring yoxdur. |
| 54 | QİSMƏN | Plugin disable action. | `HostPluginLifecycleCoordinator.DisablePlugin` və VM disable flag var; UI action wiring yoxdur. |
| 55 | QİSMƏN | Plugin uninstall action. | `HostPluginLifecycleCoordinator.UninstallPlugin`, `PluginUninstallConfirmation` var; manager UI-dan bağlanmayıb. |
| 56 | QİSMƏN | User-facing plugin rejection mesajları. | Resources və rejection reasons var; install/rejection detalları UI action path-ına map olunmur. |
| 57 | QİSMƏN | Sample plugin: manifest, icon, page, operation target, extension registration. | Source, manifest, icon, package candidate target var; manifest DLL hash/signature placeholder-dir, final signed `.dobbyplugin` yoxdur. |
| 58 | QİSMƏN | Sample plugin Main Content daxilində işləməlidir. | Main Content plugin page host və route coordinator production-wired-dir; sample plugin package hələ as-is installable/signed deyil və final sample runtime proof qalır. |
| 59 | QİSMƏN | Sample plugin denial, Result, fault, progress, work-session davranışları. | Source-da action-lar var; production UI-dan reachable deyil və work-session action session-u close etmir. |
| 60 | QİSMƏN | `sign-plugin-package` admin aləti. | Admin command və `tools/sign-plugin-package/sign-plugin-package.ps1` var; external signing key env tələb edir, production signing proof yoxdur. |
| 61 | QİSMƏN | `create-plugin-license` admin aləti. | Command və wrapper var; external key/device fixture tələb edir və yalnız active license path-i yaradır. |
| 62 | İCRA OLUNMAYIB | `verify-plugin-package` admin aləti. | `tools/verify-plugin-package` və admin command tapılmadı; yalnız validator library mövcuddur. |
| 63 | QİSMƏN | `create-recovery-response` admin aləti. | Command və wrapper var; external signing key env/proof tələb edir. |
| 64 | QİSMƏN | Production build-lərdə Dev/QA artifact-lərin rədd edilməsi. | Trust profile və verifier DevQA vendor-u Production-da rədd edir; bootstrap keys və production release proof tam deyil. |
| 65 | QİSMƏN | MSIX installer əsas yayım formatı. | `installer/msix/build-rc-msix.ps1` və RC `.msix` artefaktları var; self-signed RC-only, GA/customer cert/profile yoxdur. |
| 66 | QİSMƏN | Installer upgrade davranışı. | Script `1.0.0.0` və `1.0.1.0` eyni identity ilə yaradır; install/upgrade/rollback execution proof yoxdur. |
| 67 | QİSMƏN | Upgrade zamanı app data qorunmalıdır. | `%LocalAppData%/Dobby` DataRoot və runtime folder prep var; installer-specific data preservation validation yoxdur. |
| 68 | İCRA OLUNMAYIB | Portable build üçün dev-only/support constraint enforcement. | Constraint əsasən README-dədir; script/runtime enforcement tapılmadı və output production appsettings istifadə edir. |
| 69 | QİSMƏN | Installer artefaktı mövcud app data ilə və login-first startup-la açılmalıdır. | MSIX stage `Dobby.Host.exe` daşıyır, app login loop/HostBootstrapper login-first-dir; installed artefaktla mövcud app data üzərində launch proof yoxdur. |

## Qısa Yekun

- Qalan maddə sayı: 69
- İCRA OLUNUB: 28
- QİSMƏN: 35
- İCRA OLUNMAYIB: 6
- Əsas fokus sahələri: customer/deployment provisioning, License import UI, ReadOnlyExpired geniş enforcement, Plugin Manager lifecycle action wiring, final sample/Admin artefaktları və installer/portable release sübutları.
