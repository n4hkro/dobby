# Audit and Diagnostics Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-011`, `FR-027`, `FR-033`, `FR-034`, `AC-010`, `AC-033`, `AC-053`, `AC-054`, `NFR-011`, `NFR-020`, `NFR-027`, `ADR-0019`, `ADR-0061`

## Məqsəd

Bu spec audit trail, technical diagnostics, diagnostics export və privacy-default-local davranışını tək yerdə bağlayır.

## Prinsiplər

- Audit compliance və accountability üçündür: kim, nə vaxt, nə etdi.
- Diagnostics troubleshooting və support üçündür: nə baş verdi, hansı texniki iz var.
- Audit technical log sink deyil.
- Diagnostics audit-in əvəzi deyil.
- Dobby default olaraq vendor-a telemetry, usage analytics, crash report və diagnostics upload etmir.
- Vendor/support-a məlumat yalnız explicit user/Owner action ilə local export package kimi çıxarıla bilər.

## Audit Event Taxonomy

The audit spec owns exact event names and operationalizes the minimum payload. ADR-0019 owns the audit-vs-diagnostics boundary rule. Existing event names are not renamed as part of this consolidation.

| Category | Event names |
|---|---|
| Auth və Session | `user.login`, `user.login.failed`, `user.logout`, `session.lock`, `session.unlock`, `session.expired`, `session.invalidated` |
| User Management | `user.create`, `user.update`, `user.password.change`, `user.password.reset`, `user.activate`, `user.deactivate` |
| Permission | `permission.grant`, `permission.revoke`, `permission.denied`, `owner.protection.denied` |
| Plugin install/trust | `plugin.install.attempted`, `plugin.install.verified`, `plugin.install.rejected`, `plugin.install.signature-failed`, `plugin.install.trust-failed` |
| Plugin lifecycle | `plugin.enable`, `plugin.disable`, `plugin.uninstall`, `plugin.activation.skipped`, `plugin.activation.rejected`, `plugin.runtime.faulted`, `plugin.runtime.quarantined`, `plugin.lifecycle.v1-adapter-used` |
| Recovery və License | `recovery.export`, `recovery.import`, `license.import.attempted`, `license.import.succeeded`, `license.import.rejected`, `license.enforcement.denied`, `license.enforcement.warning` |
| Customer/deployment context | `customer-deployment-context.provision` |
| Diagnostics Export | `diagnostics.export.created` |
| Security Verification | `security.verification.failed` |
| Persistence maintenance | `persistence.snapshot.created`, `persistence.restore.requested`, `persistence.restore.completed` |

Bu cədvəl closed registry-dir: runtime audit event name registry (`Dobby.Audit.AuditEventName.All`) və onun ordered registry test-i bu cədvəllə bir-bir uyğun olmalıdır. Yeni event name əlavə etmək bu spec-in dəyişməsini tələb edir; registry-də olub cədvəldə olmayan (və ya əksinə) ad defect sayılır.

### Persistence maintenance event semantics

Backup/restore maintenance-in promotion addımı filesystem store-set move-udur, ona görə audit record-u ilə **eyni provider transaction-ını paylaşa bilmir** (`ADR-0061` tək-transaction qaydası yalnız in-database state dəyişikliyinə tətbiq olunur). Bu boundary üçün qayda:

- `persistence.snapshot.created` snapshot cəhdinin nəticəsini yazır: `success` halında `outputName` (yalnız bare artifact adı) və `sha256`, failure halında `outputName` və stable `reasonCode`.
- `persistence.restore.requested` **destructive transition-dan əvvəl**, hələ **current** store-a yazılır (`candidateName`). Restore pipeline-ının immutable-evidence merge addımı bu record-u promote olunan store-a daşıyır, beləliklə əvəz olunmuş store heç vaxt "restore icra olundu" evidence-i olmadan qalmır (`docs/specs/backup-restore-migration.md` § 2.1–2.2).
- Intent record-unun yazılması uğursuz olarsa əməliyyat **fail-closed** olur: live store-a toxunulmur və nəticə typed persistence failure kimi qaytarılır.
- `persistence.restore.completed` promotion-dan sonra yazılır: `success` halında `candidateName` və `quarantineName`, failure halında `candidateName` və stable `reasonCode`.
- Missing quarantine confirmation refusal-ı `persistence.restore.requested` + `denied` + `reasonCode=QuarantineConfirmationMissing` kimi yazılır; authorization refusal-ı isə `permission.denied` kimi.
- Bu event-lərin `details` field-ləri **heç vaxt** full path daşımır: yalnız bare artifact/quarantine adı yazılır (`NFR-004`, aşağıdakı write-time redaction boundary).
- Bu pair-in runtime proof-u davranış testi ilə verilir: production-emitted intent record-un promote olunmuş store-dan oxunması. Source scan, mövcud file və ya "tests green" bu gate-i qapatmır (`NFR-027`).

