# ADR-0012: Plugin Capability Boundary

**Status:** Accepted (security-boundary and hostile-code containment claims amended by `ADR-0059` — see ## Revision History)
**Date:** 2026-04-24

**Licensing surface note:** Bu ADR-in capability boundary və non-disclosing prinsipi
qüvvədə qalır. Licensing üçün binary `allowed / denied` nümunəsi `ADR-0053`
runtime mode surface-i ilə əvəz olunub.

## Context

`02_ARCHITECTURE_MAP.md` Row 41 (`Plugin Capability Boundary`) və Cədvəl 3 `Plugin Capability Boundary` sətri plugin-ə hansı host API surface görünür sualını Phase 1 blocker kimi göstərir. Engineering convention-lar `Plugin Trust Enforcement` və `Capability Boundary` ikisinin fərqini açıq yazır: capability boundary nəyin görünəcəyini müəyyən edir, policy/authorization enforcement isə görünənin necə istifadə ediləcəyini məhdudlaşdırır. Heç bir qat in-process plugin üçün hostile-code containment iddiası daşımır (`ADR-0059`).

`01_PRODUCT_SCOPE.md` licensing-in plugin-ə raw license detail verməməsini və yalnız non-disclosing runtime mode göstərməsini tələb edir. Bu həm licensing, həm də digər host capability-lər üçün `non-disclosing surface` prinsipini qoyur.

Bu ADR plugin capability boundary-ni rəsmiləşdirir.

## Decision

Phase 1 plugin capability boundary modeli:

### 1. Capability = explicit plugin-visible host surface

- Capability host-un plugin-ə verdiyi dar və explicit API surface-dir.
- Plugin-visible capability-lər yalnız `Dobby.Contracts` və ya `Dobby.SDK` altında elan olunur; host implementation project-ləri capability surface sayılmır.
- Plugin manifest-də capability ehtiyacı `ADR-0036` `capabilities = { name, scope }` modeli ilə elan olunur.

### 2. Capability access host-controlled olur

- Plugin capability access-i host tərəfindən verilir və yoxlanılır; plugin özbaşına service discovery və ya internal host service resolution etmir.
- `IServiceProvider`, container və internal runtime service-lər plugin-ə açıq verilmir.
- Plugin host service resolution yolu ilə host-un dəstəklədiyindən başqa type-a çatmır: plugin service scope host-dan yalnız §2a-dakı qapalı type list-i üzrə service ala bilər. List-də olmayan type üçün resolution uğursuz olur və həmin type-ı constructor-unda tələb edən plugin service instance-ı yaradılmır. Bu, DI/resolution surface haqqında qərardır; in-process fully trusted kod üçün reflection-level reachability qadağa altında deyil və bu ADR onu containment sərhədi kimi iddia etmir (`ADR-0059` trust posture).
- Manifest `capabilities[]` array-i **declaration/review artefaktıdır, runtime resolution gate deyil.** O canonical package fingerprint-ə daxil olduğu üçün signature-bound və tamper-evident-dir, admin package verification output-unda human review üçün göstərilir və install zamanı shape baxımından validate olunur (`name` non-empty unique identifier, `scope` yalnız `shell` / `plugin-page` / `background-task`). Capability adı elan etmək plugin-ə §2a list-indən kənar heç nə vermir; elan etməmək isə həmin list-dən heç nəyi geri almır. Elan olunmuş adı runtime grant-a çevirən gate P1-də yoxdur və fully trusted in-process kod üçün belə gate onsuz da containment sərhədi sayılmır (`ADR-0059`).
- Real access məhdudiyyəti iki mənbədən gəlir: §2a-dakı qapalı injectable set (nə görünür) və capability-nin öz use-time enforcement-i (görünəni necə istifadə etmək olar) — `ADR-0004` policy pattern-i, `ADR-0048` operation authorization, `ADR-0053` licensing runtime mode və per-plugin scoping.

### 2a. P1 qapalı host-injectable capability set

Plugin service scope plugin-private registration-ları öz descriptor-larından həll edir; həll oluna bilməyən type üçün host-a yalnız bu qapalı list üzrə müraciət edir. List hər activated plugin üçün eynidir və manifest declaration-ından asılı deyil:

| Plugin-visible type | Scoping | Owning ADR |
|---|---|---|
| `IEventBus` | Per-plugin subscription lease: `Subscribe(...)` nəticələri lease-də izlənir, plugin cleanup-da dispose olunur, closed lease-ə edilən subscribe no-op qaytarır | `ADR-0010` |
| `IPluginStorage` | Per-plugin storage factory; factory host tərəfindən verilmirsə type ümumiyyətlə resolvable deyil | `ADR-0030` |
| `ICapabilityLicensing` | Per-plugin non-disclosing runtime mode surface; factory verilmirsə resolvable deyil | `ADR-0053` |
| `IPluginWorkSessionService` | Per-plugin ownership wrapper: başqa owner-ın `ownerId`/`sessionId`-si ilə edilən `Open`/`UpdateProgress`/`RecordResult`/`RecordError`/`Close` çağırışı `Forbidden` qaytarır | `ADR-0050`, `FR-015` |
| `IBackgroundOperationReporter` | Host-wide shared reporter instance (per-plugin deyil); snapshot `ownerKind`/`ownerId` ilə attribution daşıyır | `ADR-0015` |
| `IBackgroundOperationCancellation` | Host-wide shared host-lifetime token; plugin öz cancellation source-unu host boundary-sinə keçirə bilmir. Bu coarse host-lifetime siqnalıdır və per-operation cancellation-ın əvəzi deyil: lifecycle hook, command dispatch və page lifecycle çağırışları host-dan ayrıca per-call `CancellationToken` alır və bounded timeout altındadır (`ADR-0059`, `ADR-0008` § 2). Plugin per-call token-i observe etməlidir; onu ignore etmək timeout defect-i verir | `ADR-0015`, `ADR-0059` |
| `ICapabilityPolicy<OperationPermissionCapability>` | Host-wide; host explicit policy inject etməzsə default **deny-all**-dır (fail-closed) | `ADR-0004`, `ADR-0048` |

Bu list-ə type əlavə etmək plugin-facing contract dəyişikliyidir: həmin capability-nin öz ADR qərarı və bu ADR-də amendment tələb edir. Host implementation project-lərinin type-ları (validator, trust provider, persistence, shell view model və s.) bu list-də olmadığı üçün plugin tərəfindən resolve edilə bilmir; bu davranış source-inspection yolu ilə deyil, real plugin module registration üzərində behavior test-i ilə sübut olunur (`NFR-027`, `ADR-0061`).

### 3. Non-disclosing principle

- Capability surface plugin-ə yalnız minimum zəruri məlumatı qaytarır.
- Licensing capability bu qaydanın canonical nümunəsidir: plugin yalnız `ADR-0053` runtime mode surface-i görür; raw license artifact, expiry, grace, customer/device/package binding detail-i görmür.
- Eyni prinsip storage, settings və digər host-managed capability-lərə də tətbiq olunur: backend implementation detail plugin-facing surface-ə çıxmır.

### 4. Scope və enforcement ayrılığı

- Capability `scope` manifest üzərindən elan olunur və yalnız host-defined closed vocabulary qəbul edir (`shell`, `plugin-page`, `background-task`).
- Capability Boundary nəyin görünəcəyini müəyyən edir.
- Policy / authorization enforcement isə görünən capability-nin necə istifadə olunacağını məhdudlaşdırır (`ADR-0011`, `ADR-0048`). Bu enforcement məhsul qaydası sərhədidir, hostile-code sandbox deyil (`ADR-0059`).
- Bu iki mexanizm ayrı öhdəlikdir və biri digərinin əvəzi deyil.

### 5. Phase 1 minimum capability nəticəsi

Phase 1-də canonical minimum davranış budur:

- plugin storage access `IPluginStorage` contract-ı ilə gedir;
- licensing decision non-disclosing runtime mode surface-i ilə gedir (`ADR-0053`);
- work session açmaq/bağlamaq `IPluginWorkSessionService` ilə gedir və session id capability sayılmır: owner mismatch `Forbidden` qaytarır (`ADR-0050`, `FR-015`);
- long-running iş host-owned background operation contract-ı (`IBackgroundOperationReporter`) və host-lifetime cancellation token-i (`IBackgroundOperationCancellation`) ilə gedir; plugin ayrı progress/telemetry channel yaratmır (`ADR-0015`). Lifecycle hook, command execution və page lifecycle çağırışları bounded host timeout altında olduğu üçün ağır iş bu hook-ların içində saxlanılmır — əks halda nəticə timeout defect-idir (`ADR-0059`, `ADR-0008` § 3, § 4);
- host boundary-dən keçən hər plugin çağırışı (module lifecycle, action/command dispatch, page lifecycle) async, cancellable və bounded timeout-ludur; instance başına bir aktiv əməliyyat olur və host fail/timeout/cancel halında deterministik cleanup edir (`ADR-0059`);
- operation authorization `ICapabilityPolicy<OperationPermissionCapability>` ilə evaluate olunur və host policy inject etməzsə default deny-all-dır (`ADR-0048`);
- plugin-to-plugin və plugin-to-host əlaqəsi yalnız public contract, event və declared extension point-lərlə gedir;
- plugin page/screen hosting yalnız official `Dobby.Contracts` / `Dobby.SDK`
  page/extension contract-ları ilə gedir; plugin host-un daxili WPF, shell view
  model və ya implementation service-lərinə birbaşa toxunmur;
- yeni capability əlavəsi deliberate contract change sayılır və ayrıca ADR amendment tələb edir.

### 6. Runtime fault isolation

Capability boundary runtime fault-un **cooperative isolation** sərhədidir: host-un
çağırdığı hər plugin boundary-si (lifecycle hook, command dispatch, page lifecycle,
work session, event handler) exception-ı tutur, typed outcome-a çevirir və fault-u həmin
plugin instance/screen/task sərhədində saxlayır. Plugin screen, task və ya lifecycle
hook unhandled fault verərsə:

- host-invoked boundary-dən keçən plugin fault-u host process-i dayandırmır;
- fault həmin plugin instance/screen/task sərhədində isolate edilir və activation
  cycle-ın qlobal fail-fast hadisəsinə çevrilmir (`ADR-0008` § 4);
- user-facing mesaj non-technical qalır;
- raw exception detail yalnız diagnostics-də, lifecycle/security baxımından
  lazımdırsa audit-də görünür (`ADR-0019`);
- digər müstəqil plugin-lərin runtime və shell işləməyə davam edir;
- nəticə safe error mapping-dən sonra da explicit outcome origin saxlayır və yalnız
  plugin defect origin-ləri (timeout, plugin-declared failure, thrown exception,
  contract breach) fault sayılır; `Forbidden`, `InvalidInput`, `LicenseReadOnly` və
  cancellation kimi expected nəticələr fault deyil (`ADR-0059`, `ADR-0008` § 4);
- təkrarlanan defect-lər bounded process-local quarantine-ə aparır: threshold-a çatanda
  plugin-in route, event subscription, service, operation və work session-ları normal
  lifecycle cleanup boundary-si ilə çıxarılır (`ADR-0059`, `ADR-0008` § 4).

Bu isolation cooperative-dir, containment deyil. Plugin in-process və fully trusted
işlədiyi üçün host **hər** fault sinfi üçün process survival zəmanəti verə bilmir:
process-fatal hallar (məsələn `StackOverflowException`, `OutOfMemoryException`, plugin-in
özünün yaratdığı thread-də observe olunmayan exception, `Environment.Exit` və ya native
crash) managed boundary-də tutula bilmir. `ADR-0059` trust posture-una uyğun olaraq bu
ADR belə bir zəmanət iddia etmir; host-un öhdəliyi host-invoked boundary-dən keçən
fault-ları tutmaq, typed nəticəyə çevirmək, isolate etmək və təkrarlanan defect halında
plugin-i quarantine etməkdir. Qalan process-fatal risk trust/signing gate-i (`ADR-0032`,
`ADR-0059`), package review və quarantine ilə idarə olunur — sandbox iddiası ilə deyil.

### 7. Rejected alternatives

- **`IServiceProvider` açıq plugin-ə verilsin**: rədd edildi — plugin host-un bütün DI container-ini görür və istənilən service-ə çata bilər; capability boundary pozulur.
- **Dictionary-based və ya string-only capability API**: rədd edildi — explicit contract surface discoverability və boundary clarity verir.
- **Plugin-ə raw internal service vermək**: rədd edildi — capability boundary-ni mənasız edir.
- **Plugin WPF control-larını host shell-ə birbaşa yerləşdirsin**: rədd edildi —
  plugin host UI implementation detail-inə bağlanır və capability boundary
  zəifləyir. Canonical əvəz declarative host-rendered plugin UI contract-ıdır
  (`ADR-0059`): plugin WPF tipləri və delegate-lər qaytarmır.

- **Manifest capability declaration-ı runtime grant kimi oxunsun**: rədd edildi —
  `capabilities[].name` açıq identifier-dir və host-defined qapalı vocabulary
  deyil, ona görə declaration-dan runtime grant-a deterministic mapping yoxdur.
  Onu grant kimi göstərmək manifest-i faktiki olaraq enforce etmədiyi sərhədin
  sənədi kimi təqdim edərdi. Declaration review/transparency artefaktı olaraq
  qalır; access §2a qapalı set-i və use-time policy ilə məhdudlaşır.

## Consequences

- Plugin host internal service-lərini host service resolution yolu ilə ala bilmir; capability access həmişə host-controlled boundary-dən keçir. Bu resolution-surface qərarıdır, in-process hostile-code containment iddiası deyil (`ADR-0059`).
- Licensing capability non-disclosing olduğu üçün plugin yalnız runtime mode alır; plugin license detail UX-i yaza bilmir.
- Plugin UI Main Content Area-da host tərəfindən təqdim olunan contract-larla
  göstərilir; WPF/internal shell tipləri plugin-facing contract-a çevrilmir.
- Runtime fault isolation plugin failure-ını host process failure-ına çevirmir.
- Yeni capability əlavəsi public contract dəyişikliyi sayılır və ayrıca review/ADR tələb edir.
- §2a list-i qapalı olduğu üçün plugin-facing surface audit-i type list müqayisəsinə çevrilir; admin verification output-undaki declared capability-lər isə review siqnalıdır, grant deyil.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — plugin-level licensing non-disclosing prinsipi
- `docs/02_ARCHITECTURE_MAP.md` — Row 41 `Plugin Capability Boundary`, Engineering Conventions `Plugin Trust Enforcement` və `Capability Boundary`, Cədvəl 3 `Plugin Capability Boundary` risk sətri
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — Contracts surface stability
- `docs/adr/ADR-0004-capability-enforcement-model.md` — use-time policy enforcement pattern-i
- `docs/adr/ADR-0010-plugin-communication-policy.md` — icazəli kanallar, `IEventBus` shape
- `docs/adr/ADR-0011-plugin-trust-and-policy-enforcement-principle.md` — trust və policy enforcement prinsipi
- `docs/adr/ADR-0015-background-operation-reporting.md` — `IBackgroundOperationReporter` / `IBackgroundOperationCancellation`
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md` — `IPluginStorage` capability
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — `capabilities` schema
- `docs/adr/ADR-0048-operation-based-authorization.md` — `ICapabilityPolicy<OperationPermissionCapability>`
- `docs/adr/ADR-0050-in-memory-work-sessions.md` — `IPluginWorkSessionService` və owner binding
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — licensing runtime mode surface
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — current trust posture and declarative host-rendered plugin UI contract (amends this ADR's security-boundary and hostile-code containment claims; this ADR remains `Accepted` and authoritative for its untouched sections)
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-026`, `FR-015`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-027` (behavior-first proof qaydası)
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-015`, `AC-032`
- `docs/specs/plugins-manager.md`

## Revision History

- **2026-08-27:** §2-dəki "plugin elan etmədiyi capability-yə birbaşa çatmır" cümləsi
  silindi və declaration ilə access ayrıldı. Həmin cümlə shipped davranışı yanlış
  təsvir edirdi: plugin service scope host service-lərini type üzrə həll edir və
  manifest `capabilities[]` array-inə heç vaxt baxmır, `capabilities[].name` isə açıq
  identifier-dir (`#/$defs/identifier`), yəni ondan grant-a mapping mövcud deyil. Nəticədə
  ADR oxucuya faktiki olmayan bir gate-i enforcement kimi göstərirdi — declared
  capability-lərə əsaslanan review/threat-model mülahizəsi yanlış təhlükəsizlik
  fərziyyəsi üzərində qurula bilərdi (plugin trust misrepresentation). Yeni mətn üç şeyi
  explicit edir: declaration signature-bound review artefaktıdır (canonical package
  fingerprint-ə daxildir və admin verification output-unda görünür), access §2a-dakı
  qapalı injectable set ilə məhdudlaşır, use-time məhdudiyyət isə policy/scope
  enforcement-indən gəlir. §2a Phase 1-in real injectable set-ini (7 type, scoping və
  owning ADR ilə) canonical list kimi bağlayır — əvvəl bu list heç bir sənəddə yox idi,
  ona görə §5-in "yeni capability əlavəsi ayrıca ADR amendment tələb edir" qaydası
  yoxlanıla bilən deyildi; §5 həmin qayda üzrə work session, background operation
  reporting/cancellation və operation authorization ilə tamamlandı. Qərar dəyişmir:
  bu ADR-in security-boundary iddiaları onsuz da `ADR-0059` ilə amend olunub və
  list-dəki hər capability öz ADR-ində qərarlaşdırılıb, ona görə successor ADR
  yaradılmır — bu factual boundary reconciliation-dır. Qapalı set üçün behavior
  evidence-i `tests/Dobby.UnitTests/PluginFramework/Activation/PluginActivationCoreTests.cs`
  altında əlavə edildi (`NFR-027`: source scan və ya type list oxumaq sübut sayılmır).
- **2026-08-31:** §§ 2, 2a, 5, 6, Consequences və cross-reference sətri `ADR-0059`
  trust posture-u və async/cancellable lifecycle contract-ı ilə uzlaşdırıldı.
  (a) § 2-dəki "plugin host-un dəstəkləmədiyi type-a çatmır" mütləq ifadəsi resolution
  surface-inə scope olundu: qapalı §2a set-i DI/resolution qərarıdır, in-process fully
  trusted kod üçün reflection-level reachability qadağası deyil və bu ADR onu containment
  kimi iddia etmir; eyni düzəliş Consequences-dəki paralel cümləyə tətbiq edildi. Səbəb:
  mütləq oxunuş bu sətri security guarantee kimi göstərirdi və `ADR-0059`-un "heç bir qat
  hostile-code containment iddia etmir" posture-u ilə ziddiyyət yaradırdı.
  (b) §2a `IBackgroundOperationCancellation` sətri tamamlandı: host-lifetime token coarse
  siqnaldır və per-operation cancellation-ın əvəzi deyil; lifecycle/command/page çağırışları
  host-dan ayrıca per-call `CancellationToken` alır (`ADR-0059`). Əvvəlki sətri oxuyan
  plugin author yalnız host-lifetime token-in mövcud olduğunu düşünüb per-call token-i
  ignore edə və timeout defect-i yarada bilərdi.
  (c) § 5-ə host boundary-nin async/cancellable/bounded-timeout, per-instance bir aktiv
  əməliyyat və deterministik cleanup qaydası əlavə edildi; long-running iş bullet-i
  hook-ların timeout altında olduğunu explicit yazır.
  (d) § 6 "hard stop sərhədi" iddiası **cooperative isolation** modeli ilə əvəz olundu.
  Əvvəlki mətn şərtsiz "host process çökməməlidir" öhdəliyi verirdi; in-process fully
  trusted plugin üçün bu yerinə yetirilə bilməyən zəmanətdir (`StackOverflowException`,
  `OutOfMemoryException`, plugin thread-ində observe olunmayan exception, `Environment.Exit`,
  native crash managed boundary-də tutulmur). Yeni mətn host öhdəliyini host-invoked
  boundary-dən keçən fault-lara scope edir, outcome origin ayrımını və bounded
  process-local quarantine-i (`ADR-0008` § 4) daxil edir və qalan riskin trust/signing,
  review və quarantine ilə idarə olunduğunu yazır.
  (e) `ADR-0059` cross-reference sətrindəki "supersedes this ADR's security-boundary
  claims" ifadəsi amend vocabulary-sinə keçirildi — `**Status:**` sətri artıq amendment
  deyir, cross-reference isə hələ supersession deyirdi (iki authority bir-birini təkzib
  edirdi; README yalnız `**Supersedes:**` və `**Amends:**` relation-larını tanıyır).
