# Dobby Architecture Map

**Tarix:** 2026-04-23
**Status:** Təsdiqlənib
**Scope:** Runtime qatları, dependency xəritəsi, qərar/risk qeydiyyatı, engineering conventions, phase/status/stability taxonomy

Bu sənəd Dobby üçün yekunlaşdırılmış arxitektura xəritəsidir.
Burada runtime qatları, dependency xəritəsi, açıq qərarlar/risklər və engineering conventions bölməsi birlikdə verilir.

---

## Legend / Taxonomy

Aşağıdakı dəyərlər bu sənəddə ardıcıl istifadə olunur. Hər sütun üçün həm mənası, həm də keçid meyarı formal verilir.

### Phase

- **P1-core** — Phase 1-in mütləq tərkibinə daxildir; olmadan release buraxılmır.
- **P1-if-time** — Phase 1-də arzuolunandır, vaxt qalsa tamamlanır; olmadan release bloklanmır.
- **P2** — Planlı növbəti release-in tərkibinə düşür; dizayn Phase 1 ilə uzlaşdırılmalıdır.
- **later** — Post-GA və ya məhsul böyüdükdən sonra; konkret release-ə hələ bağlı deyil.

**Parent/child qaydası:** Bir project-in phase-i onun ən erkən tələb olunan namespace-inin phase-i ilə müəyyən olunur. Alt namespace-lər ayrı-ayrı öz phase-lərini saxlaya bilər (məsələn, parent `P1-core` olarkən child `P2` qala bilər).

**Keçid meyarı:** `P1-if-time → P1-core` keçidi yalnız Phase 1 tələbi dəqiqləşəndə və ya daha erkən/məcburi phase-li başqa komponentin bu namespace-ə bağlılığı aşkarlananda baş verir.

### Status

- **agreed** — Forma və sərhəd bağlanıb; implementasiyaya və ya contract code-a keçid mümkündür.
- **planned** — Yer sənəddə tutulub, amma shape və ya əlaqəli ADR decision bloklayır.

**Keçid meyarı:** `planned → agreed` keçidi yalnız bağlı ADR bağlandıqda və ya shape sənəddə dəqiq yazıldıqda baş verir.

### Stability

- **stable** — Public surface breaking change olmadan mövcuddur; breaking change rəsmi major version bump tələb edir.
- **evolving** — Public surface hələ formalaşır; Phase 1 müddətində breaking change gözləniləndir, Phase 2-dən sonra stabilləşməsi hədəfdir.
- **experimental** — Heç bir stabillik zəmanəti yoxdur; istifadə öz riski ilə.

**Keçid meyarı:** `evolving → stable` keçidi ən azı bir release cycle breaking change olmadan keçəndə və public surface review bağlananda baş verir.

### Plugin visibility

- **no** — `no` birbaşa plugin referansının qadağan olduğunu bildirir; plugin bu project-ə birbaşa reference saxlaya bilməz. Transitive visibility yalnız `Dobby.Contracts` və ya `Dobby.SDK` public surface-i üzərindən qəbul olunur (bax: `Core Public Surface Stability`).
- **contracts-only** — Plugin yalnız bu project-in public contract tiplərini görür; runtime implementation gizlidir.
- **sdk-only** — Plugin bu project-in yalnız SDK-development-facing public surface-ni görür; internal hissələri gizlidir.

### Decision Blocker

Decision-in niyə hələ açıq olduğunu təyin edir; dəyərlər aşağıdakı qruplara düşür:

- **Kod blokları:** `implementation` — implementasiyaya başlamaq üçün bağlanmalıdır.
- **Dizayn blokları:** `design` — shape/model dəqiqləşməlidir; `P2 design` — Phase 2 dizayn tsiklində bağlanır.
- **Mərhələ blokları:** `Phase 1 minimum decision` — Phase 1-in minimum dəsti üçün kifayət edən qərar tələb olunur; `Phase 1 deferred decision` — Phase 1-də bağlanmır, sonra dərinləşir.
- **Zaman blokları:** `scaling` — məhsul böyüdükcə bağlanır; `release` — release strategiyası kontekstində bağlanır.
- **Bloksuz:** `none` — qərar bağlıdır, qalan iş sadəcə icradır.

### Test strategy

Cədvəl 3-dəki `Test strategiyası` sütununun dəyərləri:

- **contract** — Interface/contract uyğunluq testi; implementation-dan asılı deyil.
- **unit** — İzolə edilmiş məntiq üçün unit test.
- **integration** — Real və ya near-real adjacent komponentlərlə birlikdə test.
- **snapshot** — Render və ya output snapshot qarşılaşdırması.
- **manual** — Avtomatlaşdırıla bilməyən və ya Phase 1-də manual qalan ssenari.

Kombinasiyalar (məs. `contract + integration`) eyni anda bir neçə strategiya tələbini göstərir.

---

## Cədvəl 1 — Əsas xəritə

