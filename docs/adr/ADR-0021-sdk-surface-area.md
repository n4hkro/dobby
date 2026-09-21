# ADR-0021: SDK Surface Area

**Status:** Accepted (§ 3a base-class lifecycle hook shape and § 4 plugin-facing compatibility gate amended by `ADR-0059` — see ## Revision History)
**Date:** 2026-04-30

## Context

`Dobby.SDK` plugin authors üçün convenience qatıdır, amma SDK şişərsə plugin-lər
host implementation detail-lərinə bağlana bilər. Architecture Map Cədvəl 3 SDK
surface-inin bağlanmasını tələb edirdi.

## Decision

### 1. SDK-nın təbiəti

`Dobby.SDK` convenience və authoring helper qatıdır; runtime implementation bypass
yolu deyil. Plugin-visible stable contract-lar `Dobby.Contracts`-da qalır. SDK
yalnız həmin contract-ları daha rahat istifadə etmək üçün helper/builder/adapter
surface təqdim edə bilər.

### 2. Dependency boundary

`Dobby.SDK` yalnız aşağıdakı project-lərə reference saxlaya bilər:

- `Dobby.Core`
- `Dobby.Contracts`
- `Dobby.Localization`

`Dobby.SDK` `Dobby.UI`, `Dobby.Shell`, `Dobby.Infrastructure`, `Dobby.Security`,
`Dobby.PluginFramework`, `Dobby.Licensing` və host implementation project-lərinə
reference saxlamır.

### 3. P1 allowed SDK surface

Phase 1 SDK surface aşağıdakı kateqoriyalarla məhdudlaşır:

- plugin module registration helper-ləri;
- manifest/extension descriptor builder-ləri;
- typed integration event publish/subscribe helper-ləri;
- Result/error ergonomics helper-ləri;
- storage contract convenience wrapper-ləri;
- localization key/resource helper-ləri;
- route/extension descriptor helper-ləri;
- background operation progress helper-ləri `Dobby.Contracts` operation/progress
  contract-larına adaptasiya kimi.

SDK yeni capability, storage backend, logging channel, telemetry channel, raw
license surface, WPF view type və ya host service locator təqdim etmir.

Storage convenience wrapper-ləri yalnız `Dobby.Contracts.PluginStorage`
contract-larını və `Result<T>`/`Result` semantics-ini ergonomik şəkildə wrap edir.
Onlar yeni backing store, yeni capability, host service locator, plugin-owned
connection/path access və ya `PluginBase` üzərində built-in Storage property-si
yaratmır.

### 3a. `PluginBase` / `PluginBaseV2` SDK shape

Architecture Map Row 81-dəki base class helper-ləri iki-tier module contract-ını
(`ADR-0006` §1, `ADR-0037`) əks etdirir. Canonical namespace
`Dobby.SDK.PluginFramework`-dür.

```csharp
public abstract class PluginBase : IPluginModule {
    public abstract void RegisterServices(IPluginServiceCollection services);
    public virtual void OnActivated(IPluginActivationContext context) { }
}

public abstract class PluginBaseV2 : PluginBase, IPluginModuleV2 {
    public sealed override void OnActivated(IPluginActivationContext context) { }
    public virtual ValueTask<Result> InitializeAsync(IPluginActivationContext context, CancellationToken cancellationToken);
    public virtual ValueTask<Result> ActivateAsync(IPluginActivationContext context, CancellationToken cancellationToken);
    public virtual ValueTask<Result> DeactivateAsync(CancellationToken cancellationToken);
    public virtual ValueTask<Result> UnloadAsync(CancellationToken cancellationToken);
}
```

- `RegisterServices(...)` abstract və mandatory-dir; plugin author service və
  extension-point registration intent-ini açıq göstərməlidir.
- `PluginBase.OnActivated(...)` virtual no-op və optional-dır; post-load activation
  işi olmayan v1 plugin boş override yazmağa məcbur edilmir.
- `PluginBaseV2` yeni plugin-lər üçün default base class-dır. O `OnActivated(...)`-ı
  `sealed override` no-op edir, çünki V2 activation-ı host həmişə `ActivateAsync(...)`
  ilə dispatch edir; bu, hər iki hook-un icra olunduğunu fərz edən double-activation
  səhvini contract səviyyəsində mümkünsüz edir.
- V2 lifecycle phase-lərinin hamısı virtual default `Result.Success()` qaytarır: plugin
  yalnız ehtiyac duyduğu phase-i override edir, lakin deactivate/unload hook-u SDK
  səviyyəsində **mövcuddur** (əvvəlki "`PluginBase` disable, uninstall və ya deactivate
  hook-u əlavə etmir" qaydası ləğv olunub — `ADR-0037` lifecycle qərarı).
- `PluginBase`-dən (yalnız v1 surface) törəyən yeni plugin host API 2.x-də hələ activate
  olunur, lakin legacy v1 adapter path-ı ilə: host `OnActivated(...)`-ı bounded timeout
  altında çağırır, outcome-a `legacy-v1-adapter-upgrade-required` warning-i qoyur və
  `plugin.lifecycle.v1-adapter-used` audit event-i yazılır (`ADR-0008` § 2,
  `ADR-0037`, `docs/specs/audit-and-diagnostics.md`). Adapter yalnız 2.x migration
  window-u üçündür və növbəti host API major gate-ində silinir, ona görə yeni plugin
  authoring üçün forward-compatible seçim yalnız `PluginBaseV2`-dir; v1 surface upgrade
  yolu, default deyil.
- V2 phase-lərinin hər biri host-dan per-call `CancellationToken` alır və host-owned
  bounded timeout altında icra olunur (plugin timeout dəyərini oxuya və dəyişə bilmir).
  Plugin author-ın öhdəliyi: token-i observe edib qaytarmaq. Cooperate etməyən hook
  preemptively dayandırılmır — host onu timeout ilə kəsir və nəticə `timeout` defect
  origin-i olur (`ADR-0008` § 4). Instance başına bir aktiv əməliyyat olur; ağır iş
  hook-un içində deyil, `ADR-0015` background operation contract-ında və ya lazy
  extension-point initialization-da aparılır.
- Phase-in **failed `Result`** qaytarması expected outcome-dur (`Forbidden`,
  `InvalidInput`, `LicenseReadOnly`, cancellation daxil) və defect sayılmır; exception
  throw etmək, contract breach və timeout defect origin-idir və bounded process-local
  quarantine sayğacını artırır (`ADR-0008` § 4, `ADR-0059`). Bu fərq SDK səviyyəsində
  vacibdir: gözlənilən validation səhvini exception ilə bildirən plugin öz-özünü
  quarantine-ə aparır.
- Base class-lar built-in Logger, Storage, EventBus convenience property-si,
  host service locator və ya yeni logging/telemetry channel təqdim etmir.
  Plugin-lər logging və digər services-ləri approved Contracts/SDK surface və
  normal DI pattern-ləri ilə, `ADR-0012` §2a qapalı set-i çərçivəsində alır.

### 4. Versioning

SDK breaking change plugin-facing breaking change-dir və Contracts review ilə
birlikdə idarə olunur. SDK helper əlavə etmək mümkündür, amma helper existing
contract semantic-lərini dəyişdirə bilməz. Deprecated helper ən azı bir release
cycle saxlanılmadan silinmir.

Plugin-facing compatibility gate-i `hostApiVersion` `MAJOR.MINOR` contract-ıdır və
authority-si `PluginCompatibilityMatrix`-dir (`ADR-0059`, `ADR-0037`); SDK versiyası
öz-özlüyündə gate deyil. Buna görə SDK/Contracts surface dəyişikliyi eyni change-də
host API version-a köçürülməlidir:

- additive, backward-compatible surface (yeni helper, yeni optional overload) host API
  **MINOR**-u artırır. Köhnə minor bəyan edən plugin uyğun qalır; yeni minor bəyan edən
  plugin isə həmin minor-u dəstəkləməyən host-da `unsupported-host-api-minor` ilə
  **rejected** olur;
- existing member-in semantic-ini, imzasını və ya lifecycle contract-ını dəyişən
  breaking change host API **MAJOR**-u artırır və mövcud plugin-lər
  `unsupported-host-api-major` ilə rejected olur — bu, adapter/migration window qərarı
  tələb edən release-level gate-dir (`ADR-0037`);
- deprecated helper-in silinməsi breaking change-dir: bir release cycle saxlanılma
  qaydası deprecation-ı elan etmək üçündür, MAJOR gate-ini əvəz etmək üçün deyil.

Version-ı artırılmamış surface dəyişikliyi compatibility gate-ini yan keçir: plugin
manifest-i keçir, sonra çağırış runtime-da `MissingMethodException`/contract breach
kimi partlayır — yəni clean `unsupported-host-api-*` rejection-ı əvəzinə plugin defect
origin-i və quarantine sayğacı alınır (`ADR-0008` § 4). Ona görə host API version
bump-ı SDK surface dəyişikliyinin ayrılmaz hissəsidir.

### 5. Sample plugin rule

Sample plugin yalnız `Dobby.Contracts` və `Dobby.SDK` public surface-lərinə
reference saxlamalıdır. Sample plugin implementation host internals-i istifadə
edirsə, AC-036 evidence etibarsız sayılır.

## Consequences

- Plugin authoring rahatlığı host internals leakage yaratmır.
- Static dependency scan AC-043 üçün qapalı reference list əldə edir.
- SDK skeleton platform-neutral `net10.0` qala bilər; WPF-specific surface SDK-ya
  daşınmır.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `Dobby.SDK`, plugin-facing surface qaydaları
- `docs/03_TECH_STACK_AND_VERSIONS.md` — Target Framework split
- `docs/adr/ADR-0001-contracts-dependency-boundary.md`
- `docs/adr/ADR-0006-plugin-di-registration.md` — iki-tier module contract və registration surface
- `docs/adr/ADR-0010-plugin-communication-policy.md`
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — §2a qapalı host-injectable capability set
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md` — `IPluginModuleV2` lifecycle və v1 adapter window
- `docs/adr/ADR-0008-deferred-plugin-activation.md` — § 2 activation dispatch, § 4 outcome
  origin və bounded process-local quarantine
- `docs/adr/ADR-0015-background-operation-reporting.md` — hook-dan kənar uzunmüddətli iş
  üçün background operation contract-ı
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — bu ADR-in §3a base-class
  lifecycle hook shape və §4 plugin-facing compatibility gate hissələrini amend edir;
  həmin bölmələr üçün current authority
- `docs/specs/audit-and-diagnostics.md` — `plugin.lifecycle.v1-adapter-used` audit event-i
- `docs/specs/sample-plugin.md`

## Revision History

- **2026-08-27:** §3a shipped SDK shape-i ilə uzlaşdırıldı. Əvvəlki mətn yalnız
  `PluginBase : IPluginModule` bloku göstərir və "`PluginBase` Phase 1-də disable,
  uninstall və ya deactivate hook-u əlavə etmir" qaydasını qoyurdu; halbuki SDK-da
  `PluginBaseV2 : PluginBase, IPluginModuleV2` mövcuddur, `OnActivated`-ı `sealed
  override` no-op edir və `InitializeAsync`/`ActivateAsync`/`DeactivateAsync`/
  `UnloadAsync` virtual default-larını verir. Yanlış mətn plugin author-ı deactivate
  cleanup hook-unun olmadığına inandıra bilərdi — nəticədə subscription/work session
  cleanup-ı `ADR-0037` phase-lərində deyil, host-un təmizləyəcəyi fərziyyəsi ilə
  yazılardı. Qərar dəyişmir: lifecycle `ADR-0037`-də qərarlaşıb və bu ADR-in
  lifecycle hissələri `ADR-0059` ilə amend olunub, ona görə successor ADR
  yaradılmır — bu factual SDK surface reconciliation-dır.
- **2026-08-31:** §3a plugin-author öhdəlikləri və §4 plugin-facing compatibility gate
  `ADR-0059`/`ADR-0037`/`ADR-0008` ilə uzlaşdırıldı; `**Status:**` sətrinin §4 amendment
  claim-i əvvəl bu bölmənin gövdəsində heç bir izi olmayan boş claim idi.
  (a) §3a v1 authoring-in nəticəsini yazmırdı. `PluginBase`-dən törəyən yeni plugin
  legacy adapter path-ı ilə activate olunur, `legacy-v1-adapter-upgrade-required`
  warning-i və `plugin.lifecycle.v1-adapter-used` audit event-i yaradır və adapter
  növbəti host API major gate-ində silinir; əvvəlki mətn v1-i sadəcə "optional" kimi
  göstərirdi, yəni author 3.x-də işləməyəcək surface-ə qarşı yeni plugin yazıb bunu
  bilməzdi.
  (b) §3a V2 signature-larını göstərirdi, amma hook öhdəliyini yazmırdı: per-call
  cancellation token-in observe olunması, host-owned bounded timeout, instance başına bir
  aktiv əməliyyat, ağır işin `ADR-0015` background operation-a köçürülməsi. Bu boşluq
  konkret defect yaradırdı — `ActivateAsync` içində token-i ignore edən uzun blocking iş
  timeout defect origin-i olur və üç dəfə təkrarlandıqda plugin process-local quarantine-ə
  düşür.
  (c) §3a failed `Result` (expected outcome) ilə exception/contract breach/timeout
  (defect origin) fərqini yazmırdı; SDK sənədi author-ın oxuduğu yer olduğu üçün
  gözlənilən validation səhvini exception ilə bildirən plugin öz-özünü quarantine-ə
  aparırdı.
  (d) §4 yalnız "SDK breaking change plugin-facing breaking change-dir" deyirdi və
  plugin-facing yeganə real gate-i — `hostApiVersion` `MAJOR.MINOR` contract-ını — heç
  adlandırmırdı. Additive → MINOR, breaking → MAJOR qaydası, deprecated helper silinməsinin
  breaking olması, və version bump-sız surface dəyişikliyinin clean
  `unsupported-host-api-*` rejection-ı runtime contract breach-ə çevirməsi explicit
  yazıldı. Qərar dəyişmir: gate-in authority-si `PluginCompatibilityMatrix`-də
  (`ADR-0059`, `ADR-0037`) qalır, bu bölmə onu SDK tərəfindən görünən dilə çevirir.
