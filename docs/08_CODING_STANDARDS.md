# 08. Coding Standards

**Tarix:** 2026-04-29
**Status:** P1 canonical — applies from skeleton commit 1
**Scope:** Naming, file ordering, namespace boundaries (kod səviyyəsi), language idioms, dependency injection, MVVM layering, UI/OS boundary, SQL/Dapper conventions, migration style və common prohibitions

Bu spec coding standards, MVVM boundary və UI/OS adapter qaydalarının canonical owner-idir. Canonical mapping bu sənədin section-larında saxlanılır; ayrıca migration plan referensi tələb olunmur.

## 1. Sənədin məqsədi

Bu sənəd Dobby host, Admin Tool, plugin framework və host-distributed plugin-lər üçün kod yazılarkən təqib edilməli olan dil səviyyəsi, layout səviyyəsi və idiom səviyyəsi qaydalarını saxlayır. `docs/02_ARCHITECTURE_MAP.md` layer və namespace sərhədlərinin canonical owner-idir. Bu spec arxitektur qərarı gündəlik review olunan kod-a çevirən qərarları daşıyır.

## 2. Sənədin authority-si

- Bu spec aşağıdakıların canonical owner-idir: naming convention-lar, file structure və ordering, namespace boundaries (kod səviyyəsi), language idioms (`var`, nullability, async), dependency injection pattern-ları, MVVM layering, UI/OS boundary qaydaları, SQL/Dapper conventions, migration code style və common prohibitions.
- Arxitektur/layering qərarları üçün `docs/02_ARCHITECTURE_MAP.md` üstündür; spesifik ADR qərarları da bu sənəddən üstündür.
- Bu sənədə zidd commit review-də rədd edilir, yalnız ADR amendment ilə dəyişmək mümkündür.

## 3. Naming Conventions

- Public types, public members, enum members, commands, view models, services and DTO/read model types use `PascalCase`.
- Private fields use `_camelCase`; method parameters and local variables use `camelCase`.
- Async methods that perform I/O, DB, plugin calls or host service calls use the `Async` suffix and accept `CancellationToken` when cancellation can be observed.
- DTO/read model names end with `Dto`, `Summary`, `Details`, `Snapshot` or another explicit read-model suffix; persistence entity names do not leak into service/UI/plugin contracts.
- XAML view files and view models share the same base name (`UsersView.xaml` + `UsersViewModel`) unless an established host screen pattern requires a different suffix.

## Test and generator naming additions

- Physical test project layout `docs/09_BUILD_RUN_AND_RELEASE.md` tərəfindən idarə olunur və Phase 1-də `Dobby.UnitTests`, `Dobby.IntegrationTests`, `Dobby.SmokeTests` evidence split-idir. Həmin project-lərin içində test folder/class adları source project ownership-u mirror etməlidir.
- Test class adı tested type adı ilə başlayıb `Tests` suffix-i ilə bitməlidir.
- Test method naming formatı: `Method_Scenario_Expected`.
- `[ObservableProperty]` field-ləri `_camelCase` formatında qalır; generated property adı public `PascalCase` olur.
- `[RelayCommand]` method-ları command intent-i ilə adlandırılır və generated command adları review zamanı görünən public command surface kimi qəbul edilir.

## Test evidence conventions

- Test-lər observable davranışı assert edir: typed nəticə, persisted state, restart/crash recovery və negative trust case-ləri. Xüsusilə security, licensing, packaging və atomicity üçün "happy path keçdi" tək başına evidence deyil.
- Restart-durable davranış (throttle state, consumed nonce, quarantine, LKG license state) yalnız yeni host/composition instance-ı üzərində yenidən oxunaraq sübut olunur; eyni in-memory instance daxilində assert etmək restart proof deyil.
- Source inspection (regex, `Contains`, file-exists) test-ləri yalnız structural invariant-lar üçün icazəlidir: namespace/dependency direction, forbidden reference, layout və naming. Runtime, security və ya release readiness sübutu kimi istifadə oluna bilməz (`docs/02_ARCHITECTURE_MAP.md` § `Architecture Evidence Rule`).
- Time-dependent davranış deterministic `TimeProvider` seam ilə test edilir; `Thread.Sleep`/`Task.Delay` ilə real vaxt gözləmək evidence sayılmır.

## Documentation naming and capitalization additions

- `Dobby.Host` project/type identity-si üçün istifadə olunur.
- `Dobby host` running host process və product prose üçün istifadə olunur.
- `Dobby.SDK`, `Dobby.Contracts`, `Dobby.Core` və digər project adları exact
  project identity kimi yazılır; prose-da həmin project-lərin davranışı izah
  ediləndə project adı dəyişdirilmir.