| # | Ad | Tip | Aid olduğu Project | Məsuliyyət | Plugin visibility | Phase | Status |
|---:|---|---|---|---|---|---|---|
| 1 | Dobby.Host | project | self | Proqramın giriş nöqtəsidir; startup, bootstrap və composition root-u idarə edir. | no | P1-core | agreed |
| 2 | Startup / Bootstrap | namespace | Dobby.Host | Host-un qalxma ardıcıllığını, ilkin minimum datanı və login öncəsi/sonrası yüklənmə qaydasını idarə edir. | no | P1-core | agreed |
| 3 | Dobby.Core | project | self | Sistemin fundamental bünövrəsidir; ortaq primitive-ləri, baza abstraction-ları və dayaq davranışlarını saxlayır. | no | P1-core | agreed |
| 4 | Result Standardization | namespace | Dobby.Core | Bütün əməliyyatlar üçün vahid success/failure/result modelini verir. Exception vs. result ayrımı `docs/04_RESULT_VS_EXCEPTION_POLICY.md` ilə təsbit olunur. | no | P1-core | agreed |
| 5 | Error Codes | namespace | Dobby.Core | Result model-lərində və sistem xətalarında istifadə olunan sabit error code-ları saxlayır. | no | P1-core | agreed |
| 6 | Validation | namespace | Dobby.Core | Field, async və ümumi validation primitive-lərini verir; business rule-lar plugin və ya `Dobby.Application` tərəfindədir. | no | P1-core | agreed |
| 7 | Time | namespace | Dobby.Core | Vaxt, expiry, timeout və timestamp üçün vahid time provider yanaşması verir. | no | P1-core | agreed |
| 8 | ID / Key Generation | namespace | Dobby.Core | Phase 1 üçün Correlation ID ownership-i və minimum generator/context accessor primitive-lərini saxlayır; job id, audit id və digər geniş ID mexanizmləri P2-yə qala bilər. | no | P1-core | agreed |
| 9 | Serialization | namespace | Dobby.Core | JSON/config/cache serialization qaydalarını və uyğunluq davranışını idarə edir. | no | P1-core | agreed |
| 10 | Fallback / Degradation | namespace | Dobby.Core | Bir hissə işləməyəndə sistemin kontrollu fallback davranışını müəyyən edir. | no | P2 | planned |
| 11 | Resource Lifecycle / Disposal | namespace | Dobby.Core | Unload, unsubscribe, timer/file/stream cleanup və resurs ömrünü idarə edir. | no | P2 | planned |
| 12 | Concurrency / Synchronization | namespace | Dobby.Core | Parallel əməliyyatlar, duplicate execution və race condition qorunmasını təmin edir. | no | P2 | planned |
| 13 | Page State Management | namespace | Dobby.UI | Shell və built-in WPF ekranlarında Loading, empty, error, forbidden, dirty kimi page state modellərini standartlaşdırır; plugin-visible page state contract lazım olarsa `Dobby.Contracts`/`Dobby.SDK` səthində ayrıca bağlanır. | no | P1-if-time | planned |
| 14 | Selection / Bulk Operation State | namespace | Dobby.UI | Shell və built-in WPF grid/list-lər üçün select all, partial select və bulk action state məntiqini standartlaşdırır; cross-plugin/plugin-visible selection contract P2 deferred bulk-selection/SDK surface qərarı olmadan buradan çıxmır. | no | P1-if-time | planned |
| 15 | Configuration Primitives | namespace | Dobby.Core | Config binding və default value pattern-ləri üçün baza primitive-ləri verir; tam settings manager deyil. | no | P1-core | agreed |
| 16 | Dobby.Contracts | project | self | Sistem hissələri arasında sərhəd-müqavilələri saxlayır; runtime implementasiya vermir. | contracts-only | P1-core | agreed |
| 17 | Contracts / Shared Models | namespace | Dobby.Contracts | Request/response, paged result, query DTO və ümumi model müqavilələrini saxlayır. | contracts-only | P1-core | agreed |
| 18 | Plugin Contracts | namespace | Dobby.Contracts | Plugin ilə host arasında rəsmi interfeysləri saxlayır. | contracts-only | P1-core | agreed |
| 19 | Extension Point Contracts | namespace | Dobby.Contracts | Menu, page, toolbar və digər genişlənmə nöqtələrinin contract-larını saxlayır. | contracts-only | P1-core | agreed |
| 20 | Event Contracts | namespace | Dobby.Contracts | Integration event base tiplərini, `IEventBus` kimi müqavilələri və event sərhədini saxlayır. | contracts-only | P1-core | agreed |
| 21 | Plugin Storage Contracts | namespace | Dobby.Contracts | Plugin storage müqavilələrini saxlayır; canonical compartments `SharedDomainStorage` və `UserScopedStorage` kimi ADR-0030-da bağlanır. | contracts-only | P1-core | agreed |
| 22 | Policy Enforcement Contracts | namespace | Dobby.Contracts | Capability yoxlamaları üçün `ICapabilityPolicy<TCapability>` + `PolicyDecision` pattern-li contract-ları saxlayır (ADR-0004); enforcement implementasiya Security-dədir. God-interface qadağandır. | contracts-only | P1-core | agreed |
| 23 | UI Composition Ordering Contracts | namespace | Dobby.Contracts | UI extension sıralaması üçün Group/Weight və stabil ordering contract-larını saxlayır. | contracts-only | P1-core | agreed |
| 24 | Dobby.PluginFramework | project | self | Plugin discovery, activation, dependency, lifecycle və management məntiqinin əsas evidir. | no | P1-core | agreed |
| 25 | Plugin Framework | namespace | Dobby.PluginFramework | Plugin-lərin tapılması, local `.dobbyplugin` install flow-u, enable/disable, uninstall, status tracking və lifecycle idarəsini edir; update/repair/downgrade P2-yə saxlanılır. | no | P1-core | agreed |
| 26 | Manifest / Metadata Management | namespace | Dobby.PluginFramework | Plugin manifest-lərini oxuyur, doğrulayır və display metadata-nı (name/version/description) runtime code execution etmədən plugin list/cached metadata-ya çevirir. | no | P1-core | agreed |
| 27 | Plugin Packaging | namespace | Dobby.PluginFramework | `.dobbyplugin` package formatı, manifest `signature` block-u, manifest `files` section-u, canonical signed content fingerprint, package content boundary və detached `.dobbylicense` artifact ayrılığını idarə edir. Plugin package `.dobbylicense`, customer data, device fingerprint, vendor private key, host internal config və host persistence migration script daşımır. | no | P1-core | agreed |
| 28 | Plugin Update / Rollback | namespace | Dobby.PluginFramework | Plugin yenilənməsi və uğursuzluqda əvvəlki vəziyyətə qayıtma məntiqini idarə edir. | no | P2 | planned |
| 29 | Plugin Compatibility / Versioning | namespace | Dobby.PluginFramework | Host ilə plugin arasındakı versiya/compatibility uyğunluğunu yoxlayır; Phase 1 minimum davranış: compatibility tələbi ödənməyən plugin activation-a buraxılmır. | no | P1-core | agreed |
| 30 | Plugin Service Registration Bridge | namespace | Dobby.PluginFramework | Plugin `IPluginModule.RegisterServices` surface-i üzrə service-ləri host DI-yə bağlayır; plugin lifetime singleton instance-ları plugin disable zamanı dispose olunur; host service-ləri plugin-ə yalnız capability surface üzərindən görünür (ADR-0006). | no | P1-core | agreed |
| 31 | Assembly Isolation / Load Context Management | namespace | Dobby.PluginFramework | Hər plugin ayrıca `AssemblyLoadContext` (ALC) içində yüklənir; Phase 1-də `isCollectible: false`; shared dependency-lər (Dobby.Contracts/SDK/Core, BCL, Microsoft.Extensions.*) host ALC-dən pin edilir; plugin-ə məxsus third-party dependency-lər plugin ALC daxilində qalır; out-of-process worker P1 scope deyil və yalnız `ADR-0007` P2/later trigger-ləri ilə yenidən qiymətləndirilir. | no | P1-core | agreed |
| 32 | Deferred Plugin Activation | namespace | Dobby.PluginFramework | Login ekranı görünənə qədər plugin discovery/manifest scan başlamır; login-visible pre-auth mərhələdə yalnız yüngül cached plugin metadata oxuna bilər; fresh post-login discovery + dependency graph sort + per-plugin activation loop (compatibility + license + assembly load + `IPluginModule.RegisterServices` + `OnActivated` hook); per-plugin failure/runtime fault isolation (ADR-0008). | no | P1-core | agreed |
| 33 | Plugin Dependency Resolution | namespace | Dobby.PluginFramework | `dependsOn` graph-ını, activation sırasını, missing/circular dependency davranışını idarə edir. | no | P1-core | agreed |
| 34 | Dobby.Security | project | self | Authentication, authorization, trust və host-controlled capability sərhədini qoruyur. | no | P1-core | agreed |
| 35 | Authentication / Session | namespace | Dobby.Security | Device-bound local password-based auth üzərində login, logout cleanup, single active user session, lock/manual lock/auto-lock, self-service password change session semantics, session creation və invalid/expired session davranışını idarə edir (ADR-0031, ADR-0038); remember-me yalnız pre-auth masked-fill rahatlığıdır və auto-login/session refresh yaratmır; ilk uğurlu giriş heç bir istifadəçi mövcud olmadığı halda həmin hesabı `Owner` kimi təsbit edir (first-user-owner bootstrap); `Owner` password recovery artefakt-ının imza, expiry, device fingerprint və replay yoxlamalarını aparır (ADR-0039); cloud identity (Entra/OIDC) Phase 1 scope-una daxil deyil. | no | P1-core | agreed |
| 36 | Current Context | namespace | Dobby.Security | Current user, single active customer/deployment context və `default` tenant storage primitive-i təqdim edir (`ADR-0052`). Multi-customer runtime switching və storage migration P2-yə saxlanılır, amma customer/deployment license binding Phase 1-də enforce olunur. | no | P1-core | agreed |
| 37 | Permissions / Policy | namespace | Dobby.Security | Registry-based operation authorization, Owner all-grants/protected-principal behavior, managed-user default deny, grant-only/on-off permission data, built-in target registration, plugin target registration from validated signed manifest `operationTargets` metadata before plugin activation, User Management create/update/reset/deactivate/reactivate/grant guard-ları və audited deny decisions typed `ICapabilityPolicy<OperationPermissionCapability>` pattern-i (`ADR-0004`) ilə enforce olunur. Verb set host-defined closed list-dir; plugin yalnız canonical verb-lərdən dəstəklədiyini elan edə bilər. | no | P1-core | agreed |
| 38 | Plugin Trust Verification | namespace | Dobby.Security | Plugin signature və vendor trust doğrulamasını edir. | no | P1-core | agreed |
| 39 | Plugin Isolation / Sandbox | namespace | Dobby.Security | Plugin-in nəyə çata biləcəyini host sərhədi ilə məhdudlaşdırır; wrapper özü deyil, enforcement əsasdır. | no | P1-core | agreed |
| 40 | Secrets / Secure Storage | namespace | Dobby.Security | Password, token, key, remember-me secret, recovery secret, session material və protected config üçün field-level/protected-storage qoruması verir; protected-storage scope mapping `ADR-0055` ilə bağlanır. | no | P1-core | agreed |
| 41 | Plugin Capability Boundary | namespace | Dobby.Security | Plugin-ə görünən host capability surface-ni müəyyən edir; plugin page hosting yalnız official Contracts/SDK surface ilə gedir, runtime fault plugin boundary-də isolate edilir və licensing üçün non-disclosing runtime mode surface-i (`FullAccess`, `ReadOnlyExpired`, `Denied`) `ADR-0053` ilə bağlanır. | no | P1-core | agreed |
| 42 | Dobby.Licensing | project | self | Plugin entitlement, activation period və expiry məntiqinin evidir. | no | P1-core | agreed |
| 43 | License / Entitlement | namespace | Dobby.Licensing | License v2 state machine, mandatory customer/deployment/device/package binding, grace, read-only expired mode, `Plugins > License` import flow-u və non-disclosing deny/mode mapping-i idarə edir (`ADR-0051`, `ADR-0052`, `ADR-0053`). | no | P1-core | agreed |
| 44 | Dobby.Configuration | project | self | App config, user/plugin settings və feature flag-ların mərkəzi evidir. | no | P1-core | agreed |
| 45 | Application Configuration Management | namespace | Dobby.Configuration | Environment-specific app config, binding, validation və default-ları idarə edir. | no | P1-core | agreed |
| 46 | User / Plugin Settings Management | namespace | Dobby.Configuration | User preferences və shell view preferences-lərini idarə edir; Sidebar/Third Bar ordering, TopToolbar plugin ordering/hidden state, window size/position və sidebar collapse state per-user saxlanır, default ordering canonical `ADR-0005/ADR-0028` authority altında qalır. Plugin-facing settings access P1-də yoxdur və P2-deferred qalır (`ADR-0027`). | no | P1-core | agreed |
| 47 | Feature Flags / Capability Settings | namespace | Dobby.Configuration | Funksiya aktiv/passivliyi və rollout qaydalarını config üzərindən idarə edir. | no | P2 | planned |
| 48 | Dobby.Localization | project | self | String və localization resurslarının vahid evidir. | sdk-only | P1-core | agreed |
| 49 | String / Localization Management | namespace | Dobby.Localization | Resource provider, Phase 1 `az-AZ` default culture, fallback behavior, namespace-based resource naming, missing-key diagnostics və canonical terminology mapping-i idarə edir. Host resources mərkəzləşdirilmişdir; plugin localization resources plugin assembly-lərində qalır. Runtime culture switch P2-yə saxlanılır. | sdk-only | P1-core | agreed |
| 50 | Dobby.Navigation | project | self | TopToolbar, Sidebar, Third Bar və route-state kompozisiyasını idarə edir. | no | P1-core | agreed |
| 51 | Navigation Composition | namespace | Dobby.Navigation | TopToolbar, Sidebar, Third Bar, favorites, breadcrumb və hierarchical navigation contribution descriptors-i kompozisiya edir. Active plugin entries TopToolbar-da görünür; selected TopToolbar child-ları Sidebar-a, selected Sidebar child-ları Third Bar-a düşür. Ordering default `group + weight + stable tiebreak` modelidir, user override-ları ADR-0005 boundary-si ilə saxlanır. Manifest target vocabulary closed qalır: `top-toolbar`, `sidebar`, `third-bar`, `plugin-page-region`. | no | P1-core | agreed |
| 52 | Routing / Deep Link Handling | namespace | Dobby.Navigation | Navigation state ilə səhifə/view xəritəsini və deep link davranışını idarə edir. | no | P1-core | agreed |
| 53 | Dobby.Messaging | project | self | Event yayımı üçün runtime implementasiyanı verir; pluginlərə birbaşa görünmür. | no | P1-core | agreed |
| 54 | Messaging Core | namespace | Dobby.Messaging | Event publish/subscribe və bus implementasiyası; in-memory event dispatch (Phase 1 scope); subscription lifecycle plugin lifecycle-a bağlıdır (plugin disable zamanı subscription-lar təmizlənir); plugin-ə yalnız `IEventBus` contract üzərindən görünür (Row 20 Event Contracts referansı). | no | P1-core | agreed |
| 55 | Messaging UI Bridge | namespace | Dobby.Messaging | Event-lərin UI thread və UI notification axınına təhlükəsiz ötürülməsini təmin edir; host-internal `IUiDispatcher` abstraction-ı `Dobby.Messaging.UiDispatch` altında qalır, WPF implementation `Dobby.Shell`-də wire edilir; notification surface taxonomy toast, banner və modal olaraq shell UI spec-də bağlanır. | no | P1-core | agreed |
| 56 | Dobby.Application | project | self | Host-un use-case və application flow qatıdır; platforma davranışlarını və əməliyyat orkestrini saxlayır, vertical business logic daşımır. | no | P1-core | agreed |
| 57 | API / Communication | namespace | Dobby.Application | Yalnız opsional internet əsaslı inteqrasiyalar, service client, sync/upload/download və auth injection mexanizmini verir; core host və built-in məhsul funksionallığı buna bağlı deyil. | no | P2 | planned |
| 58 | Querying Infrastructure | namespace | Dobby.Application | Filter, sort, paging və query DTO davranışını ortaqlaşdırır. | no | P1-if-time | planned |
| 59 | Search | namespace | Dobby.Application | Debounce, normalization, accent tolerance və suggestion/recent search davranışını verir. | no | P2 | planned |
| 60 | Caching / Query Orchestration | namespace | Dobby.Application | Query cache, invalidation, stale/refresh və dedupe məntiqini idarə edir. | no | P1-if-time | planned |
| 61 | Import / Export | namespace | Dobby.Application | CSV/Excel və digər formatlarda import/export axınlarını mərkəzləşdirir. | no | P2 | planned |
| 62 | File Processing | namespace | Dobby.Application | Fayl validation, temporary storage, parsing və preview pipeline-ini idarə edir. | no | P2 | planned |
| 63 | Background Tasks / Process Reporting | namespace | Dobby.Application | In-memory work session-lar üçün minimum progress/cancel/reporting implementation-ını verir; plugin-facing operation snapshot/progress/cancellation contract tipləri `Dobby.Contracts` altında qalır, SDK isə yalnız helper/adapter verir. Richer queue/retry və advanced reporting P1-if-time/P2-də dərinləşə bilər. Bu sahədə bütün host-owned background operation work (long-running I/O, storage, plugin install/license import, gələcək shared-data work) Row 63 + `NFR-021` sərhədində qalır və ad-hoc UI-thread work və ya standalone spec kimi implementasiya olunmur. | no | P1-core | agreed |
| 64 | Command / Action Pipeline | namespace | Dobby.Application | Validate-authorize-execute-audit-notify-refresh axınını və read-only expired mode üçün fail-closed mutation/extraction gates-i standartlaşdırır; print/file-output decisions üçün host-internal `IPrintTargetClassifier` abstraction-ı `Dobby.Application.Printing` altında qalır, physical print/OS adapter implementation-ı `Dobby.Infrastructure.Printing` tərəfindən təmin olunur. | no | P1-core | agreed |
| 65 | Dobby.Diagnostics | project | self | Texniki müşahidə, log, metric, trace və sistem sağlamlıq görünürlüyünü təmin edir; default local-only işləyir və vendor-a avtomatik telemetry/upload etmir. | no | P1-core | agreed |
| 66 | Logging | namespace | Dobby.Diagnostics | Texniki log-ları, exception flow, diagnostic qeydiyyatı və explicit local diagnostics export package flow-unu saxlayır; raw secrets və raw license detail export olunmur. | no | P1-core | agreed |
| 67 | Observability | namespace | Dobby.Diagnostics | Metrics, traces və performans ölçülərini təmin edir. | no | P2 | planned |
| 68 | Health / Diagnostics | namespace | Dobby.Diagnostics | Runtime health check, plugin status və connectivity diaqnostikasını verir. | no | P2 | planned |
| 69 | Dobby.Audit | project | self | Kim, nə vaxt, nə etdi tarixçəsini rəsmi audit kimi saxlayır. | no | P1-core | agreed |
| 70 | Audit Trail | namespace | Dobby.Audit | İdarəetmə və compliance yönümlü append-only audit qeydiyyatını saxlayır; texniki log deyil, audit viewer P1 blocker deyil və baxış ayrıca permission/capability tələb edir. | no | P1-core | agreed |
| 71 | Dobby.UI | project | self | Shared WPF vizual komponentlər və UI davranış infrastrukturu verir; plugin-facing descriptor/extension contract-ları `Dobby.Contracts`/`Dobby.SDK` altında qalır. | no | P1-core | agreed |
| 72 | UI Component Library | namespace | Dobby.UI | Shell-in aktiv istifadə etdiyi reusable WPF komponentlər və əsas vizual blokları verir. Minimum host/built-in WPF implementation inventory `docs/specs/design-system-assets.md` içində bağlanır: WPF-UI shell basis, built-in form controls, DataGrid/list/status primitives və P1-if-time controls. Plugin-visible public surface bu row-dan çıxmır. | no | P1-core | agreed |
| 73 | Theme / Layout System | namespace | Dobby.UI | Phase 1 minimum foundation: design tokens (primary/surface color set), spacing scale və base typography scale P1-core-dur və shell/built-in WPF ekranlarında aktiv istifadə olunur. Richer theming (custom themes, advanced layout, motion) P1-if-time qalır; plugin-visible theme contract bu row-dan çıxmır. | no | P1-core | agreed |
| 74 | Dialog / Modal Orchestration | namespace | Dobby.UI | Shell və built-in WPF ekranları üçün confirm, prompt, wizard və progress modal davranışını standartlaşdırır; plugin-visible dialog descriptor/contract lazım olarsa `Dobby.Contracts`/`Dobby.SDK` səthində ayrıca bağlanır. | no | P1-if-time | planned |
| 75 | Notification / Feedback | namespace | Dobby.UI | Shell və built-in WPF ekranlarında toast, alert və success/warning/error feedback mexanizmini verir; plugin-visible notification contract bu implementation row-dan çıxmır. | no | P1-if-time | planned |
| 76 | Forms Infrastructure | namespace | Dobby.UI | Shell və built-in WPF ekranları üçün shared form rendering, submit flow və field davranışlarını verir; plugin-visible form descriptor/contract ayrıca Contracts/SDK surface qərarı tələb edir. | no | P1-if-time | planned |
| 77 | DataGrid / List Infrastructure | namespace | Dobby.UI | Shell və built-in WPF grid/list-lər üçün paging, columns, row actions və bulk UX infrastrukturu verir; plugin-visible grid/list contract bu row-dan çıxmır. | no | P1-if-time | planned |
| 78 | Dobby.Shell | project | self | Login sonrası əsas shell layout və hosting qatıdır. | no | P1-core | agreed |
| 79 | Shell Composition / Hosting | namespace | Dobby.Shell | TopToolbar, Sidebar, Third Bar soft panel cutout slot, Main Content Area, bottom status/progress, profile menu, host-shipped built-in page set-i (`Home`, `Plugins`, `Settings`), plugin page hosting-i, shell inteqrasiyasını, minimized work strip-i və process-lifetime in-memory work session explicit minimize/restore orchestration-ını idarə edir; silent auto-minimize yoxdur. | no | P1-core | agreed |
| 80 | Dobby.SDK | project | self | Plugin developer üçün helper-lər və rahat extensibility səthi verir. | sdk-only | P1-core | agreed |
| 81 | Developer SDK / Extensibility Helpers | namespace | Dobby.SDK | Plugin base class-ları (`PluginBase : IPluginModule`), fluent registration helper-lər, storage convenience wrapper-lər, operation/localization/result ergonomics və convenience attribute-lar; SDK yeni host capability, logging/audit/telemetry channel, raw license surface və ya host service locator yaratmaz, yalnız mövcud `Dobby.Contracts`, `Dobby.Core` və `Dobby.Localization` surface-lərini ergonomik istifadəyə çevirir (`ADR-0021`). | sdk-only | P1-core | agreed |
| 82 | Dobby.Update | project | self | Dobby host tətbiqinin yenilənməsi üçün ayrılmış project-dir; Phase 1 product minimumunda deyil, P2 release-ində dərinləşir. Phase 1-də yalnız manual file replacement path dəstəklənir; internet əsaslı auto-update, signed self-update və rollback P2-yə keçir. Phase 1 skeleton `.sln`/`.csproj` graph-ında `Dobby.Update` project-i yaradılmır və heç bir P1 project `Dobby.Update`-ə referans saxlamır; manual local replacement path Host/release docs ilə ifadə olunur. | no | P2 | planned |
| 83 | Host Update Mechanism | namespace | Dobby.Update | Host app üçün update flow P2-yə keçirilir; Phase 1-də yalnız manual local replacement saxlanılır, internet əsaslı version check, signed self-update, rollback və geniş compatibility davranışı P2-də bağlanır. | no | P2 | planned |
| 84 | Dobby.Admin | project | self | Customer, plugin və activation/signing idarəsi üçün ayrıca admin tətbiqidir. Phase 1-də yalnız minimal recovery signer/generator path-i məcburidir (ADR-0039 üçün); tam admin UI P1-if-time qalır. | no | P1-core | agreed |
| 85 | Admin Application Composition | namespace | Dobby.Admin | Tam admin UI composition: Customer seçimi, plugin seçimi, activation period və signing flow üçün vahid surface. Phase 1-də tələb olunmur; P1-if-time-da vaxt qalsa tamamlanır. | no | P1-if-time | planned |
| 86 | Owner Recovery Artefakt Generation | namespace | Dobby.Admin | `Owner` password recovery response artefaktının vendor tərəfdə imzalanması və generasiyası üçün minimal CLI/surface (ADR-0039). Phase 1-də recovery axını məhsul minimumunda olduğu üçün bu namespace P1-core-dur; tam UI composition (Row 85) P1-if-time qalır. | no | P1-core | agreed |
| 87 | Dobby.Infrastructure | project | self | Host-internal Win32/OS adapter qatı; plugin sərhədinə sızmır. Hardware identity query, consumed recovery nonce persistence və Win32 API wrapper-lərini saxlayır (ADR-0039 asılılığı). | no | P1-core | agreed |
| 88 | Hardware Identity / Device Binding | namespace | Dobby.Infrastructure | Registry, WMI və host-owned stable device identity source-ları üzərindən Owner recovery və license binding üçün internal device identity materialını hesablayır; ADR-0039 5-hash composite recovery flow-u fuzzy/quorum modelidir, ADR-0051 license fingerprint-i isə exact `MachineGuid + Dobby stable device id` binding modelidir. | no | P1-core | agreed |
| 89 | Host Persistence / Consumed Nonces | namespace | Dobby.Infrastructure | ADR-0030 uyğun host persistence implementasiyasının internal evidir; protected local DB backing store-larını, secret field wrappers-ləri və recovery üçün consumed nonce append-only store-unu saxlayır. | no | P1-core | agreed |
| 90 | Versioning | namespace | Dobby.Core | `HostVersion` və `IHostVersionProvider` primitive-lərini saxlayır; plugin-visible istifadə yalnız `Dobby.Contracts` / `Dobby.SDK` sərhədi və `ADR-0001` public-surface qaydası ilə açılır. | contracts-only | P1-core | agreed |

