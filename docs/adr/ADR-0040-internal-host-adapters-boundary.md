# ADR-0040: Internal Host Adapters Boundary (Dobby.Infrastructure)

**Status:** Accepted (ADR-0039 5-hash composite recovery references are historical — recovery now follows ADR-0058 device recovery key model; the Dobby.Infrastructure adapter boundary itself is unchanged)
**Date:** 2026-04-24

## Context

Audit `H-04` tapıntısı göstərdi ki, `ADR-0039 Owner Password Recovery` accepted qərarı `Dobby.Infrastructure` namespace/project-inə istinad edir (ADR-0039 § 132, § 161-162, § 200-202):

- `Dobby.Infrastructure` Win32 API inteqrasiyasını (Registry, WMI, NetworkInterface) hardware identity query səthi kimi açır
- Consumed recovery nonce persistence `Dobby.Infrastructure` (ADR-0030 boundary ilə) altında saxlanır
- `Dobby.Infrastructure` `Dobby.Security`-dən çağrılır

Amma `02_ARCHITECTURE_MAP.md` Cədvəl 1-də (rows 1-85) və Cədvəl 2 dependency graph-ında belə project mövcud deyildi. Bu, accepted ADR ilə architecture map arasında ownership gap yaradırdı.

`MF-06` tapıntısı ilə bağlı olaraq, storage ownership per-project dağılımı (plugin storage, settings, licensing cache, audit storage) da sənədləşməmişdi; bu boundary-nin Phase 1-də formalizasiyası lazım idi. Sonrakı Owner qərarı local DB/persistence fayllarının data-at-rest qorunmasını, yalnız secret field-lərin əlavə qorunmasını və DB açarının təkcə `deviceId`-dən törəməməsini də tələb edir.

Bu ADR `Dobby.Infrastructure` project-ini və internal host adapter layer-ini rəsmiləşdirir. Architecture Map Cədvəl 1 Row 87-89 və Cədvəl 2 Dobby.Infrastructure dependency entry-si bu ADR-in icra addımlarıdır.

## Decision

Phase 1 internal host adapters boundary modeli:

### 1. `Dobby.Infrastructure` project

- `Dobby.Infrastructure` internal-only host adapter layer-idir (`plugin visibility: no`).
- Plugin heç bir halda `Dobby.Infrastructure` assembly-sinə birbaşa reference saxlamır və manifest-də onu mention edə bilmir.
- Plugin visibility enforcement `Engineering Conventions / Qızıl Asılılıq Sərhədi` və `Plugin-facing Surface Qaydası` ilə qorunur.

### 2. Responsibility-lər

`Dobby.Infrastructure` aşağıdakı responsibility-ləri saxlayır:

- **Hardware Identity query** (Row 88): `Dobby.Infrastructure / Hardware Identity` namespace-i Registry, WMI və NetworkInterface üzərindən device fingerprint hash-lərini hesablayır; `ADR-0039` 5-hash composite recovery flow-unun internal surface-idir.
- **Host Persistence / Consumed Nonces** (Row 89): `ADR-0030` host persistence boundary-si daxilində consumed recovery nonce append-only store-u saxlanır. Bu SQLite-based store-dur və yalnız host internal code-dan accessible-dir.
- **Local persistence protection**: host və plugin local persistence backing file-larının data-at-rest qorunması üçün storage protection adapter-lərini saxlayır. Bu adapter-lər şifrələmə/protection açarını istifadəçidən əl ilə istəmir və təkcə `deviceId`-dən törətmir; Dobby-created local secret-lər və OS/device protected mechanism-lərindən istifadə edir.
- **Secret field protection**: password, token, key, remember-me secret, recovery secret və session material kimi həssas sahələr üçün DB qorumasından əlavə protected-storage/field-level wrapper-ləri təqdim edir.
- **Win32 API wrapper-ləri**: platform-specific API-ların encapsulated adapter-ləri (Registry read, WMI query, NetworkInterface enumeration) Windows-only adapter slice-da saxlanılır. `Dobby.Security` və `Dobby.Audit` platform-neutral contract/abstraction surface görür; concrete Windows adapter-lər `Dobby.Host` composition root tərəfindən DI-da wire edilir.

### 3. Dependency direction