- Bu rule bilingual prose-u qadağan etmir; yalnız project/type identity ilə
  running-process prose terminlərini qarışdırmamağı tələb edir.

## 4. File Structure və Ordering

- Use file-scoped namespaces in C# files.
- `using` directives stay at the top, sorted with `System.*` first, then external packages, then Dobby namespaces.
- One public top-level type per file. Small private helper types may live in the same file only when they are not reused elsewhere.
- File/folder layout mirrors project ownership from `docs/02_ARCHITECTURE_MAP.md`; do not create cross-layer utility folders that mix UI, persistence and application logic.
- Type order: constants/static readonly fields, fields, constructors, public members, protected/private helpers. Keep nested types at the bottom.

## 5. Namespace Boundaries

- Namespace names mirror project and folder boundaries. A file under `Dobby.Application/...` uses a `Dobby.Application...` namespace, not `Dobby.Core` or `Dobby.UI`.
- UI namespaces do not reference persistence namespaces directly.
- Plugin-visible contracts live only in `Dobby.Contracts`/`Dobby.SDK` surfaces approved by the architecture map.
- Infrastructure ownership follows `ADR-0040` `HD-008` / `DM-095`: owner projects keep host-internal behavior and use-case contracts, while `Dobby.Infrastructure` provides documented primitive/backing adapters only. Direct owner-project references to `Dobby.Infrastructure` are allowed only when the architecture map shows that edge explicitly.
- Shared helpers must have an owner namespace. `Common`, `Utils`, `Helpers` and `Extensions` dumping-ground namespaces are not accepted.

Slash-separated architecture-map groupings use this hybrid namespace mapping
convention:

1. The project label is `Dobby.<ProjectRole>`.
2. The group label is PascalCase concatenation of architecture-map words.
3. If the group label's trailing word duplicates the parent project role, that
   trailing word is dropped. Example: `Plugin Storage Contracts` under
   `Dobby.Contracts` becomes `PluginStorage`.
4. The final namespace is `<Dobby.ProjectLabel>.<NormalizedGroupLabel>`.

The explicit table below is canonical and overrides the general rule where the
architecture-map label is descriptive but the namespace is semantically named.
The source pointer may name an exact architecture-map row or a row/prose slice;
it does not create additional architecture-map rows:

| Source pointer | Canonical namespace placement |
|---|---|
| Architecture map row 90 `Versioning` | `Dobby.Core.Versioning` |
| Architecture map row 21 `Plugin Storage Contracts` | `Dobby.Contracts.PluginStorage` |
| Architecture map rows 18 `Plugin Contracts` and 19 `Extension Point Contracts`; plugin page hosting prose in ADR-0049 | `Dobby.Contracts.PluginPages` |
| Architecture map row 81 `Developer SDK / Extensibility Helpers`; ADR-0021 `PluginBase` namespace | `Dobby.SDK.PluginFramework` |
| Architecture map row 55 `Messaging UI Bridge` | `Dobby.Messaging.UiDispatch` |
| Architecture map row 64 `Command / Action Pipeline` prose: host-internal print/file-output decisions | `Dobby.Application.Printing` |
| Architecture map row 64 `Command / Action Pipeline` prose: physical print/OS adapter implementation | `Dobby.Infrastructure.Printing` |
| ADR-0038 login-visible public auth/recovery surface and ADR-0041 login-first startup policy | `Dobby.Host.Login` |
| Sprint 6 decision `D-21` Settings/About source orchestration authority | `Dobby.Host.Settings` only for authenticated Settings/About source orchestration, bindable UI-facing source models, Host wiring, and Host-mediated Security/Audit coordination; durable Settings preference ownership remains `Dobby.Configuration`, and concrete `HostConfiguration` access remains Host-wired outside `Dobby.Configuration`. |
| Architecture map row 88 `Hardware Identity / Device Binding` | `Dobby.Infrastructure.HardwareIdentity` |
| Architecture map row 27 `Plugin Packaging`; Sprint 8 `HD-S8-003` installed-state/cache authority | `Dobby.PluginFramework.Packaging` only for `.dobbyplugin` package reading/validation/fingerprints and domain ports/typed outcomes for installed state/cache; concrete durable adapters, migrations, and `Dobby.Infrastructure` references stay outside `Dobby.PluginFramework`. |
| Architecture map row 38 `Plugin Trust Verification`; ADR-0032/ADR-0062 trust-resource model; Sprint 8 `HD-S8-002` | `Dobby.Security.Trust` only for plugin signature/trust verification and selected embedded public-key resource access; runtime filesystem/configuration trust loading remains forbidden. |
| Architecture map row 79 `Shell Composition / Hosting`; ADR-0049 plugin page hosting; Sprint 8 `HD-S8-004` | `Dobby.Shell.PluginPages` only for host-owned plugin page render registry and approved model-to-host-view presentation mapping; plugin-provided WPF objects, XAML paths, view keys, and assembly-qualified view identities remain forbidden. |
| Architecture map row 1 `Dobby.Host`; ADR-0041 login-first startup policy; Sprint 8 `HD-S8-004` | `Dobby.Host.Plugins` only for post-login host composition and coordination of plugin runtime activation, audit/diagnostics mapping, session cancellation, and Security registry seeding; plugin framework logic, trust verification, and authorization policy/enforcement remain in their owner projects. |

