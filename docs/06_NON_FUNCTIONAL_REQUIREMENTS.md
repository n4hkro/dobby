# Dobby Non-Functional Requirements

**Tarix:** 2026-04-24
**Status:** Təsdiqlənib
**Scope:** Keyfiyyət atributları, runtime sərhədləri və release-level qeyri-funksional tələblər

Bu sənəd Phase 1 üçün keyfiyyət atributlarını, startup/reliability/security sərhədlərini və ölçülə bilən release qaydalarını toplayır. Funksional davranışlar [docs/05_FUNCTIONAL_REQUIREMENTS.md](./05_FUNCTIONAL_REQUIREMENTS.md), acceptance yoxlamaları isə [docs/07_ACCEPTANCE_CRITERIA.md](./07_ACCEPTANCE_CRITERIA.md) ilə birlikdə işləyir.

## Requirements

| ID | Requirement | Phase | Source |
|---|---|---|---|
| NFR-001 | Core runtime offline-first işləyir; internet core login, shell, local auth, plugin install verification və lokal runtime üçün məcburi deyil. | P1-core | `01_PRODUCT_SCOPE.md`, `ADR-0038` |
| NFR-002 | Login-first startup policy qorunur: login görünənə qədər yalnız minimal host/auth məlumatları oxunur; plugin runtime/activation, full plugin scan, online check, update check, import/export, heavy migration və license-heavy verification icra olunmur. Login UI görünəndən sonra pre-auth dövrdə yalnız non-blocking cached plugin metadata oxuna bilər; assembly load və plugin kodu login success-dən əvvəl başlamır. | P1-core | `01_PRODUCT_SCOPE.md`, `ADR-0041`, `ADR-0008` |
| NFR-003 | Pre-login və post-login startup mərhələləri instrument olunur; minimum release ölçmələri cold start, warm start və `login submit -> shell first paint` keçidlərini ayrıca qeyd edir. | P1-core | `ADR-0041`, `02_ARCHITECTURE_MAP.md` Row 66 |
| NFR-004 | Signed plugin trust modeli məcburidir: unsigned, tampered və ya untrusted plugin-lər install və activation sərhədindən keçmir. | P1-core | `ADR-0032`, `ADR-0033` |
| NFR-005 | Plugin-level licensing non-disclosing host boundary ilə enforce olunur; plugin licensing-dən opt-out edə bilmir, raw license artifact, expiry, grace, revision və binding detail görmür, yalnız runtime mode surface-i görür. | P1-core | `01_PRODUCT_SCOPE.md`, `ADR-0051`, `ADR-0053` |
| NFR-006 | Result-vs-Exception policy qorunur: expected failure `Result<T>` ilə, programmer/invariant violation exception ilə, plugin boundary raw exception sızdırmır, fatal/corrupted-state exception-lar tutulmur. | P1-core | `04_RESULT_VS_EXCEPTION_POLICY.md` |
| NFR-007 | Local data protection mövcuddur: host və plugin local persistence faylları data-at-rest qorunur; normal biznes məlumatları field-level şifrələnmir, yalnız password, token, key, remember-me secret, recovery secret və session material kimi secrets əlavə field-level/protected-storage qoruması alır. DB qoruma açarı istifadəçidən əl ilə istənilmir, təkcə `deviceId`-dən törədilmir və Dobby-nin yaratdığı lokal secret-lər + cihazın qorunan identity/protection mexanizmləri ilə avtomatik idarə olunur. Plugin host storage detalını görmür və yalnız rəsmi storage contract/capability-lərdən istifadə edir. | P1-core | `03_TECH_STACK_AND_VERSIONS.md`, `ADR-0030`, `ADR-0038`, `ADR-0040`, `ADR-0054`, `ADR-0055` |
| NFR-008 | Plugin isolation qorunur: plugin runtime yalnız authenticated session-dan sonra başlayır, plugin yalnız `Dobby.Contracts` və `Dobby.SDK` public surface-lərini görür, host implementation project-lərinə birbaşa bağlanmır və plugin UI yalnız official page/extension contract vasitəsilə Main Content Area-da host olunur. Runtime fault plugin boundary-də isolate edilməli, host process və digər plugin-lər çökməməlidir. | P1-core | `ADR-0001`, `ADR-0007`, `ADR-0008`, `ADR-0012`, `02_ARCHITECTURE_MAP.md` conventions |
| NFR-009 | Compatibility və dependency resolution deterministikdir: incompatible plugin activation-a buraxılmır, missing/circular dependency halları typed səbəblə skipped olur, activation/runtime fault halları per-plugin isolate edilir və digər müstəqil plugin-lərin lifecycle-ını dayandırmır. | P1-core | `ADR-0009`, `ADR-0037`, `ADR-0008` |
| NFR-010 | Localization readiness və theme foundation saxlanır: string-lər mərkəzləşdirilmiş resource idarəçiliyi ilə verilir, P1 `az-AZ` default culture-dır, light/dark theme design token-lar üzərindən idarə olunur və minimum theme/shared UI primitives ayrı-ayrı bölmələrdə təkrarlanmır. | P1-core | `01_PRODUCT_SCOPE.md`, `02_ARCHITECTURE_MAP.md` Row 49/72/73 |
| NFR-011 | Auditability və diagnostics ayrıdır: audit compliance yönümlü append-only iz saxlayır, technical logging isə ayrıca diagnostics qatında qalır. | P1-core | `ADR-0019`, `02_ARCHITECTURE_MAP.md` Row 66/70 |
| NFR-012 | Reusable UI infrastructure, richer dialogs və extended process reporting release blocker deyil; lakin in-memory work session-lar üçün minimum process-lifetime progress/restore reporting P1-core-dur. | P1-if-time | `02_ARCHITECTURE_MAP.md` Row 13/14/63/73-77, `ADR-0050` |
| NFR-013 | Localization resources namespace/culture convention-a tabedir; plugin resource-ları plugin package/assembly sərhədində qalır və host declared provider/hook vasitəsilə oxuyur. Development-də missing key görünən olmalı, production-da fallback culture və diagnostics log/event (`localization.missing-key`) istifadə olunmalıdır. Missing key audit-ə yazılmır; runtime language switching P2-yə, full multi-language UI və RTL polish isə P3-ə saxlanılır. | P1-core | `ADR-0043`, `ADR-0044`, `ADR-0019` |
| NFR-014 | `.dobbyplugin` və `.dobbylicense` artifact boundary customer data, device fingerprint, vendor private key, host internal configuration, embedded license payload və host persistence migration script leakage-in qarşısını almalıdır. Package fingerprint canonical signed content əsasında hesablanmalıdır; verification unsigned extra file, missing file, hash mismatch, duplicate entry, forbidden path, symlink və raw container fingerprint dependency hallarında reject edir. | P1-core | `ADR-0045`, `ADR-0030`, `ADR-0032` |
| NFR-015 | License binding mismatch non-disclosing deny və audit/diagnostics nəticəsi yaradır; plugin və normal user-facing surface mismatch-in customer/deployment/device/package detalını görmür. License import mövcud host-level customer/deployment context-i ilə yoxlanır və ilk import zamanı yeni context-i opportunistic yaratmır. | P1-core | `ADR-0051`, `ADR-0052`, `ADR-0019` |
| NFR-016 | Read-only expired mode UI-only deyil; host storage write, command pipeline, background task, plugin/domain import/export/report və print target boundary tərəfindən fail-closed enforce olunur. `.dobbylicense` import recovery exception kimi host-level licensing flow-da permission yoxlaması ilə mümkün qalır. | P1-core | `ADR-0051`, `ADR-0053`, `02_ARCHITECTURE_MAP.md` Row 64 |
| NFR-017 | Authorization modeli grant-only/on-off işləyir: explicit deny permission record-u yoxdur, grant yoxdursa əməliyyat deny edilir. Permission check-ləri registry-produced `<targetId>.<verb>` operation key-ləri və canonical verb siyahısı ilə aparılır; unknown target/verb fail-closed deny olur və audit warning yaradır. Authorization deny qərarları audit trail-ə yazılır; plugin və normal user-facing deny reason non-disclosing qalır. | P1-core | `ADR-0048`, `ADR-0004`, `ADR-0019` |
| NFR-018 | Customer/device migration offline file-based və vendor-imzalı olmalıdır; Phase 1-də bu axın mövcud deyil və P2/later-ə saxlanılır. | P2 | `01_PRODUCT_SCOPE.md`, `FR-020` |
| NFR-019 | Shell və built-in ekranlarda P1 accessibility minimumu qorunur: keyboard navigation, görünən focus indicator, kifayət qədər contrast və əsas control-lar üçün role/label mapping. Full WCAG/screen reader polish P3 scope-una saxlanılır, amma P1 shell workflow-ları keyboard-only istifadəyə əsas maneə yaratmamalıdır. | P1-core | `02_ARCHITECTURE_MAP.md`, `docs/specs/shell-ui.md` |
| NFR-020 | Privacy default-local modeldir: Dobby default olaraq vendor-a usage analytics, crash report, telemetry və diagnostics upload etmir. Diagnostics export yalnız explicit user/Owner action ilə local package kimi yaradılır və secrets, raw license detail, session material və credential materialı redaktə etmədən və ya daxil etmədən çıxarılmalıdır. | P1-core | `01_PRODUCT_SCOPE.md`, `ADR-0019`, `docs/specs/audit-and-diagnostics.md` |
| NFR-021 | Login submit-dən sonra UI thread uzun host əməliyyatları (IO, DB, crypto, plugin activation/call, plugin install/license import və icazəli opsional network call) ilə bloklanmır; bu işlər background-da icra olunur və `IProgress<T>` üzərindən progress reporting, `CancellationToken` üzərindən cancellation, typed failure status və host-owned retry/reporting semantics verir. | P1-core | `02_ARCHITECTURE_MAP.md` Row 63, `docs/specs/shell-ui.md` |
| NFR-022 | Host-owned text surface-lər (DB, log fayllar, config, plugin manifest read/write, diagnostics export və host UI text resource faylları) UTF-8 encoding ilə oxunur və yazılır; file/artifact səviyyəsində BOM yazılmır. Encoding heç bir host layer-də system locale-dən asılı olmamalıdır. In-memory string-lər və UI-rendered mətn üçün BOM anlayışı tətbiq olunmur. Plugin-owned export/search/report formatları plugin öhdəliyindədir və bu NFR-in əhatəsindən kənardadır. | P1-core | `ADR-0043`, `ADR-0044`, `ADR-0045`, `docs/specs/audit-and-diagnostics.md` |
| NFR-023 | Azərbaycan diakritikləri (`əƏ`, `ğĞ`, `öÖ`, `üÜ`, `şŞ`, `çÇ`, `ıI`) host-owned user-facing text surface-lərində (input, render, persistence, host search/display) dataloss/encoding pozulması yaratmadan işləməlidir. User-facing host search/display müqayisəsi Turkic locale qaydalarına uyğun aparılır; canonical identifier-lər (plugin ID, package ID, manifest key, permission key, error code və digər stable code) həmişə ordinal/invariant müqayisə ilə işləyir və culture-sensitive davranışa düşmür. Plugin business export/search bu NFR-in əhatəsindən kənardadır. | P1-core | `ADR-0036`, `ADR-0043`, `NFR-022` |
| NFR-024 | App upgrade (manual local replacement və ya MSIX upgrade) host data, audit log, license cache, users/permissions və user preferences-i pozmamalıdır; release-də v(N) → v(N+1) upgrade smoke test ilə yoxlanılır. | P1-core | `ADR-0047`, `03_TECH_STACK_AND_VERSIONS.md` § 5 |