---

## Cədvəl 2 — Dependency xəritəsi

| Ad | Referans alır | Onu referans alanlar | Stability | Plugin necə istifadə edir |
|---|---|---|---|---|
| Dobby.Host | Dobby.Core, Dobby.Contracts, Dobby.PluginFramework, Dobby.Security, Dobby.Licensing, Dobby.Configuration, Dobby.Localization, Dobby.Navigation, Dobby.Messaging, Dobby.Application, Dobby.Diagnostics, Dobby.Audit, Dobby.UI, Dobby.Shell, Dobby.Infrastructure | executable entry point only | evolving | no |
| Dobby.Core | none | Dobby.Host, Dobby.Contracts, Dobby.PluginFramework, Dobby.Security, Dobby.Licensing, Dobby.Configuration, Dobby.Localization, Dobby.Navigation, Dobby.Messaging, Dobby.Application, Dobby.Diagnostics, Dobby.Audit, Dobby.UI, Dobby.Shell, Dobby.SDK, Dobby.Admin; future/P2 Dobby.Update | stable | no |
| Dobby.Contracts | Dobby.Core (minimal only; mümkün qədər az) | Dobby.Host, Dobby.PluginFramework, Dobby.Security, Dobby.Licensing, Dobby.Configuration, Dobby.Localization, Dobby.Navigation, Dobby.Messaging, Dobby.Application, Dobby.Diagnostics, Dobby.Audit, Dobby.UI, Dobby.Shell, Dobby.SDK, Dobby.Admin, plugin-lər; future/P2 Dobby.Update | stable | contracts-only |
| Dobby.PluginFramework | Dobby.Core, Dobby.Contracts, Dobby.Security, Dobby.Licensing, Dobby.Configuration, Dobby.Messaging | Dobby.Host, Dobby.Shell, Dobby.Admin | evolving | no |
| Dobby.Security | Dobby.Core, Dobby.Contracts, Dobby.Configuration, Dobby.Infrastructure | Dobby.Host, Dobby.PluginFramework, Dobby.Licensing, Dobby.Application, Dobby.Shell, Dobby.Admin; future/P2 Dobby.Update | evolving | no |
| Dobby.Licensing | Dobby.Core, Dobby.Contracts, Dobby.Security | Dobby.Host, Dobby.PluginFramework, Dobby.Application, Dobby.Admin | evolving | no |
| Dobby.Configuration | Dobby.Core, Dobby.Contracts | Dobby.Host, Dobby.PluginFramework, Dobby.Security, Dobby.Application, Dobby.Shell, Dobby.Admin | evolving | no |
| Dobby.Localization | Dobby.Core, Dobby.Contracts | Dobby.Host, Dobby.Navigation, Dobby.UI, Dobby.Shell, Dobby.SDK, Dobby.Admin | stable | sdk-only |
| Dobby.Navigation | Dobby.Core, Dobby.Contracts, Dobby.Localization | Dobby.Shell, Dobby.Host, Dobby.Admin | evolving | no |
| Dobby.Messaging | Dobby.Core, Dobby.Contracts | Dobby.Host, Dobby.Application, Dobby.Shell, Dobby.PluginFramework | evolving | no |
| Dobby.Application | Dobby.Core, Dobby.Contracts, Dobby.Security, Dobby.Configuration, Dobby.Messaging, Dobby.Licensing, Dobby.Audit, Dobby.Diagnostics | Dobby.Host, Dobby.Shell, Dobby.Admin | evolving | no |
| Dobby.Diagnostics | Dobby.Core, Dobby.Contracts | Dobby.Host, Dobby.Application, Dobby.Shell, Dobby.Admin; future/P2 Dobby.Update | evolving | no |
| Dobby.Audit | Dobby.Core, Dobby.Contracts, Dobby.Infrastructure | Dobby.Host, Dobby.Application, Dobby.Admin | stable | no |
| Dobby.UI | Dobby.Core, Dobby.Contracts, Dobby.Localization | Dobby.Shell, Dobby.Host, Dobby.Admin | evolving | no |
| Dobby.Shell | Dobby.Core, Dobby.Contracts, Dobby.Navigation, Dobby.Application, Dobby.UI, Dobby.Localization, Dobby.Security, Dobby.PluginFramework, Dobby.Messaging | Dobby.Host | evolving | no |
| Dobby.SDK | Dobby.Core, Dobby.Contracts, Dobby.Localization | plugin-lər | evolving | sdk-only |
| Dobby.Update | Dobby.Core, Dobby.Contracts, Dobby.Diagnostics, Dobby.Security | none in Phase 1; future Dobby.Host in P2; not generated in P1 skeleton | experimental | no |
| Dobby.Admin | Dobby.Core, Dobby.Contracts, Dobby.PluginFramework, Dobby.Security, Dobby.Licensing, Dobby.Configuration, Dobby.Localization, Dobby.Navigation, Dobby.Application, Dobby.Diagnostics, Dobby.UI, Dobby.Audit | standalone admin executable | evolving | no |
| Dobby.Infrastructure | Dobby.Core, Dobby.Contracts | Dobby.Host, Dobby.Security, Dobby.Audit | evolving | no |