## Minimum Audit Payload

Hər audit entry minimum bu field-ləri daşıyır:

- `timestamp` — ISO 8601 UTC;
- `actor` — user ID və ya `system`;
- `action` — typed action identifier; persistence schema-da English code (məsələn `user.login`, `permission.grant`) saxlanır, UI display tərcüməsi localization key (`audit.action.user.login`) üzərindən gəlir. Persistence dili UI culture-dan asılı deyil;
- `target` — entity ID, tətbiq olunursa;
- `result` — `success`, `failed` və ya `denied`;
- `correlationId` — `ADR-0029` ilə uyğun correlation ID.

Action-specific details ayrıca `details` field-ində saxlanıla bilər. `details`
raw secret, raw password, raw license artifact/detail, private key, raw recovery
nonce və raw recovery response materialı daşımamalıdır. Recovery correlation üçün
yalnız non-reversible `nonceHash` kimi redacted representation qəbul olunur.

`details` field-lərinin icazəsi **per-event closed allow-list** ilə verilir və
write-time enforcement `Dobby.Audit.AuditDetailsSanitizer`-dədir: allow-list-də
olmayan key emission anında `InvalidOperationException` ilə fail-closed atılır.
Buradan iki məcburi nəticə çıxır:

1. bu spec-in və ya hər hansı sibling spec-in "icazəlidir"/"minimumdur" deyə
   göstərdiyi hər `details` field-i həmin event-in allow-list-ində qeydiyyatda
   olmalıdır — əks halda spec-uyğun emission runtime-da atomic mutation
   boundary-si daxilində uğursuz olur və dokumentasiya driftini fail-closed
   outage-a çevirir;
2. `nonceHash` recovery event-ləri (`recovery.export`, `recovery.import`) üçün
   allow-list-də mövcuddur və ledger-dəki `RecoveryRequests.NonceHash` sütunu ilə
   **eyni** non-reversible təmsil (lowercase hex SHA-256) olmalıdır; əks halda
   audit qeydi replay ledger-i ilə korrelasiya edilə bilmir.