## Out of scope (Phase 1 explicit non-goals)

Phase 1 release-gate-ində aşağıdakı tələblər qiymətləndirilmir; gələcək release-də ayrıca ADR/spec ilə aktivləşdirilə bilər:

- Load testing və stress testing (Dobby tək istifadəçili thick-client desktop tətbiqdir; çox-istifadəçi paralel yük profili out-of-scope-dur)
- Auto online update (P2 — `ADR-0047`)
- Remote logging / telemetry / diagnostics upload (`NFR-020` privacy default)
- Runtime language switching (P2 — `ADR-0043`)
- Full multi-language UI + RTL polish (P3 — `ADR-0043`, `ADR-0044`)
- Cross-platform support (Windows-only, `03_TECH_STACK_AND_VERSIONS.md`)

## Branch Shared Mode Interpretation

`NFR-001` remains true as "no mandatory internet dependency". In Branch Shared
mode it does not mean the app can authenticate or mutate shared authority while
LAN PostgreSQL is unavailable. Branch Shared shared operations fail closed when
the LAN authority cannot be reached or validated.

`NFR-007` is mode-aware:

- Standalone and DeviceLocal SQLite/local files remain under `ADR-0054`
  DB-at-rest/protected-storage requirements.
- Branch Shared PostgreSQL data-directory protection is governed by
  `ADR-0056` and `docs/specs/postgres-deployment-ops.md` operational protection
  floor.
- DeviceLocal secrets/profile material in Branch Shared mode remains protected
  by `ADR-0055`; PostgreSQL operational protection does not weaken DeviceLocal
  SQLite protection.

Privacy default-local remains unchanged: diagnostics are not uploaded
automatically, and Branch Shared does not introduce vendor telemetry.