Table entries are canonical; the rule applies only when no table row exists.
New public/plugin-visible or skeleton-critical host-internal namespace placements
require a table row or an explicit docs amendment before creation.

## 6. Language Idioms

- Nullable reference types remain enabled. Do not silence nullable warnings with `!` unless the surrounding invariant is documented in the same method.
- Prefer explicit types where the right side does not make the type obvious; `var` is allowed for object creation, LINQ projections and long generic types when readability improves.
- Do not block async code with `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` on UI, plugin, service or persistence paths.
- Expected business failures follow `docs/04_RESULT_VS_EXCEPTION_POLICY.md`; exceptions are for unexpected infrastructure or programmer failures.
- Expected `Result` failure-ları runtime fault kimi qeyd olunmur. Yalnız exception və ya explicit plugin-defect outcome fault/quarantine policy-sinə təsir edir (`docs/04_RESULT_VS_EXCEPTION_POLICY.md` qayda #9, `ADR-0059`).
- Async UI event handler-ləri həqiqətən asynchronous application əməliyyatı çağırır, `CancellationToken` ötürür və arbitrary synchronous I/O-nu lifetime, progress və error ownership-ini itirəcək şəkildə `async void`/fire-and-forget arxasında bükmür.
- Do not catch and swallow exceptions. Catches either translate to a typed result, add diagnostic context and rethrow, or fail closed at a public boundary.

## 7. Dependency Injection

- Constructor injection is the default for services, view models, adapters and application components.
- Composition root-lar iki explicit scope təqdim edir: `PreAuthRuntime` və authentication uğurundan sonra yaradılan session-scoped `AuthenticatedRuntime` (`docs/02_ARCHITECTURE_MAP.md` § `Runtime Composition Scopes`, `ADR-0061`). Registration pre-auth allow-list-dən kənar service-i login render path-ında resolve etmir.
- Constructor, factory və property initializer gizli background iş başlatmır: file/DB/network I/O, timer, thread, scan və plugin activation yalnız explicit çağırılan async əməliyyat daxilində baş verir. Belə iş görən service-i pre-auth path-da construct etmək özü qadağan olunmuş işdir.
- DI registration happens in composition-root modules owned by the host or feature; random runtime service locator usage is prohibited.
- Owner projects do not self-register infrastructure primitive adapters by default. `Dobby.Host` composition root owns the default wiring between owner behavior and `Dobby.Infrastructure` primitives unless an ADR/architecture-map row explicitly assigns a different wiring point.
- Singleton services must be stateless or explicitly thread-safe. User/session-specific state is scoped to the current session boundary, not stored in static mutable fields.
- UI services/adapters expose abstractions to view models. Concrete WPF/window/dialog implementations stay behind host-owned adapters.
- Background work dependencies must receive explicit cancellation and correlation/context values instead of reading ambient mutable state when enqueueing.

## 8. MVVM Layering

- Views render, bind and forward user input only; business rules, authorization decisions and persistence calls do not live in code-behind.
- ViewModels do not hold `Window`, `UserControl`, `Page`, `FrameworkElement` or other View references.
- ViewModels receive dependencies through constructor injection and expose bindable state/commands. They do not resolve services from a global container.
- ViewModels may expose DTO/read model state, form state and command state; they do not expose persistence entities directly.
- Code-behind is limited to view-only wiring that cannot reasonably be expressed through binding, such as focus handoff, control-specific interop or animation startup.
- Navigation, dialogs, notifications, file pick/save and OS integration flow through host-owned services/adapters.

## 9. UI və OS Boundary

- ViewModels never open OS file dialogs, folder pickers, message boxes or shell processes directly.
- File pick/save workflows use host-owned request/response DTOs such as purpose, allowed extension list, default file name, initial logical location and selected result.
- The adapter that talks to WPF/Windows dialog APIs lives in the UI/infrastructure boundary, not in application services or plugin-visible contracts.
- Plugin-facing workflows receive host-approved file descriptors or streams; plugins do not receive arbitrary host paths unless the feature explicitly allows that path exposure.
- Plugin SDK/contract boundary immutable declarative model-lər və host-mediated command-lardan ibarətdir. WPF tipləri, `IServiceProvider`, filesystem/network handle-ları, `Stream` sahibliyi və raw delegate contract boundary-ni keçmir (`ADR-0021`, `ADR-0049`, `ADR-0059`).
- Dialog result handling is typed (`accepted`, `cancelled`, `failed`) and cancellation is not logged as an error.
- OS integration must be testable with fake adapters in unit/integration tests.

## 10. SQL və Dapper Conventions

- SQL/Dapper code uses parameterized queries only; string interpolation is not used for values.
- SQL text is kept readable: one clause per line for multi-clause queries, explicit column lists for `SELECT`, `INSERT` and `UPDATE`, no `SELECT *` in production queries.
- Dapper parameter objects use named properties that match SQL parameter names.
- Persistence methods return entities only inside persistence/infrastructure boundaries; application services map to DTO/read models before returning.
- Connection, transaction and cancellation ownership must be explicit in method signatures or local scopes.
- Domain/security state dəyişikliyi ilə onun audit/outbox record-u provider-owned transaction/unit-of-work daxilində yazılır. Application workflow bir-birindən asılı olmayan repository çağırışlarını sıralayaraq atomicity iddia edə bilməz; commit-dən əvvəl heç bir success nəticəsi qaytarılmır (`docs/04_RESULT_VS_EXCEPTION_POLICY.md` qayda #11, `ADR-0061`).

## 11. Migration Style

- Migration file names sort deterministically and include an ordered prefix plus a short description.
- Migrations run through the documented migration runner and keep transaction scope explicit.
- Migration code must be diagnosable: failed migration logs the migration id/name and typed reason without leaking secrets.
- Pre-login bootstrap is not a full migration path; it is limited to the closed login-visible schema set documented in `docs/specs/storage-and-data-protection.md`.
- Migration scripts are written so re-running an already-applied migration fails harmlessly or is skipped by migration metadata.

## Migration naming and style additions

- SQL migration file adları deterministic sort üçün `{version:D4}_{description}.sql` formatından istifadə etməlidir.
- `description` qısa, snake_case və domain-specific olmalıdır.
- Pre-login bootstrap compatibility davranışı `docs/specs/storage-and-data-protection.md` sahibidir; coding standards yalnız migration naming/style qaydasını saxlayır və həmin spec-ə cross-reference verir.

## Untrusted input və cryptography conventions

- Untrusted byte stream-ləri (plugin package, license artifact, recovery response, import faylı) materialize etməzdən əvvəl incremental şəkildə, explicit limit-lərlə validate olunur: declared vs actual size, entry count, per-entry və total uncompressed size, compression ratio və nested archive qadağası. Limit yoxlaması allocation-dan sonra deyil, əvvəl gəlir (`ADR-0059`).
- Archive extraction hər entry üçün final absolute destination-ı hesablayır (`Path.GetFullPath`), verified root altında olduğunu təsdiqləyir, mövcud faylı overwrite etmir və symlink/reparse point izləmir. Traversal, absolute path, Windows reserved name və duplicate/case-collision entry-lər fail-closed rədd olunur.
- Cryptographic encoding kod və test-lərdə explicit yazılır: ECDSA signature format overload default-una güvənilmir (`DSASignatureFormat.IeeeP1363FixedFieldConcatenation`), base64url unpadded variant explicit seçilir və length/format yoxlanılır.
- Canonical signed payload-lar version, purpose və key ID/domain separation daşıyır. Bir purpose üçün buraxılmış key başqa purpose-un artifact-ını verify etmək üçün istifadə oluna bilməz (`docs/02_ARCHITECTURE_MAP.md` § `Key Purpose Separation`).
- Range/overflow: untrusted ölçü, offset və count dəyərləri checked arithmetic və ya explicit range yoxlaması ilə işlənir; negative/overflowing dəyər typed fail-closed nəticədir.
- Fail-closed nəticə mövcud last-known-good persisted state-i pozmur: invalid candidate artifact heç bir halda valid state-i silmir və ya üzərinə yazmır (`ADR-0060`).

## Machine-readable value formats

Host-owned structured file və machine-readable value surface-ləri aşağıdakı formatları izləməlidir:

| Surface | Convention |
|---|---|
| JSON | UTF-8, deterministic indentation, invariant property names |
| Stored timestamps | ISO 8601 UTC |
| User-facing localized date/time | UI culture formatting, P1-də `az-AZ` |
| Decimal values in stored machine-readable formats | invariant `.` separator |
| Logs and diagnostics export snippets | structured timestamp və correlation id; raw secret/redaction qaydası `docs/specs/audit-and-diagnostics.md` sahibidir |

Security-relevant log yazıları typed structured field-lərdən istifadə edir və
allow-list redaction tətbiq olunur (deny-list deyil). Raw exception yalnız
sanitize olunduqdan və correlation ID ilə əlaqələndirildikdən sonra
diagnostics-only kimi saxlanılır; user-facing mesaj, audit record və export
olunan support material raw exception message, stack trace, DB path, SQL,
connection string və ya secret daşımır (`docs/04_RESULT_VS_EXCEPTION_POLICY.md`
qayda #10, `ADR-0019`).

## 12. Common Prohibitions

- No direct OS dialogs from view models.
- No UI-thread blocking waits.
- No empty catch blocks.
- No sensitive values in logs.
- No public mutable fields on contract models.
- No authorization decision based only on hidden/disabled UI.
- No persistence entity crossing service, UI or plugin-visible boundaries.
- No static mutable state for current user/session/customer/deployment context.
- No ad-hoc SQL string concatenation for values.
- No catch-all service that bypasses operation-specific authorization or validation.
- No WPF type, `IServiceProvider`, filesystem/network handle or raw delegate on the plugin contract boundary.
- No allocation of untrusted archive/stream content before its declared limits are validated.
- No default-format cryptographic call whose signature/encoding format is implied rather than specified.
- No source-text or file-existence assertion presented as runtime, security or release evidence.

## Review checklist additions

- Magic string və magic number istifadə etmə; domain constants, enums və typed value objects seç.
- Public contract-facing API üçün XML documentation tələb olunur; plugin-facing interface, DTO property və public extension contract metodları bura daxildir.
- XAML-də 3 və ya daha çox attribute varsa hər attribute ayrıca sətrə çıxmalıdır.
- `#region`, `dynamic`, `goto`, UI thread-də `Thread.Sleep`, `Console.WriteLine` və public mutable field istifadə edilməməlidir; mövcud contract-model qadağası bu qaydanın ən sərt review gate-idir.
- Fayllar UTF-8 without BOM, final newline və no trailing whitespace ilə saxlanmalıdır.
- Localization/resource key-ləri host central resource namespace və plugin assembly/package ownership sərhədini qarışdırmamalıdır; concrete plugin prefix syntax canonical edilmir.

## 13. References

- Sections 3-7 — naming, file ordering, namespace, language idioms and dependency injection conventions.
- Sections 8-9 — MVVM boundary and UI/OS adapter boundary.
- Sections 10-12 — SQL/Dapper, migration style and common prohibitions.
- `docs/02_ARCHITECTURE_MAP.md` (architecture/layer canonical owner)

## Storage Provider Guardrails

Storage code and future skeleton shape must keep provider semantics explicit:

- Do not expose `DeviceLocal`, `BranchShared` or future `OrganizationShared`
  scope selection to plugin manifests or plugin-facing storage APIs unless a
  later plugin storage ADR approves it.
- Do not treat physical provider names as business semantics. Repositories and
  application services should speak in domain/use-case terms; composition/root
  provider resolution owns SQLite vs PostgreSQL binding.
- Do not use shared-folder/network-drive SQLite as a substitute for Branch
  Shared PostgreSQL.
- Do not introduce ad-hoc string-built SQL for values. Structured command
  parameters and provider-specific migration/DDL ownership stay explicit.
- Future PostgreSQL DDL must avoid optional extension dependencies unless an
  ADR/spec explicitly approves one.

These guardrails are documentation-stage rules now and skeleton-stage rules only
after a separate implementation phase is approved.

## Revision History

- **2026-08-26:** `ADR-0058`…`ADR-0061` reconciliation. `2026-08 Engineering
  Amendment` bölməsi ləğv edildi; qərarları normativ body section-lara
  köçürüldü — composition scope və hidden-work qadağası (§7), expected-`Result`
  vs runtime fault və async UI handler qaydaları (§6), provider-owned
  unit-of-work (§10), yeni `Untrusted input və cryptography conventions`
  bölməsi, plugin contract boundary qadağası (§9 və §12), security logging
  redaction qaydası (`Machine-readable value formats`) və yeni
  `Test evidence conventions` bölməsi.