Durable audit record structured security/business record-dur, exception dump
deyil. Audit `details` raw exception text, stack trace, SQL/query text,
connection string, full local filesystem path, artifact body (raw package,
license, recovery payload) və secret dəyər daşımır. Səbəb stable reason code,
allow-list edilmiş safe field-lər və `correlationId` ilə ifadə olunur; raw
technical iz yalnız diagnostics boundary-də qalır (`ADR-0019`,
`docs/04_RESULT_VS_EXCEPTION_POLICY.md` qayda #10).

## Audit Storage və Visibility

Audit storage append-only olmalıdır. Public API update/delete verməməlidir.

Security/domain state dəyişikliyi və onun tələb olunan audit/outbox record-u **tək
provider transaction** daxilində yazılır: commit uğursuz olarsa nəticə failure-dır
və nə state dəyişikliyi, nə audit record məntiqi olaraq tətbiq olunmuş qalır.
External sink-lər (file export, future forwarder) commit-dən sonra idempotent
outbox consume edir; outbox delivery retry audit record-unu dublikat etmir
(`ADR-0061`, `docs/04_RESULT_VS_EXCEPTION_POLICY.md` qayda #11, `NFR-025`).

Audit storage pre-login allowed workload-dadır (`ADR-0041`) və tətbiqin ilk launch-da boş schema ilə initialize olunur. Pre-auth audit hadisələri (məsələn `user.login.failed`) birbaşa audit DB-yə yazılır; ayrıca file-first fallback dizayn olunmamışdır.

Audit data P1-də yazılır. Audit viewer UI P1 blocker deyil və P2/P3-də ayrıca qurula bilər.

Daha geniş audit viewer və advanced diagnostics UI üçün `docs/02_ARCHITECTURE_MAP.md` Future Scope cədvəlinə bax.

Audit log-a baxış ayrıca permission/capability ilə idarə olunmalıdır. Audit viewer açıldıqda:

- user/security/compliance hadisələri göstərilir;
- diagnostics raw technical detail göstərilmir;
- plugin raw exception stack trace-i audit viewer-ə qarışdırılmır.

## Audit retention enforcement note

Cleanup enforcement post-login async maintenance task kimi işləyə bilər; login-first startup first-paint path-ini bloklamamalıdır. Bu note audit retention floor-u aşağı salmır və exact scheduler/runner yalnız həmin maintenance implementation phase-də bağlanır.

## Diagnostics

Diagnostics local technical troubleshooting datasıdır:

- technical logs;
- exception details;
- plugin/runtime health details;
- package/license verification diagnostic detail;
- localization missing-key diagnostics;
- performance/startup measurement details.

Localization missing-key hadisəsi audit deyil, diagnostics-dir.

License mismatch və package verification failure kimi hallarda user-facing mesaj non-disclosing qalır; detailed host-side səbəb diagnostics və lazım olduqda audit `details` içində saxlanılır.

Audit log retention default-u minimum 365 gündür və config bu dəyəri yalnız artıra bilər. Audit retention floor aşağı salınmır; canonical config key `Dobby:Audit:RetentionDays` `ADR-0027` P1 Configuration Schema tərəfindən idarə olunur. Audit export/backup və legal hold kimi geniş retention mövzuları P2/P3-də ayrıca policy ilə dərinləşə bilər.

Technical log faylları rolling file qaydası ilə rotate olunur. Default technical diagnostics retention 30 gündür və əlavə finite total size cap ilə birlikdə işləyir; canonical config keys `Dobby:Diagnostics:TechnicalLogRetentionDays` və `Dobby:Diagnostics:TechnicalLogTotalSizeCapMegabytes` `ADR-0027` tərəfindən idarə olunur. Technical loglar unlimited böyüməməlidir. Finite total size cap konfiqurasiyası olmadan `AC-052` release verification fail verir. Config 30 gün default-u azalda bilər yalnız explicit support/privacy policy ilə; secret redaction boundary heç vaxt zəiflədilmir.

Reopen only if diagnostic volume measurements, support policy, or disk policy
changes.

Technical log severity səviyyəsi (`Verbose`/`Debug`/`Information`/`Warning`/`Error`) appsettings/config-dən rebuild-siz dəyişdirilə bilməlidir; konkret sink/provider binding tech stack-də saxlanılır.

Write-time redaction boundary: technical log/diagnostics layer raw password,
password hash, token, private key, session material, remember-me secret,
recovery secret, raw recovery nonce, raw recovery response materialı və raw
`.dobbylicense` payload-larını yazma anında qəbul etmir. Bu boundary Diagnostics
Export redaction-undan əvvəldir: secret heç vaxt local log faylına düşmür,
sonradan export-da silinmir. Exception logging-də raw payload context-i yox,
redaction-applied summary saxlanır.

Redaction **structured və allow-list based**-dir: log/audit/export writer yalnız
explicit icazə verilmiş typed field-ləri buraxır, qalan payload default olaraq
kənarda qalır. Regex/pattern-based scrubbing yalnız defense-in-depth əlavəsidir;
tək secret-detection mexanizmi kimi qəbul edilmir və allow-list-in yerini tutmur.

## Diagnostics Events

### `localization.missing-key`

`localization.missing-key` diagnostics-only event-dir; audit trail-ə yazılmır.
Event localization resource lookup fail olduqda və production fallback chain
istifadə edildikdə yaranır.

| Field | Meaning |
|---|---|
| `culture` | Lookup zamanı istifadə olunan UI/resource culture, məsələn `az-AZ`. |
| `resourceKey` | Host və ya plugin-owned resource key, məsələn `Plugins.InstallButton`. |
| `resourceScope` | Closed scope: `shell`, `built-in`, və ya `plugin:<pluginId>`. |
| `fallbackChainResult` | Closed result: `found-in-fallback` və ya `not-found-anywhere`. |

Severity floor `Warning`-dir. Production-da event per process lifetime
`(culture, resourceKey, resourceScope)` tuple-ı üzrə dedupe olunur: ilk occurrence
full event kimi yazılır, sonrakılar in-memory counter artırır. Diagnostics export
AC-033 support package-ında event və final counter snapshot-u daxil ola bilər.

`resourceKey` host/plugin-owned constant sayılır, raw user input deyil və P1-də
redaction tələb etmir. `culture` OS/UI metadata-dır. Event audit deyil və
NFR-013 missing-key audit qadağasını dəyişdirmir.

## Plugin diagnostics and measurement boundary

Plugin-facing diagnostics/measurement API varsa, span/metric/event-lər host-owned diagnostics və redaction boundary-sinə adapt olunur. Plugin host-dan kənar ayrıca telemetry channel, ayrı log file və ya vendor upload path yaratmır.

Diagnostics payload-ları `docs/specs/shell-ui.md` background operation status vocabulary-sini dəyişdirmir və `docs/specs/audit-and-diagnostics.md` write-time redaction boundary-sinə tabe qalır.

## Diagnostics Markers

Diagnostics aşağıdakı kritik flow-larda timing və failure marker-larını saxlamalıdır:

- startup;
- shell composition;
- plugin package validation;
- plugin activation;
- license import;
- lock/unlock;
- storage provider initialization;
- backup/restore;
- shared data import/export.

`backup/restore` marker-ı host-owned lokal support snapshot/verified restore üçün P1-core-da aktivdir (`FR-034`, Cədvəl 1 Row 91): timing və failure marker-ları staging verification, exclusive ownership acquisition, evidence overlay, quarantine və promotion addımlarını əhatə edir. Cross-device restore/customer-device migration axınının əlavə marker-ları həmin scope P2-dən P1-ə keçdikdə aktivləşir. `shared data import/export` marker-ları həmin feature P2/future scope-dan P1-ə daxil olduqda aktivləşir. Digər marker-lar P1-də mövcuddur.

## No-Telemetry Default

Default olaraq Dobby aşağıdakıları vendor-a avtomatik göndərmir:

- usage analytics;
- crash report;
- telemetry;
- diagnostics;
- health report;
- plugin/runtime error detail.

Bu default davranışı dəyişmək ayrıca explicit product/security qərarı tələb edir.

## Diagnostics Export

Diagnostics export yalnız explicit user/Owner action ilə başlayır və local support package yaradır. Export avtomatik vendor upload deyil.

Host-owned support diagnostics export bütün plugin-license lifecycle state-lərində
(`Active`, `Grace`, `ReadOnlyExpired`, `Pending`, `Denied`) əlçatan qalır: license
və capability gate-ləri plugin/domain data export-larına tətbiq olunur, licensing
problemini diagnoz etmək üçün lazım olan minimum host support bundle-ına yox
(`docs/01_PRODUCT_SCOPE.md`, `ADR-0060`).

Export package seçilmiş support məlumatlarını daşıya bilər:

- app version/build;
- device support code və non-secret environment summary;
- installed plugin id/version/status summary;
- recent diagnostics logs;
- relevant audit event summaries;
- non-secret package/license verification summaries;
- correlation IDs və timestamp-lər.

Export package aşağıdakıları daşımamalıdır:

- raw password və password hash;
- token;
- private key;
- session material;
- remember-me secret;
- recovery secret;
- raw recovery nonce və raw recovery response materialı;
- raw license artifact və raw license binding detail;
- full local filesystem paths, əgər support üçün zəruri deyilsə;
- customer business data, explicit user seçimindən kənardırsa.

Diagnostics export action özü `diagnostics.export.created` action ID-si ilə audit trail-ə yazılır.

## Acceptance Hooks

- `AC-010` audit event taxonomy-nin görünməsini yoxlayır.
- `AC-017` missing localization key-in diagnostics olduğunu, audit olmadığını yoxlayır.
- `AC-020`, `AC-032` non-disclosing UI + diagnostics detail boundary-ni yoxlayır.
- `AC-033` no-telemetry default və explicit diagnostics export davranışını yoxlayır.
- `AC-053` state + audit/outbox tək unit of work qaydasını fault injection ilə yoxlayır.
- `AC-054` persistence maintenance event pair-inin icra olunmuş snapshot/restore ilə yazılmasını və intent record-un promotion-dan sağ çıxmasını yoxlayır.

## Branch Shared Audit Addendum

Branch Shared authoritative audit is stored in PostgreSQL. DeviceLocal
diagnostics remain local technical/support evidence and are not promoted to
authoritative audit unless the authoritative Branch Shared audit write succeeds.

Canonical Branch Shared pre-auth audit events:

- `user.login.failed`;
- `recovery.export`;
- `recovery.import`;
- `security.verification.failed`.

The event-name whitelist is application-layer behavior. PostgreSQL INSERT grants
do not prove event-level filtering.

If PostgreSQL is unreachable before the canonical pre-auth audit INSERT occurs,
the attempt is DeviceLocal diagnostics/setup evidence only. Pending local audit
buffering is deferred and reopens only if security review rejects direct
pre-auth PostgreSQL audit INSERT or a later ADR/spec amendment explicitly
approves a signed DeviceLocal pending-audit buffer.

BranchSetup evidence:

- pre-schema setup report is authoritative setup evidence and is not re-injected
  into PostgreSQL later;
- post-schema setup events may write `source=BranchSetup` audit events;
- audit read/export grants are not part of P1 runtime support; audit viewer and
  export read paths are deferred by `DM-089`;
- audit origin uses the `DM-091` model below.

### Branch Shared audit origin payload

Branch Shared audit payload carries these origin fields:

| Field | Meaning |
|---|---|
| `source` | Closed vocabulary: `Host`, `BranchSetup`, `Support`. |
| `originDeviceId` | Branch Shared server-issued `deviceId` after registration. |
| `localDeviceRef` | Optional support-safe event-time snapshot used before server registration. |

`originDeviceId` is nullable only before Branch Shared device registration. In
that case, `localDeviceRef` may be recorded so support can correlate the setup
or pre-registration event without exposing raw MachineGuid, full license
fingerprint, protected secrets or privacy-sensitive hardware material.

`localDeviceRef` is the audit snapshot term only. The live Branch Shared
connection-profile reference is `deviceProfileRef`; audit `localDeviceRef`
preserves the support-safe value observed when the event was written and does
not follow later profile changes.

Adding a new `source` value requires an ADR/spec amendment.

## Revision History

- **2026-08-26:** Audit event taxonomy closed-registry qaydası əlavə edildi;
  `plugin.lifecycle.v1-adapter-used` (runtime registry-də mövcud olan, cədvəldə
  itmiş event) Plugin lifecycle sətrinə bərpa edildi; `persistence.snapshot.created`,
  `persistence.restore.requested`, `persistence.restore.completed` event-ləri və
  onların pre/post pair semantics-i (`FR-034`, `AC-054`) əlavə edildi;
  `backup/restore` diagnostics marker-ının P1-core statusu düzəldildi.
- **2026-08-26:** `ADR-0058`…`ADR-0061` reconciliation. `2026-08 Hardening
  Amendment` bölməsi ləğv edildi; audit-in structured-record (exception dump
  deyil) qaydası `Minimum Audit Payload`-a, state+audit/outbox tək transaction və
  idempotent post-commit delivery `Audit Storage və Visibility`-yə,
  license-state-dən asılı olmayan host support export `Diagnostics Export`-a,
  allow-list redaction (regex yalnız defense-in-depth) isə write-time redaction
  boundary paraqrafına köçürüldü.