---

## Cədvəl 3 — Qərarlar / risklər

| Ad | Risk / Qeyd | Decision Needed? | Decision Blocker | ADR | Test strategiyası |
|---|---|---|---|---|---|
| Dobby.Contracts | `Dobby.Core`-a dependency minimal saxlanmalıdır; yoxsa contracts şişər və sərhəd pozular. | no | none | [ADR-0001 Contracts dependency boundary](adr/ADR-0001-contracts-dependency-boundary.md) | contract |
| Event Contracts | Internal host events, public integration events və UI-local events qarışdırılmamalıdır. Event visibility modeli ADR-0010-a fold edildi; ayrıca ADR-0002 yaradılmır. | no | none | [ADR-0010 Plugin communication policy](adr/ADR-0010-plugin-communication-policy.md) | contract + integration |
| Plugin Storage Contracts | Bring-your-own-storage within host boundary modeli contract-larda açıq yazılır. `SharedDomainStorage` və `UserScopedStorage` compartments və method surface ADR-0003/ADR-0030 ownership-i ilə bağlanıb. | no | none | [ADR-0003 Plugin storage boundary and access model](adr/ADR-0003-plugin-storage-boundary-and-access-model.md), [ADR-0030 Host persistence and plugin storage boundary](adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md) | contract + integration |
| Policy Enforcement Contracts | Capability-oriented `ICapabilityPolicy<TCapability>` pattern-i bağlandı; contract `Dobby.Contracts / Policy`-də, enforcement `Dobby.Security / Permissions / Policy`-də qalır; god-interface qadağandır. | no | none | [ADR-0004 Capability enforcement model](adr/ADR-0004-capability-enforcement-model.md) | contract |
| UI Composition Ordering Contracts | UI ordering contract-ları group + weight + stabil tiebreak default modelini düzgün ifadə etməlidir; Sidebar/Third Bar drag-and-drop override-ları və TopToolbar plugin ordering/hidden state P1-də per-user preference kimi saxlanır. | no | none | [ADR-0005 UI ordering model](adr/ADR-0005-ui-ordering-model.md) | contract + unit |
| UI Component Library | Row 72 Phase 1-də P1-core olaraq minimum host/built-in WPF implementation inventory verir. Inventory plugin-visible UI descriptor/contract deyil; plugin-visible UI surface lazım olarsa ayrıca `Dobby.Contracts`/`Dobby.SDK` qərarı ilə idarə olunur və bu implementation row-dan çıxmır. | no | none | inline WPF component shape; `docs/specs/design-system-assets.md` | unit |
| Plugin Service Registration Bridge | Plugin `IPluginModule.RegisterServices` surface-i, plugin lifetime singleton-ları plugin scope-unda qalır (host singleton deyil), extension point registration və contract override semantic-i bağlandı. | no | none | [ADR-0006 Plugin DI registration](adr/ADR-0006-plugin-di-registration.md) | integration |
| Assembly Isolation / Load Context Management | Hər plugin ayrı AssemblyLoadContext (isCollectible: false Phase 1); shared dependency-lər host ALC-dən pinned; plugin özəl dependency-lər plugin ALC-də qalır; unload P2-yə saxlanıldı. | no | none | [ADR-0007 Plugin load context strategy](adr/ADR-0007-plugin-load-context-strategy.md) | integration |
| Plugin Compatibility / Versioning | Phase 1 minimum davranışı bağlanıb: host ilə plugin arasındakı compatibility tələbi ödənməyəndə plugin activation-a buraxılmır. Exact manifest surface və version comparison semantics compatibility ADR-i ilə idarə olunur. | no | none | [ADR-0037 Plugin compatibility and versioning](adr/ADR-0037-plugin-compatibility-and-versioning.md) | contract + integration |
| Deferred Plugin Activation | Login ekranı görünənə qədər plugin discovery/manifest scan yoxdur; login-visible pre-auth mərhələdə yalnız cached metadata oxuna bilər; post-login explicit discovery/activation phase (dependency-sort + per-plugin activation loop), per-plugin failure isolation; hot-reload P2-yə saxlanıldı. | no | none | [ADR-0008 Deferred plugin activation](adr/ADR-0008-deferred-plugin-activation.md) | integration |
| Plugin Dependency Resolution | `dependsOn`, circular dependency, missing dependency və activation failure davranışı bağlanıb; skipped/failure semantics canonical-dir. | no | none | [ADR-0009 Plugin dependency graph](adr/ADR-0009-plugin-dependency-graph.md) | unit + integration |
| Plugin-to-Plugin Communication Policy | Pluginlər bir-biri ilə yalnız public contract/event üzərindən danışmalıdır; birbaşa reference qadağandır. | no | none | [ADR-0010 Plugin communication policy](adr/ADR-0010-plugin-communication-policy.md) | contract + integration |
| Plugin Isolation / Sandbox | Wrapper təkbaşına sandbox deyil; permission + scope + policy enforcement mütləq wrapper daxilində olmalıdır. P1 sandbox OS/process hard isolation deyil, host-controlled enforcement qatlarının birlikdə işləməsidir. | no | none | [ADR-0011 Sandbox enforcement principle](adr/ADR-0011-plugin-sandbox-enforcement-principle.md) | integration |
| Plugin Capability Boundary | Plugin-ə hansı host API surface göründüyü explicit capability boundary ilə bağlanıb; plugin host implementation service-lərinə birbaşa çıxmır, plugin page hosting official Contracts/SDK surface-lə məhdudlaşır və runtime fault plugin boundary-də isolate edilir. | no | none | [ADR-0012 Plugin capability boundary](adr/ADR-0012-plugin-capability-boundary.md) | contract + integration |
| Routing / Deep Link Handling | P1 navigation minimal route-to-view mapping shell və plugin pages üçün bağlanıb; external deep links P2/future qala bilər. Mapping `Dobby.Navigation`-dadır; hosting/gating `Dobby.Shell`-də, authorization decision `Dobby.Security`-də qalır. | no | none | [ADR-0013 Routing and deep links](adr/ADR-0013-routing-and-deep-link-model.md) | integration |
| Messaging UI Bridge | UI thread dispatch və notification routing public plugin API deyil; event visibility ADR-0010-da, UI-local dispatch/render davranışı shell UI spec-də bağlanır. Ayrıca ADR-0014 yaradılmır. | no | none | [ADR-0010 Plugin communication policy](adr/ADR-0010-plugin-communication-policy.md), [Shell UI spec](specs/shell-ui.md) | integration |
| Background Tasks / Process Reporting | Progress reporting modeli host-owned background operation contract-ı ilə shell process/status bar-a bağlanır; UI thread blocking qadağandır. | no | none | [ADR-0015 Background operation reporting](adr/ADR-0015-background-operation-reporting.md) | integration |
| Logging | Structured logging formatı, correlation id istifadəsi, local-only diagnostics sink və redaction floor bağlanıb. Exception handling və plugin boundary exception mapping `docs/04_RESULT_VS_EXCEPTION_POLICY.md` ilə uyğunlaşdırılır. | no | none | [ADR-0016 Logging strategy](adr/ADR-0016-logging-strategy.md) | integration |
| Observability | Metrics/traces Phase 1 release blocker deyil; startup/storage/plugin/license marker-ları diagnostics spec-də qalır, full span/metric/tracing layer P2-də ayrıca bağlanır. | yes | P2 design | P2 deferred — ADR-0017 Observability scope | integration |
| Health / Diagnostics | Hansı health check-lər runtime-da, hansıları admin görünüşündə olacaq ayrılmalıdır; P1 no-telemetry diagnostics export modeli ADR-0019/spec ilə bağlanıb, health checks P2 dizayn mövzusudur. | yes | P2 design | P2 deferred — ADR-0018 Health checks scope | integration + manual |
| Audit Trail | Audit log ilə technical log sərhədi qorunmalıdır; audit compliance yönümlüdür. Default telemetry yoxdur, diagnostics yalnız explicit local export package kimi çıxarıla bilər. | no | none | [ADR-0019 Audit vs diagnostics boundary](adr/ADR-0019-audit-vs-diagnostics-boundary.md) | integration |
| Feature Flags / Capability Settings | Feature flags P1-də licensing/authorization boundary-ni bypass etmir; tam rollout/config flag modeli P2-də bağlanır. Licensing hüququ, feature flag isə hüququ olan funksiyanın texniki aktiv/passivliyidir. | yes | P2 design | P2 deferred — ADR-0020 Feature flag model | unit + integration |
| Dobby.SDK | SDK convenience qatıdır və host implementation bypass yolu deyil; P1 allowed surface və dependency boundary bağlanıb. | no | none | [ADR-0021 SDK surface area](adr/ADR-0021-sdk-surface-area.md) | contract + integration |
| Host Update Mechanism | `Dobby.Update` project-i və host update flow-u P2 release minimumundadır; Phase 1-də yalnız manual local replacement path saxlanılır. İnternet əsaslı auto-update, signed self-update, rollback və release-wide update strategy P2-də ayrıca bağlanır. | yes | P2 release | P2 deferred — ADR-0022 Host update strategy | integration + manual |
| Dobby.Admin | Admin app ilə host arasında shared contract sərhədi qorunur; business logic duplication qadağandır. Phase 1-də `Dobby.Admin` project fazası `P1-core`-dur (minimal recovery signer/generator üçün, ADR-0039 asılılığı); tam admin UI composition `P1-if-time` qalır. | no | none | [ADR-0023 Admin-host boundary](adr/ADR-0023-admin-host-boundary.md) | integration |
| Selection / Bulk Operation State | Row 14 shell/internal WPF grid bulk selection state-inin sahibidir; cross-plugin selection state, persistence və serialization detalları plugin-visible surface-ə çıxarılarsa P2-də formal bağlanır. | yes | P2 design | P2 deferred — ADR-0024 Bulk selection ownership | unit |
| Querying Infrastructure + Search | Search ilə filter/sort/query eyni project-dədir, amma P1-də yalnız minimal/internal behavior lazımdır; geniş query/search boundary P2-də bağlanır. | yes | P2 design | P2 deferred — ADR-0025 Querying and search boundary | unit + integration |
| Dobby.Licensing | License Lifecycle v2 Phase 1-də mandatory customer/deployment/device/package binding, `pending`, `active`, `grace`, `readonly`, `denied` state-ləri, `Plugins > License` import flow-u və non-disclosing runtime mode surface ilə bağlanıb. | no | none | [ADR-0051 License lifecycle v2](adr/ADR-0051-license-lifecycle-v2.md), [ADR-0053 Capability surface runtime mode](adr/ADR-0053-capability-surface-runtime-mode.md), [License import spec](specs/license-import.md) | unit + integration |
| Dobby.Configuration | Environment-specific config, binding, validation, settings override, protected values və plugin config boundary modeli bağlanıb. | no | none | [ADR-0027 Configuration model](adr/ADR-0027-configuration-model.md) | unit + integration |
| Shell Composition Rendering | Ordering, group/weight qaydaları, TopToolbar center nav, single theme switch, profile button, Sidebar collapse, Third Bar soft panel cutout slot, bottom status/progress və minimized work strip render səviyyəsində yoxlanmalıdır. | no | none | [ADR-0028 Shell composition ordering render](adr/ADR-0028-shell-composition-ordering-render.md) | integration + snapshot |
| Correlation ID Ownership | Correlation id Phase 1-də `Dobby.Core` içində mövcud `ID / Key Generation` namespace-ində saxlanır; ayrıca correlation namespace-i açılmır. Minimum shape: `CorrelationId`, `ICorrelationIdGenerator`, `ICorrelationContextAccessor`. Generator yalnız yeni scope-da işləyir, mövcud correlation id-ni dəyişmir, async-flow uyğun daşınır, background task-a explicit ötürülür və scope bitəndə təmizlənir. | no | none | [ADR-0029 Correlation ID ownership](adr/ADR-0029-correlation-id-ownership.md) | unit + integration |
| Host Persistence Ownership and Plugin Storage Boundary | `Dobby.Storage` bu düzəlişdə ayrıca project kimi əlavə edilmir. Host persistence daxildə qalır; storage taxonomy 3 bucket-dir: host data, plugin shared domain data və plugin user-scoped data. Plugin storage access yalnız `SharedDomainStorage` və `UserScopedStorage` contract/capability səthlərindən keçir; default fallback yoxdur. SDK wrapper yeni storage capability yaratmır, yalnız mövcud contract-ların rahat istifadəsini verir. | no | none | [ADR-0030 Host persistence and plugin storage boundary](adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md) | contract |
| Session Lifecycle and Refresh | `active session` host tərəfindən etibarlı sayılan current authenticated session-dır. Phase 1-də session refresh yoxdur; invalid/expired session authenticated context-i təmizləyir, session state-i etibarsız sayır, token varsa onu da etibarsız sayır və user-i login flow-a qaytarır. Logout session və in-memory user state cleanup edir; lock/manual lock/auto-lock isə current session-u saxlayıb password unlock və ya logout seçimi verir. Self-service password change cari session-u saxlayır, başqa user üçün admin reset isə həmin user-in mövcud session-ını invalid edir. Remember-me session refresh deyil və ADR-0038-də pre-auth masked-fill rahatlığı kimi tənzimlənir. Expiry trigger semantics ayrıca ADR-0031-də bağlıdır. | no | none | [ADR-0031 Session lifecycle and refresh](adr/ADR-0031-session-lifecycle-and-refresh.md) | unit + integration |
| Plugin Signature Trust Model | Plugin manifest/package signature doğrulaması plugin yükləmə və activation üçün Phase 1 blocker-i idi; canonical trust model bağlanıb, unsigned və ya untrusted plugin activation-a buraxılmır. | no | none | [ADR-0032 Plugin signature trust model](adr/ADR-0032-plugin-signature-trust-model.md) | integration |
| Initial UI Culture and Fallback Chain | Phase 1 default culture `az-AZ`-dir; `en-US` və `tr-TR` resource skeleton-ları future language readiness üçün saxlanılır, runtime language switch P2-yə qalır. | no | none | [ADR-0043 Initial UI culture and fallback chain](adr/ADR-0043-initial-ui-culture-and-fallback-chain.md) | integration + static-scan |
| Localization Resource Organization | Localization resource ownership namespace-based model ilə bağlanır; host resources `Dobby.Localization` altında, plugin resources plugin assembly-lərində qalır. | no | none | [ADR-0044 Localization resource organization](adr/ADR-0044-localization-resource-organization.md) | contract + integration |
| Plugin Package and License Artifact Boundary | `.dobbyplugin` package deterministic manifest signature/files section modelini, canonical signed content fingerprint-i və detached `.dobbylicense` artifact ayrılığını saxlayır; forbidden package content reject edilir. | no | none | [ADR-0045 Plugin package and license artifact boundary](adr/ADR-0045-plugin-package-and-license-artifact-boundary.md) | integration |
| UI and Code Terminology Mapping | User-facing terminology canonical UI/code mapping-ə tabedir; Azerbaijani UI üçün `Permission` termini default `İcazə` kimi bağlanır. | no | none | [ADR-0046 UI and code terminology mapping](adr/ADR-0046-ui-and-code-terminology-mapping.md) | static-scan + manual |
| Host Installer and Portable Artifact Policy | Customer deployment üçün MSIX primary, MSI fallback saxlanır; portable artifact yalnız support/dev/offline test üçündür və release blocker deyil. | no | none | [ADR-0047 Host installer and portable artifact policy](adr/ADR-0047-host-installer-and-portable-artifact-policy.md) | manual |
| Operation-Based Authorization | P1 permission-ları host-owned target registry, canonical verb list və typed `OperationPermissionCapability` ilə evaluate olunur; built-in target-lər host tərəfindən, plugin target-lər isə validated signed manifest `operationTargets` metadata-sından registry-yə düşür. Operation key forması `<targetId>.<verb>`-dir və parser son nöqtədən split edir, explicit deny toggle yoxdur və unknown target/verb fail-closed deny + audit warning yaradır. Built-in `plugins.license.import` operation-u license import flow-unu guard edir. | no | none | [ADR-0048 Operation-based authorization](adr/ADR-0048-operation-based-authorization.md), [License import spec](specs/license-import.md) | integration |
| Hierarchical Navigation Contribution Model | Navigation descriptors parent/child hierarchy və per-parent ordering daşıyır; active plugin top-level entries TopToolbar-da, selected child-lar Sidebar/Third Bar-da render olunur və `ADR-0036` closed target vocabulary saxlanılır. | no | none | [ADR-0049 Hierarchical navigation contribution model](adr/ADR-0049-hierarchical-navigation-contribution-model.md) | snapshot + integration |
| In-Memory Work Sessions | Phase 1 minimumu browser-style tab chrome deyil; user başladığı canlı iş ekranını explicit minimize/park edib başqa işə keçə və sonra eyni ekran vəziyyətini restore edə bilməlidir. Silent auto-minimize yoxdur; yarımçıq işdən keçiddə warning seçimləri verilir. Restart sonrası host restore P2-dir, real/draft/domain data plugin storage məsuliyyətində qalır. | no | none | [ADR-0050 In-memory work sessions](adr/ADR-0050-in-memory-work-sessions.md), [Work sessions spec](specs/work-sessions.md) | integration + snapshot |
| Customer/Device/Package Binding | Plugin license customer/deployment/device/package binding ilə enforce olunur; customer/deployment context v2 və License v2 köhnə optional/no-op qərarları əvəz edir. | no | none | [ADR-0051 License lifecycle v2](adr/ADR-0051-license-lifecycle-v2.md), [ADR-0052 Tenant/customer context v2](adr/ADR-0052-tenant-customer-context-v2.md) | integration |
| Read-Only Expired Mode | Expired plugin `ReadOnlyExpired` mode-da köhnə nəticələri göstərir, mutating/extraction/report/file-output/background start və plugin/domain import əməliyyatları host tərəfindən fail-closed deny edilir; `.dobbylicense` import `Plugins > License` host-level recovery flow-u kimi permission yoxlaması ilə mümkün qalır. | no | none | [ADR-0051 License lifecycle v2](adr/ADR-0051-license-lifecycle-v2.md), [ADR-0053 Capability surface runtime mode](adr/ADR-0053-capability-surface-runtime-mode.md), [License import spec](specs/license-import.md) | integration |
| Capability Surface Runtime Mode | `IsFeatureAllowed(string)` surface-i `FullAccess`/`ReadOnlyExpired`/`Denied` kimi non-disclosing runtime mode modeli ilə əvəz olunur. | no | none | [ADR-0053 Capability surface runtime mode](adr/ADR-0053-capability-surface-runtime-mode.md) | contract |
| UI Permission Term (Default Confirmed) | Azerbaijani UI-da `Permission` termini `İcazə` kimi canonical qalır; Owner gələcəkdə `Yetki` seçərsə ayrıca wording update tələb olunur. | no | none | [ADR-0046 UI and code terminology mapping](adr/ADR-0046-ui-and-code-terminology-mapping.md) | static-scan + manual |
| Signing Key Ownership | Vendor master signing key ownership, public-key provisioning, build-time dev/QA trust profile, `dobby.dev-qa` vendor id və Phase 1 manual rotation cavabı minimum scope-da bağlanıb. | no | none | [ADR-0033 Signing key ownership](adr/ADR-0033-signing-key-ownership.md) | manual + static-scan |
| Secure Storage Boundary | Local persistence data-at-rest qorunur; business data field-level şifrələnmir, yalnız password/token/key/remember-me/recovery/session secrets əlavə protected-storage qoruması alır. DB açarı istifadəçidən əl ilə istənilmir və təkcə `deviceId`-dən törəmir. | no | none | [ADR-0030 Host persistence and plugin storage boundary](adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md), [ADR-0040 Internal host adapters boundary](adr/ADR-0040-internal-host-adapters-boundary.md) | integration |
| Tenant / Customer Context Scope | Phase 1 single active customer/deployment context, `CustomerDeploymentContext` pre-login table və `default` tenant storage primitive-i ilə bağlandı. Multi-customer switching P2-dir, amma license customer/deployment binding P1-də enforce olunur. | no | none | [ADR-0052 Tenant/customer context v2](adr/ADR-0052-tenant-customer-context-v2.md), [Storage and data protection spec](specs/storage-and-data-protection.md) | unit + integration |
| Plugin Manifest Schema | Manifest schema typed declarative modeldir: `dependsOn = { pluginId, minVersion }`, `capabilities = { name, scope }`, `operationTargets = { id, verbs, labelKey }`, `extensionPoints = { id, target, routeKey, labelKey?, parentId?, group?, weight? }`. `capabilities.scope` host-defined closed set-dir; Phase 1 vocabulary yalnız `shell`, `plugin-page`, `background-task`. `operationTargets` validated signed manifest metadata-dan permission registry-ni seed edir və `capabilities` ilə qarışdırılmır. `extensionPoints.target` də host-defined closed set-dir; Phase 1 vocabulary yalnız `top-toolbar`, `sidebar`, `third-bar`, `plugin-page-region`; `routeKey` ADR-0013 route identity-yə, `parentId` isə ADR-0049 hierarchical navigation semantics-inə tabedir. `labelKey` non-`top-toolbar` targets üçün schema tərəfindən required olur; target-specific required field qaydaları schema/ADR-0036-da qalır. | no | none | [ADR-0036 Plugin manifest schema](adr/ADR-0036-plugin-manifest-schema.md), [ADR-0048 Operation-based authorization](adr/ADR-0048-operation-based-authorization.md) | contract + integration |
| Owner Password Recovery Flow | Vendor-signed, device-bound (5-hash composite, ≥3 match), 24 saatlıq müddətli və tək istifadəli artefakt modeli; müştəri-vendor mübadiləsi fayl əsaslıdır; imzalama vendor master key ilə paylaşılır (ADR-0033 scope). | no | none | [ADR-0039 Owner password recovery](adr/ADR-0039-owner-password-recovery.md) | integration + manual |
| Dobby.Infrastructure Boundary | Host-internal Win32/OS adapter qatı (`Dobby.Infrastructure` Row 87) plugin sərhədinə sızmır; hardware identity query surface (Row 88) və consumed recovery nonce persistence (Row 89) bu boundary-nin minimum Phase 1 responsibility-ləridir. `HD-008` / `DM-095` per-row ownership modelinə görə owner project-lər host-internal behavior və use-case-ləri saxlayır, `Dobby.Infrastructure` isə primitive/backing adapter layer-i kimi qalır. Direct owner-to-infrastructure edge-lər yalnız bu dependency table-da explicit görünəndə açıqdır. | no | none | [ADR-0040 Internal host adapters boundary](adr/ADR-0040-internal-host-adapters-boundary.md) | integration |
| Login-First Startup Policy | Login ekranı görünənə qədər qadağan olunan ağır işlər canonical closed set kimi sənədləşdirilib; `IHostedService` və digər heavy background work yalnız post-login açılır. Ölçmə və release gate-lər NFR və acceptance sənədləri ilə birlikdə işləyir. | no | none | [ADR-0041 Login-first startup policy](adr/ADR-0041-login-first-startup-policy.md) | integration + unit |