- `Dobby.Infrastructure` → `Dobby.Core`, `Dobby.Contracts` (minimal reference; primitive-lər və result types).
- `Dobby.Infrastructure`-a direct reference yalnız `docs/02_ARCHITECTURE_MAP.md`
  dependency table-da explicit görünən edge-lər üçün açıqdır. Cari P1 required
  direct edge-lər `Dobby.Host`, `Dobby.Security` və `Dobby.Audit` üçündür; başqa
  owner project direct primitive access tələb edərsə həmin edge architecture
  map-də ayrıca göstərilməlidir.
- Plugin, `Dobby.Contracts`, `Dobby.SDK` birbaşa və ya transitive `Dobby.Infrastructure`-a bağlanmır.

Composition root qaydası: owner project-lər (`Dobby.Configuration`,
`Dobby.Licensing`, `Dobby.PluginFramework`, `Dobby.Audit`, `Dobby.Security`) öz
host-internal behavior, use-case və contract adlarını saxlayır.
`Dobby.Infrastructure` həmin owner contract-larının ümumi implementation evidən
daha dar rola malikdir: Win32, SQLite/backing-store, DPAPI, device və
protected-storage primitive adapter-lərini verir. `Dobby.Host` composition root
bu primitive-ləri DI-da wire edir. Bu wiring dependency direction-u dəyişmir və
plugin-visible surface yaratmır.

### 3.1. HD-008 / DM-095: Infrastructure abstraction and adapter ownership

Dobby infrastructure-related contracts, implementation behavior and wiring üçün
per-row ownership table istifadə edir.

| Area | Contract / use-case location | Implementation location | DI wiring location | Architecture-map dependency rule |
|---|---|---|---|---|
| Plugin-visible page contracts (`IPluginPage`, `IPluginPageModel`, descriptors) | `Dobby.Contracts` | Host rendering in `Dobby.Shell` / `Dobby.UI`; plugin implementation only through approved contracts | `Dobby.Host` | Plugins/SDK must not reference `Dobby.Infrastructure` or WPF host implementation types. |
| Plugin storage (`SharedDomainStorage`, `UserScopedStorage`) | Plugin-visible contract in `Dobby.Contracts`; host plugin storage use case in `Dobby.PluginFramework` | `Dobby.PluginFramework` orchestration with `Dobby.Infrastructure / Host Persistence` backing primitive | `Dobby.Host` | Any direct `Dobby.PluginFramework -> Dobby.Infrastructure` edge must be shown in the architecture map; otherwise Host wiring keeps the edge out. |
| Audit append-only store | `Dobby.Audit` | `Dobby.Audit` audit store behavior using `Dobby.Infrastructure / Host Persistence` primitive where needed | `Dobby.Host` | `Dobby.Audit -> Dobby.Infrastructure` is allowed only because the architecture map shows it. |
| License cache | `Dobby.Licensing` | `Dobby.Licensing` cache behavior using a Host-wired persistence primitive where needed | `Dobby.Host` | Add `Dobby.Licensing -> Dobby.Infrastructure` only if implementation needs direct primitive access and the architecture map is amended. |
| User/app/plugin settings | `Dobby.Configuration` | `Dobby.Configuration` settings behavior using a Host-wired persistence primitive where needed | `Dobby.Host` | Add `Dobby.Configuration -> Dobby.Infrastructure` only if implementation needs direct primitive access and the architecture map is amended. |
| Recovery nonce store | `Dobby.Security` | `Dobby.Security` recovery nonce behavior using `Dobby.Infrastructure / Host Persistence` primitive where needed | `Dobby.Host` | `Dobby.Security -> Dobby.Infrastructure` is allowed only because the architecture map shows it. |
| Device fingerprint / hardware identity | `Dobby.Security` | `Dobby.Security` device identity behavior using `Dobby.Infrastructure` Registry/WMI/NetworkInterface primitives | `Dobby.Host` | `Dobby.Security -> Dobby.Infrastructure` must remain explicit in the architecture map. |
| DPAPI/session material and protected secret wrappers | `Dobby.Security` | `Dobby.Security` protection behavior using `Dobby.Infrastructure` DPAPI/protected-storage primitives | `Dobby.Host` | `Dobby.Security -> Dobby.Infrastructure` must remain explicit in the architecture map. |

Variant 1, where `Dobby.Infrastructure` implements owner-project contracts by
referencing those owner projects, is rejected as the default because it reverses
the current dependency map for most rows. The accepted model keeps plugin-visible
abstractions in `Dobby.Contracts`, keeps host-internal behavior with owner
projects, and keeps `Dobby.Infrastructure` as the primitive/backing adapter
layer.