---

## Phase 2 / Phase 3 / P1-if-time Future Scope

P1 release-blocker decision-larından sonra qalan deferred mövzuların canonical navigation indexidir.

| Mövzu | Phase | Anchor / Hədəf doc |
|---|---|---|
| Dark theme visual reference / derived palette | P1-if-time | resolved; canonical model in `docs/specs/design-system-assets.md` §9.1 and `docs/specs/shell-ui.md` §6 |
| Home empty/favorites polish | P1-if-time | `FR-006`, `AC-011` |
| Shared UI infrastructure (dialog, notification, forms, grid) | P1-if-time | `NFR-012`, `AC-013` |
| Richer process reporting | P1-if-time | `NFR-012`, `AC-012` |
| Full Admin Tool UI pull-forward | P1-if-time | bax: P2 row (canonical phase) |
| Full Admin Tool UI | P2 | `ADR-0023`, `docs/specs/admin-tool.md` |
| Backup / restore / migration | P2 | `FR-020`, `NFR-018`, `docs/specs/backup-restore-migration.md` |
| Cross-device restore sub-specs | P2 | admin-tool spec sub-specs |
| Plugin update / repair / downgrade | P2 | `ADR-0037`, `docs/specs/plugins-manager.md` extension — version compatibility validation, plugin data preservation (shared domain + user-scoped storage) və failed-update rollback bu sətr daxilində bağlanır |
| Out-of-process plugin worker / hardened process isolation | P2 | `ADR-0007` — yalnız arbitrary/user-installable və ya untrusted plugin modeli açılarsa, ALC fault isolation acceptance-ləri ödəməzsə, native dependency/process permission boundary lazım olarsa və ya customer security policy process isolation tələb edərsə yenidən qiymətləndirilir |
| Shared Data Tools | P2 | `docs/specs/shared-data-tools.md` |
| Owner transfer / replacement | P2 | admin-tool spec, Owner protection invariants |
| Host app update | P2 | `ADR-0047` |
| Audit viewer / advanced diagnostics UI | P2 | `ADR-0019` audit viewer phase, `docs/specs/audit-and-diagnostics.md` extension |
| Audit retention policy extensions | P2 | `docs/specs/audit-and-diagnostics.md` extension və ya yeni `docs/specs/data-retention.md` — P1 365-day minimum floor saxlanılır; P2 yalnız configurable default retention, cleanup scheduler ownership və advanced enforcement axınını dərinləşdirir |
| Plugin/host critical-flow metrics and traces layer | P2 | Existing Diagnostics Markers (`docs/specs/audit-and-diagnostics.md`) təkrarlanmadan startup, shell init/composition, plugin discovery/loading/activation, license verification/enforcement/import, backup/restore və P2 shared-data preview/import/mapping/validation/export üçün future span/metric/tracing layer-i |
| Backup restore audit immutability | P2 | `docs/specs/backup-restore-migration.md` — backup restore zamanı `AuditLog` overwrite olunmur, mövcud audit tarixçəsi qorunur |
| Runtime language switching | P2 | `ADR-0043` |
| Multi-customer / tenant switching | P2 | `ADR-0052` |
| Home dashboard contribution | P3 | `ADR-0049` extension, yeni Home contribution spec |
| Full multi-language UI + RTL | P3 | `ADR-0043`, `ADR-0044` |
| Full accessibility / WCAG / screen reader polish | P3 | `NFR-019`, `AC-027` baseline -> P3 polish |
| Lock screen visual / UX polish | P3 | `docs/specs/session-lifecycle.md` extension |
| Local marketplace / catalog | P3 | `docs/specs/plugins-manager.md` extension |
| Collaborative drafts / shared work-in-progress | P3 | `ADR-0030`, plugin storage contract |
| Layout settings (advanced) | P3 | `docs/specs/settings-and-about.md` extension |
| Advanced theme palette / per-plugin theme | P3 | `docs/specs/shell-ui.md` extension |
| Plugin tab behavior preference | P3 | `docs/specs/work-sessions.md` / `docs/specs/shell-ui.md` extension |
| Advanced auto-lock UI | P3 | `docs/specs/session-lifecycle.md` / `docs/specs/settings-and-about.md` extension |

---

## Engineering Conventions / Boundary Rules

**Convention Inclusion Rule:**
Yalnız konkret səhvin, yanlış dependency-nin, təhlükəsizlik boşluğunun, uyğunsuz implementasiyanın və ya komanda içi qarışıqlığın qarşısını alan qaydalar burada saxlanılır. Ümumi, dumanlı və yalnız “nice to have” tipli cümlələr bu bölməyə daxil edilmir.

- **Qızıl Asılılıq Sərhədi:** Plugin yalnız `Dobby.Contracts` və `Dobby.SDK` görür. Plugin heç bir runtime implementation layihəsinə birbaşa referans almır.
- **Plugin-facing Surface Qaydası:** Plugin-facing yeni surface yalnız `Dobby.Contracts` və ya `Dobby.SDK` altında ola bilər. Plugin page/main-content hosting də host-neutral descriptor/contract modeli ilə gedir; WPF/internal shell implementation type-ları plugin-facing contract deyil.
- **SDK və UI Sərhədi:** `Dobby.SDK` `Dobby.UI` project-inə referans almır. Plugin-facing UI descriptor/extension surface yalnız `Dobby.Contracts` və ya `Dobby.SDK` altında qalır; `Dobby.UI` WPF/shared component implementation qatıdır və plugin-visible contract sayılmır.
- **SDK-nın Təbiəti:** SDK convenience qatıdır, runtime implementation bypass yolu deyil.
- **Konfiqurasiya Gizliliyi:** Plugin öz settings-lərinə yalnız `Dobby.Contracts` və ya SDK üzərindən çatır. `Dobby.Configuration` implementasiyası tamamilə gizli qalır.
- **Storage Sərhədi:** Plugin storage access yalnız host-controlled storage contract/capability üzərindən verilir; SDK yalnız convenience wrapper verə bilər və yeni storage capability yaratmır. Canonical compartments `SharedDomainStorage` və `UserScopedStorage`-dır; host data plugin storage sayılmır. Host persistence daxildə qalır və plugin contract-a sızmır.
- **Shared Mapping Profile Sərhədi:** Shared mapping profile-lar host-owned record-lardır və authenticated plugin context-dən çıxarılan caller plugin identity ilə bind olunur. Plugin `callerPluginId`-ni input kimi diktə etmir; field-level profile schema və `canonicalFieldVersion` detalları future `docs/specs/shared-data-tools.md` P2 spec-inə saxlanılır.
- **Local Data Protection:** Host və plugin local persistence faylları data-at-rest qorunur. Business data field-level şifrələnmir; yalnız password/token/key/remember-me/recovery/session secrets əlavə protected-storage qoruması alır. `deviceId` encryption key mənbəyi deyil.
- **Package/Storage Boundary:** Plugin package host persistence migration script-i daşımır. Host persistence schema migration ownership-i host-internal qalır və plugin package boundary-sinə sızmır.
- **Package Verification Boundary:** Plugin package verification tolerant deyil: unsigned extra file, manifest/package missing entry, hash mismatch, duplicate/case-collision, symlink, forbidden path construct və raw-container fingerprint dependency reject edilir.
- **Admin DRY Qaydası:** `Dobby.Admin` host-un biznes məntiqini kopyalamır; shared behavior-lar `Contracts` və ya `Application` səviyyəsində reuse edilir.
- **Admin-in Təbiəti:** `Dobby.Admin` ayrıca UI-dir, ayrıca domain deyil.
- **Event Visibility (3 səviyyə):** Event-lər `internal host events`, `public integration events` və `UI-local events` olaraq qəti şəkildə ayrılır.
- **Plugin-to-Plugin Əlaqə:** Pluginlər bir-birinə birbaşa referans vermir, əlaqə yalnız public contract və `IntegrationEvent` üzərindən qurulur.
  *Nümunə axın:* Plugin A `CustomerChangedIntegrationEvent` publish edir → `Dobby.Messaging` bunu qəbul edib paylayır → Plugin B subscribe olduğu üçün məlumatı alıb UI-nı yeniləyir.