Exact host-internal provider-neutral abstraction names, namespaces and method
surfaces for settings storage, license cache, audit append-only writing, recovery
nonce storage, protected-storage wrappers and plugin-storage backing primitives
are blocked pending a human/architecture-approved table. Docs-fix or skeleton
agents must not invent provider-neutral repository/session factory names,
method signatures, `Result<T>` behavior or error-code surfaces from the prose
ownership map alone. Until that table exists, initial skeleton work must stop
before creating storage-facing host abstractions beyond already-approved
plugin-visible contracts from ADR-0003.

### 4. Plugin-visible storage surface dəyişməz qalır

- Plugin storage access həmişə `IPluginStorage` contract-ından keçir (`ADR-0030`).
- `Dobby.Infrastructure / Host Persistence` namespace-i `IPluginStorage` implementation-ının internal backing store-unu saxlaya bilər, amma plugin bu backing store-u birbaşa görmür.
- Infrastructure layer-də SQLite + Dapper istifadə olunur (`03_TECH_STACK_AND_VERSIONS.md` `## 2. DI, Hosting & Infrastructure`, Storage row); plugin boundary yalnız contract-ı görür, backend texnologiyasını yox.

### 5. Storage ownership xəritəsi (MF-06)

Aşağıdakı xəritə Phase 1-də kim hansı storage area-sını sahib saxladığını göstərir:

| Storage area | Behavior / contract owner | Backing primitive | Dependency / wiring rule | Plugin-visible? |
|---|---|---|---|---|
| Plugin shared domain storage (`SharedDomainStorage`) | Plugin-visible contract in `Dobby.Contracts`; host storage use case in `Dobby.PluginFramework` | `Dobby.Infrastructure / Host Persistence` backing primitive | `Dobby.Host` wires primitive unless an explicit architecture-map edge is added | Yalnız storage contract |
| Plugin user-scoped storage (`UserScopedStorage`) | Plugin-visible contract in `Dobby.Contracts`; host storage use case in `Dobby.PluginFramework` | `Dobby.Infrastructure / Host Persistence` backing primitive | `Dobby.Host` wires primitive unless an explicit architecture-map edge is added | Yalnız storage contract |
| User/app settings | `Dobby.Configuration / User / Plugin Settings Management` behavior | `Dobby.Infrastructure / Host Persistence` primitive where needed | `Dobby.Host` wires by default; direct edge requires architecture-map amendment | Yox |
| Licensing cache | `Dobby.Licensing / License / Entitlement` behavior | `Dobby.Infrastructure / Host Persistence` primitive where needed | `Dobby.Host` wires by default; direct edge requires architecture-map amendment | Yalnız `ICapabilityLicensing.GetCurrentMode()` üzərindən `PluginRuntimeMode`; raw license detail görünmür |
| Audit log | `Dobby.Audit / Audit Trail` behavior | `Dobby.Infrastructure / Host Persistence` append-only primitive where needed | `Dobby.Audit -> Dobby.Infrastructure` edge is explicit in architecture map | Yox |
| Consumed recovery nonces | `Dobby.Security` recovery behavior | `Dobby.Infrastructure / Host Persistence` primitive where needed | `Dobby.Security -> Dobby.Infrastructure` edge is explicit in architecture map | Yox |
| DPAPI session material and protected secret wrappers | `Dobby.Security / Authentication / Session` and secret protection behavior | `Dobby.Infrastructure` DPAPI/protected-storage primitive where needed | `Dobby.Security -> Dobby.Infrastructure` edge is explicit in architecture map | Yox |

Local persistence backing store-ları data-at-rest qorunur. Business/domain data
field-level şifrələnmir; secrets yuxarıdakı protected wrapper-lərdən keçir.
`deviceId` support/identity metadata-dır, encryption key mənbəyi deyil.

### 6. Platform-specific assumption

- `Dobby.Infrastructure` canonical TFM split-i `docs/03_TECH_STACK_AND_VERSIONS.md` Target Framework Split section-ına tabedir: default surface `net10.0` qalır, Windows-only adapter slice isə `net10.0-windows` target-ləyə bilər.
- `Dobby.Security` və `Dobby.Audit` `net10.0` qalır və yalnız platform-neutral abstraction/contract surface-ə reference saxlayır; Registry, WMI, NetworkInterface və DPAPI concrete adapter-lərinə compile-time bağlı olmur.
- Windows-only adapter slice eyni `Dobby.Infrastructure` project-inin conditional/multi-target implementation hissəsi və ya ayrıca sənədləşdirilən adapter slice ola bilər, amma Phase 1 required project list-inə ayrıca yeni public project adı əlavə edilmir.
- Registry, WMI və Win32 API Windows-da mövcuddur; cross-platform runtime support Phase 1 scope-una daxil deyil. Bu split cross-platform product promise deyil, build/TFM leakage-ni önləyən adapter boundary-dir.

### 7. Rejected alternatives

- **Infrastructure = `Dobby.Security` daxilində namespace**: rədd edildi — Win32 adapter-lər Security responsibility deyil; testability və cross-project reuse üçün ayrıca project daha sadədir.
- **Plugin-accessible Win32 surface**: rədd edildi — plugin boundary-də platform-specific API qadağandır; plugin capability boundary (`ADR-0012`) bunu enforce edir.
- **Ayrıca `Dobby.Storage` project açılması**: rədd edildi — `ADR-0030` specifically `Dobby.Storage` project-ini açmayacağını qeyd edir; storage ownership `Dobby.Infrastructure`-da sub-namespace kimi saxlanır.

## Consequences

- `ADR-0039` ownership gap bağlanır; recovery flow-da adı çəkilən `Dobby.Infrastructure` artıq architecture map-də formal project-dir.
- `MF-06` storage ownership gap bağlanır; hər data sahibi storage backing-in where/how bağlanması aydındır.
- Plugin boundary dəyişməz qalır; `IPluginStorage` contract-ı və capability-based surface plugin-lər üçün yeganə access path-dir.
- DB/data protection adapter ownership `Dobby.Infrastructure` daxilində qalır; plugin bu qorumanın key materialını, backing file path-ini və provider detalını görmür.
- `Dobby.Infrastructure` Windows-only adapter implementation-ları daşıya bilər, amma `Dobby.Security` və `Dobby.Audit` üçün görünən surface platform-neutral qalmalıdır; host cross-platform runtime support Phase 2+ roadmap-ə aiddir.
- Cədvəl 2 dependency graph-ında `Dobby.Host`, `Dobby.Security`, `Dobby.Audit` → `Dobby.Infrastructure` kənarları platform-neutral surface üçün saxlanılır; Windows-only concrete adapter-lər `Dobby.Host` composition root-da wire edilir.

## Resolved Decisions

| ID | Resolution |
|---|---|
| `HD-008` | RESOLVED_BY_HUMAN_DECISION (`DM-095`): infrastructure-related contract, implementation and DI wiring ownership uses the per-row table in this ADR; `Dobby.Infrastructure` provides primitive/backing adapters, not general owner-project contract implementations. |

## Reopen Conditions

These resolved decisions must not be reopened for preference-only reasons.

| Decision | Reopen condition |
|---|---|
| `DM-095` / `HD-008` | Architecture tests show circular references, plugin-visible implementation leakage, or an owner project cannot implement its documented behavior without an undocumented reference. |

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 87 `Dobby.Infrastructure`, Row 88 `Hardware Identity`, Row 89 `Host Persistence / Consumed Nonces`, Cədvəl 3 `Dobby.Infrastructure Boundary` risk sətri
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md` — host persistence internal saxlanması qaydası
- `docs/adr/ADR-0039-owner-password-recovery.md` — `Dobby.Infrastructure` hardware identity + consumed nonce usage (superseded by `ADR-0058`; nonce/replay state qalır, fuzzy hardware match yoxdur)
- `docs/adr/ADR-0019-audit-vs-diagnostics-boundary.md` — audit storage boundary
- `docs/adr/ADR-0038-local-first-authentication.md` — DPAPI session material storage
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-007`
- `docs/specs/storage-and-data-protection.md` — DB/data protection və key management spec

## Branch Shared Provider Amendment

`Dobby.Infrastructure` may later host provider-specific adapters for Branch
Shared PostgreSQL, but the current Mərhələ 1 pass is documentation-only.
Provider-neutral repository/session factory shape may be planned later and is
not authorized by this amendment without a human/architecture-approved table;
Npgsql runtime adapter and live PostgreSQL tests are not authorized by this
amendment.

SQLite and PostgreSQL provider behavior must remain semantic, not string/path
driven. Shared-folder/network-drive SQLite is not a Branch Shared substitute.