- **Plugin Performance Surface:** Plugin host-dan kənar ayrıca metric/timing channel açmır, ayrı log file yazmır və host measurement contract-ını bypass etmir. Plugin daxilində `Stopwatch` və ya local timing istifadəsi qadağan deyil; qadağa plugin-in host-un verdiyi `IProgress<T>` / `ILogger<T>` / shared measurement abstraction-ları əvəzinə paralel kənar observability surface-i yaratmasıdır.
- **Qavramsal Ayrım (Licensing vs. Feature Flags):** Licensing müştərinin hüququ olub-olmadığını yoxlayır. Feature Flag isə hüququ olan funksiyanın texniki olaraq aktiv/passivliyini idarə edir. Tenant/Customer context bu hüquq və flag-ların hansı scope-da tətbiq olunduğunu müəyyən edir.
- **Licensing Surface Sərhədi:** Pluginə raw licensing surface verilmir; yalnız non-disclosing runtime mode (`FullAccess`, `ReadOnlyExpired`, `Denied`) görünə bilər.
- **License Import Surface:** `.dobbylicense` import-un canonical yeri `Plugins > License`-dır; Plugin Details shortcut-u eyni flow-a gedir, About import yeri deyil və import `plugins.license.import` permission-u tələb edir.
- **License Binding Enforcement:** Customer, deployment, device və package fingerprint binding host tərəfində enforce olunur; mismatch plugin-ə detal sızdırmayan deny kimi görünür, audit-də isə host-side səbəb saxlanılır.
- **Read-Only Expired Enforcement:** Read-only expired mode UI-only deyil; storage write, command/action, background task, plugin/domain import/export/report/file output və print target sərhədləri fail-closed işləyir; `.dobbylicense` import host-level recovery flow kimi ayrıca qalır və permission yoxlamasını bypass etmir.
- **Correlation ID Ownership:** Correlation ID `Dobby.Core` içində mövcud `ID / Key Generation` altında qalır; ayrıca correlation namespace-i açılmır.
- **Correlation Context Lifecycle:** Correlation generator mövcud id-ni overwrite etmir; context async-flow uyğun daşınır, background task-a explicit ötürülür və scope bitəndə təmizlənir.
- **Session Lifecycle Minimumu:** Phase 1-də session refresh yoxdur; invalid/expired session user-i login flow-a qaytarır. Logout current authenticated context və in-memory user state-i təmizləyir, lock isə current session-u saxlayıb unlock/logout seçimi verir. Remember-me session refresh deyil, ADR-0038-də pre-auth masked-fill rahatlığı kimi tənzimlənir.
- **Sandbox Enforcement:** Wrapper obyekti təkbaşına sandbox deyil; sandbox həmin wrapper daxilindəki permission və policy enforcement-dir.
- **Capability Boundary vs Sandbox:** Capability Boundary nəyin görünəcəyini müəyyən edir; Sandbox isə görünənin necə istifadə ediləcəyini enforce edir.
- **Capability Access Sərhədi:** Capability access yalnız host-controlled boundary üzərindən verilir.
- **Plugin Runtime Fault Isolation:** Plugin screen/task/lifecycle hook fault-u host process failure deyil; fault həmin plugin instance/screen/task sərhədində isolate edilir, user non-technical error görür və raw detail diagnostics/audit boundary-də qalır.
- **Sample Plugin Verification Artifact:** Sample plugin platform release gate-lərini (manifest, signed install, Main Content hosting, permission, Result boundary, fault isolation, work session) yoxlamaq üçün QA/dev test artifact-ıdır; production trust model-i ilə qarışmır.
- **Policy Contract Forması:** Policy enforcement contract-ları capability yönümlü dizayn olunmalıdır; async ehtiyacı olan hallarda async model əsas götürülür.
- **Operation Authorization Forması:** User permission-ları string-only god-interface kimi yox, `OperationPermissionCapability` typed payload-u və `ICapabilityPolicy<TCapability>` pattern-i ilə evaluate olunur. Operation key string-i `<targetId>.<verb>` storage/UI vocabulary-dir, enforcement contract forması deyil; verb yalnız canonical permission list-dən seçilir.
- **User Management Guard:** User Management də registry target-dir; Owner protected principal kimi qalır. Managed user Owner-i deactivate edə, Owner məlumat/status/permission-larını dəyişə, özünə permission əlavə edə və ya özündə olmayan permission-u başqasına verə bilməz.
- **UI Extension Ordering:** UI extension-ların sıralaması təsadüfi deyil; mütləq `group + weight + stable tiebreak` qaydası ilə işləyir.
- **Shell UI Baseline:** Shell TopToolbar, Sidebar, Third Bar, Main Content Area, bottom status/progress, profile menu və minimized work strip səthlərini daşıyır; TopToolbar-da bir theme switch var.
- **Settings / About Surface:** Settings ownership host shell-də qalır və authenticated category tree (`Appearance`, `Account`, `Security / Session`, `User Management`, `About`) daşıyır; About license import/status surface-i deyil; `User Management` subpage permission-gated entry-dir; Settings TopToolbar quick switch ilə eyni theme preference-i paylaşır.
- **Localization Diagnostics:** Missing localization key compliance audit deyil; technical diagnostics event/log kimi `Dobby.Diagnostics` altında görünür. Audit yalnız user/security/compliance yönümlü hadisələr üçündür.
- **No Telemetry Default:** Dobby usage analytics, crash report, telemetry və diagnostics-i vendor-a avtomatik upload etmir. Diagnostics export yalnız explicit user/Owner action ilə local support package kimi yaranır və secrets/raw license detail daşımır.
- **Audit Visibility Boundary:** Audit data P1-də yazılır, amma audit viewer UI P1 blocker deyil. Audit log-a baxış ayrıca permission/capability ilə idarə olunur və diagnostics raw detail-i audit viewer-ə qarışdırılmır.
- **Hierarchical Navigation Ordering:** Hierarchical navigation descriptor-larında `group + weight + stable tiebreak` hər parent node-un child list-inə ayrıca tətbiq olunur.
- **Accessibility Minimumu:** Shell və built-in ekranlar keyboard navigation, visible focus indicator, contrast və role/label baseline saxlamalıdır; full WCAG/screen reader polish P3-dür.
- **Route Authorization Boundary:** Route mapping `Dobby.Navigation`-dadır; route hosting və gating `Dobby.Shell`-də orkestr olunur; actual authorization decision və enforcement `Dobby.Security`-də qalır. `Dobby.Navigation` və `Dobby.Shell` authorization məntiqini duplicate etmir — yalnız `Dobby.Security`-yə sorğu verib nəticəni tətbiq edir.
- **Work Session Boundary:** In-memory work session browser-style tab chrome deyil; `Dobby.Shell` yalnız post-login canlı iş ekranının process-lifetime explicit minimize/restore orchestration-ını saxlayır. Silent auto-minimize yoxdur; restart restore və persisted workspace P2-dir, domain data plugin storage məsuliyyətində qalır.
- **Manifest Typed Model:** Plugin manifestində `dependsOn`, `capabilities`, `operationTargets` və `extensionPoints` loose string siyahısı deyil, typed declarative modeldir. `capabilities.scope` host-defined closed set-dir və Phase 1-də yalnız `shell`, `plugin-page`, `background-task` dəyərlərini qəbul edir. `operationTargets` shape-i `{ id, verbs, labelKey }` formasındadır və validated signed manifest metadata kimi permission registry-ni plugin assembly load-dan əvvəl seed edir. `extensionPoints` shape-i `{ id, target, routeKey, labelKey?, parentId?, group?, weight? }` formasındadır; `target` host-defined closed set-dir və Phase 1-də yalnız `top-toolbar`, `sidebar`, `third-bar`, `plugin-page-region` dəyərlərini qəbul edir. `labelKey`, `parentId`, `group` və `weight` optionality-si `ADR-0036`-dakı manifest validation qaydası ilə idarə olunur.
- **Repository Layout Ownership:** Physical skeleton path-ləri və evidence-split test project layout-u `docs/09_BUILD_RUN_AND_RELEASE.md` `Repository / Solution Layout` section-ının canonical responsibility-sidir; architecture map project/namespace ownership-u saxlayır, filesystem layout-u duplicate etmir.
- **Plugin Aktivasiyası:** Plugin activation startup-dan ayrıdır; deferred activation və dependency resolution ayrıca sərt qaydadır.
- **Core Public Surface Stability:** `Dobby.Core`-un yalnız `Dobby.Contracts` və ya `Dobby.SDK` üzərindən plugin-ə transitive görünən, ADR-0001-də qapalı şəkildə sadalanan tipləri (`Result` / `Result<T>`, well-known error code types və registry-si, `CorrelationId`, `ICorrelationIdGenerator`, `ICorrelationContextAccessor`, BCL `TimeProvider`, `HostVersion`, `IHostVersionProvider`) Contracts ilə eyni stabillik səviyyəsində qəbul olunur; bu dar səthdəki breaking change Contracts breaking change-i kimi idarə olunur. `Dobby.Core`-un plugin-ə transitive çıxmayan internal hissələri bu qaydaya tabe deyil.
- **Error Handling Contract:** Expected failure-lar `Result<T>` ilə, programmer/invariant error-lar exception ilə ifadə olunur; infrastructure exception-lar ilk Dobby boundary-də loglanıb `Result<T>`-ə çevrilir. `Result<T>` qaytaran public metod unexpected exception sızdırmır. Tam policy: `docs/04_RESULT_VS_EXCEPTION_POLICY.md`.
- **Plugin Boundary Exception Seal:** Plugin-facing surface 100% `Result<T>` qaytarır; raw exception plugin-host sərhədini keçmir. Boundary wrapper exception-ları typed error code-lara çevirir (`PluginInternalError`, `Cancelled`, `InvalidInput`, `UnsupportedOperation` və s.).
- **Cancellation and Corrupted-State:** `OperationCanceledException` internal flow-da bubble ola bilər; public/plugin boundary-də `ErrorCode.Cancelled` ilə `Result<T>`-ə çevrilir. `OutOfMemoryException` və `StackOverflowException` tutulmur.
- **Baza Qatların Təmizliyi:** `Dobby.Contracts` mümkün qədər minimal və stabil saxlanır. `Dobby.Core` dumping ground olmamalıdır; iç alt bölmələri sərt saxlanmalıdır.
- **Target Framework Sərhədi:** Plugin-facing `Dobby.Contracts` və `Dobby.SDK` layihələri Windows-specific TFM-ə (`net10.0-windows`) bağlanmır; `net10.0` (platform-neutral) qalır. Windows/WPF-facing `Dobby.Host`, `Dobby.UI`, `Dobby.Shell` və `Dobby.Admin` layihələri `net10.0-windows` daşıyır. Test projects hədəf aldıqları runtime boundary-ni mirror edir. Canonical TFM split `docs/03_TECH_STACK_AND_VERSIONS.md` Target Framework section-ındadır; bu boundary contracts/core layihələrinin gələcəkdə platformasız re-host edilə bilməsini açıq saxlayır.
- **Entity / DTO Sərhədi:** Persistence entity-ləri service, UI və plugin-visible boundary-ləri keçmir. Service-lər entity-ni immutable DTO/read model-ə map edir və yalnız həmin boundary model-ləri qaytarır.
- **Startup Single-Instance Sərhədi:** Host single-instance coordinator-u login UI göstərilməzdən əvvəl acquire edir. Pre-login iş login-visible closed set ilə məhdud qalır və UI/service consumer-lər direct SQLite/Dapper çağırmır; repository və application-service contract-ları üzərindən gedir.
- **Background Operation Sərhədi:** Uzun host əməliyyatları UI thread-də və ya ViewModel içində ad-hoc `Task.Run` kimi idarə olunmur. Enqueue, progress, cancellation, retry/failure status və correlation propagation host-owned background operation contract-ından keçir; shell yalnız həmin contract-ın verdiyi status/progress modelini render edir.

### Gələcək üçün qeydlər

- Gələcəkdə sistem böyüdükcə `Dobby.UI`, `Dobby.SDK` və `Dobby.Localization` layihələrində “Public Surface” və “Internal Implementation” fərqi fiziki layihələrə bölünə bilər.

## Storage / Deployment Mode Addendum

`ADR-0056` Dobby deployment mode-larını iki canonical formaya bölür:
Standalone Installation local encrypted SQLite authority-dir; Branch Shared
Installation eyni branch LAN daxilində PostgreSQL authority-dir və hər cihazda
DeviceLocal protected profile/secrets saxlayır.

`Dobby.BranchSetup` və `Dobby.Infrastructure.Postgres` architecture map-də yalnız
deferred-future project row kimi qeyd edilə bilər. Mərhələ 1 sənəd pass-i bu
physical path-ləri yaratmır və skeleton implementation başlatmır.

`Dobby.Admin` vendor-side admin/signing tool olaraq qalır. Branch Shared setup
context-i customer-side `Dobby.BranchSetup.exe` kimi ayrıca gələcək executable
boundary-dir və `Dobby.Host` runtime ilə qarışdırılmır.

Plugin storage contracts deployment topology-dən asılı deyil. Branch Shared
mode-da plugin-visible `StorageScope` manifest/API field-i əlavə edilmir; host
provider seçimini və DeviceLocal/BranchShared mapping-i öz boundary-sində
saxlayır.
