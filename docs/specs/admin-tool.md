# Admin Tool

**Tarix:** 2026-04-29
**Status:** Split spec — P1-core minimal owner recovery response generator; P1-if-time/P2 full Admin UI
**Phase:** P1-core (`create-recovery-response`, scripted CLI contract); P1-if-time/P2 (full Admin UI)
**Scope:** Vendor-side Admin Tool UI/CLI boundary, shared application-service reuse, recovery/signing artifact generation, and no business-logic duplication with the host.
**Sources:** ADR-0023, ADR-0057, ADR-0058, ADR-0059, ADR-0060, ADR-0062, ADR-0063, NFR-004, NFR-014, NFR-026, NFR-027, AC-004, AC-018, AC-034, AC-036, docs/schemas/dobby-plugin-license-v3.schema.json, docs/09_BUILD_RUN_AND_RELEASE.md

Phase split: `create-recovery-response` is P1-core because Owner recovery is Phase 1 product minimum and ADR-0023 assigns minimal recovery artifact generation to `Dobby.Admin`. Full Admin UI composition, plugin signing wizard, license generator wizard, batch operations and audit viewer remain P1-if-time/P2 according to the command inventory below.

## 1. Məqsəd

Admin Tool vendor/operator tərəfdə işləyən ayrıca WPF executable-dir. Müştəri maşınlarında host app kimi deploy olunmur və host runtime state-i ilə ortaq local data paylaşmır.

## 2. Authority

- `ADR-0023` Admin/Host boundary üçün canonical qərardır.
- Bu spec Admin Tool-un UI və CLI execution qaydalarını saxlayır.
- Host business logic-i Admin Tool daxilində kopyalanmır; shared behavior `Dobby.Application`, `Dobby.Contracts`, `Dobby.Security`, `Dobby.UI` və `Dobby.Localization` səviyyəsində reuse edilir.

## 3. UI və CLI Boundary

- Admin UI və Admin CLI eyni application service use case-lərindən istifadə edir.
- CLI argument parsing yalnız input parsing/validation layer-dir; parsed argument-lər typed request DTO-larına çevrilir və service boundary-ni həmin DTO-lar keçir.
- CLI branch-i ayrıca business rule, signing rule, customer rule və plugin rule implement etmir.
- UI flow orchestration user interaction üçündür; domain decision service qatındadır.
- Phase 1-də ayrıca `Dobby.Admin.Cli` project yaradılmır. `tools/<command>/` PowerShell wrapper-ləri published `Dobby.Admin.exe`-ni `--cli <command>` argument forması ilə invoke edir və wrapper contract-da göstərilən exit code/stdout/stderr davranışını qoruyur.

## CLI command inventory

Admin UI və Admin CLI eyni application service use case-lərindən istifadə edir. Inventory yalnız command group xülasəsi deyil, indicative command name, input və output artifact-ını göstərməlidir. Tam Admin UI signing/license screen-ləri P1-if-time/P2 qala bilər, amma P1 smoke/release evidence üçün aşağıdakı scripted operator command adları `P1 Minimal Scripted Operator Path` bölməsində canonical folder/script adları ilə bağlanır.

| Command | Phase | Input | Output | Responsibility |
|---|---|---|---|---|
| `create-recovery-response` | P1-core | `.dobbyrecoveryrequest` file və ya request payload | signed `.dobbyrecovery` | Owner recovery response-u user seçmədən yaradır. |
| `sign-plugin-package` | P1 scripted smoke path; full UI P1-if-time/P2 | unsigned/candidate `.dobbyplugin` build artifact | signed `.dobbyplugin` və verification report | P1 sample/plugin install smoke üçün minimal wrapper; full plugin signing wizard release blocker deyil. |
| `create-plugin-license` | P1 scripted smoke path; full UI P1-if-time/P2 | plugin/customer/deployment/package/device binding input | detached `.dobbylicense` və redacted summary report | P1 license import/enforcement smoke üçün minimal wrapper; full license generator wizard release blocker deyil. |
| `verify-plugin-package` | P1 release path; full UI P1-if-time/P2 | `.dobbyplugin` | redacted verification report | Package read, compatibility, content/hash and signature policy is checked without install or execution. |
| `create-cross-device-restore-package` (indicative, P2-deferred) | P2 | backup artifact və restore request materialı | cross-device restore package | Cross-device restore sub-spec açılana qədər release blocker deyil, amma inventory-də silent buraxılmır. |

P1 sample verification may use a minimal scripted/operator path for producing a signed sample `.dobbyplugin` and detached `.dobbylicense`. This path is not the full Admin UI; it exists only to support plugin install/license smoke verification before the full UI is pulled into scope.

`verify-plugin-package` is a required release-evidence gate in addition to the
install-time customer trust gate. It never installs, loads or executes plugin
code.

### P1 Minimal Scripted Operator Path

Phase 1 skeleton must provide a minimal scripted/operator artifact path even if
full Admin UI signing/license screens stay P1-if-time/P2. The path is owned by
`Dobby.Admin` application services and exposed through repository scripts under
`tools/` during skeleton.

Canonical scripted commands:

| Command | Owner | Input | Output | Scope |
|---|---|---|---|---|
| `sign-plugin-package` | `Dobby.Admin` + `tools/sign-plugin-package/` wrapper | unsigned candidate plugin publish folder or unsigned `.dobbyplugin`, manifest, dev/QA signing profile | signed `.dobbyplugin` and verification report | P1 sample/plugin install smoke |
| `create-plugin-license` | `Dobby.Admin` + `tools/create-plugin-license/` wrapper | plugin id, package fingerprint, customer/deployment context, device binding fixture, validity window, revision, dev/QA signing profile | detached `.dobbylicense` and redacted summary report | P1 license import/enforcement smoke |
| `verify-plugin-package` | `Dobby.Admin` + `tools/verify-plugin-package/` wrapper | signed `.dobbyplugin`, explicit host/trust profile | verification report | P1 release verification |
| `create-recovery-response` | `Dobby.Admin` + `tools/create-recovery-response/` wrapper | `.dobbyrecoveryrequest` file və ya request payload | signed `.dobbyrecovery` | P1 Owner recovery smoke/release evidence |

Minimum scripted wrapper contract:

- wrappers return exit code `0` on success and non-zero on validation, signing,
  verification or I/O failure, using the approved exit-code table below;
- stdout contains produced artifact/report paths and a redacted summary;
- stderr contains only typed, non-secret failure reason codes and never raw private
  key material, raw license payload, raw recovery payload, private key path, full
  canonical payload, full local path disclosure or stack trace;
- wrapper folder names, command names, invocation through
  `Dobby.Admin.exe --cli <command>`, exit code, stdout and stderr behavior are
  canonical for P1 smoke automation;
- exact option names and option sets are approved by `HD-S10-001` in
  `artifacts/release-evidence/skeleton-local/decision-log.md`.

### Approved P1 CLI contract

This table is the canonical public CLI contract for Sprint 10 scripted Admin
flows. Each command accepts only the options listed for that command; an unknown
option, an option from another command, a missing required value, or a repeated
single-value option exits `64`. `--overwrite` is a switch and may appear at most
once.

| Wrapper | Required options | Optional options | Defaults and validation |
|---|---|---|---|
| `sign-plugin-package` | `--input-path`, `--signing-profile`, `--output-path` | `--trust-profile`, `--report-path`, `--overwrite` | `--trust-profile` defaults from `--signing-profile`; `--report-path` defaults to `<output-path>.report.json`; if both profiles are supplied they must map to the same trust profile or exit `65`. |
| `create-plugin-license` | `--plugin-package-path`, `--signing-profile`, `--customer-id`, `--deployment-id`, `--device-binding-fixture-path`, `--valid-from`, `--valid-until`, `--revision`, `--output-path` | `--report-path`, `--intent`, `--overwrite` | `--report-path` defaults to `<output-path>.report.json`; `--valid-from` and `--valid-until` are ISO-8601 UTC values with `Z` suffix and `valid-until > valid-from`; `--revision` is a positive integer. `--intent` accepts only the signed lifecycle intent values `granted` and `revoked` and defaults to `granted`; any other value exits `65` with `invalid-intent`. Intent is signed payload content, not a report label: a revocation is expressed as a new signed revision with `intent = revoked`, never by editing or deleting an issued artifact (`docs/schemas/dobby-plugin-license-v3.schema.json`, `docs/schemas/license-v3-lifecycle-truth-table.md`, `ADR-0060`). The device binding input is **digest-only**: it must carry a `deviceFingerprint` lowercase SHA-256 value, and an input carrying raw `machineGuid` or `stableDeviceIdHex` material is rejected as `invalid-device-binding` (`ADR-0063` § 3). |
| `create-recovery-response` | `--request-path`, `--device-public-key-path`, `--signing-profile`, `--output-path` | `--report-path`, `--overwrite` | The provisioned P-256 public key must hash to the request binding and verify its possession proof. `--report-path` defaults to `<output-path>.report.json`; production signing material remains governed by `HD-S10-002` and `ADR-0062`. |
| `verify-plugin-package` | `--input-path`, `--trust-profile`, `--host-version` | `--public-key-path`, `--report-path`, `--overwrite` | `--report-path` defaults to `<input-path>.verification.report.json`; an explicit P-256 public key may be supplied, otherwise the selected build profile's embedded key is used. The command never installs or executes code. |

Approved profile values are `dev-qa` and `production`. Parsing is
case-insensitive for operator ergonomics, but reports and documentation must emit
canonical lowercase values. Internal trust-profile mapping is fixed:
`dev-qa -> DobbyPluginTrustProfile.DevQa` and
`production -> DobbyPluginTrustProfile.Production`. Unknown profile values,
unknown mappings, invalid profile combinations, invalid timestamps, invalid
revision values, validation failures and output/report path conflicts exit `65`.
Known profiles whose signing material is missing or unavailable exit `66`.
The default CLI signing-material provider reads purpose-separated private-key PEM
paths only from operator-supplied environment variables. The supported suffixes
are `PLUGIN_PACKAGE_KEY_PATH`, `PLUGIN_LICENSE_KEY_PATH`, and
`OWNER_RECOVERY_RESPONSE_KEY_PATH`, each prefixed by either
`DOBBY_ADMIN_SIGNING_DEV_QA_` or `DOBBY_ADMIN_SIGNING_PRODUCTION_`.
These values are never emitted to stdout/stderr or reports; absent, unreadable
or malformed material still exits `66`.

`--output-path` is required for artifact-producing commands. The verifier's
default report is written beside its input only when `--report-path` is omitted.
`--overwrite` defaults to `false`; if output or report targets already exist and
`--overwrite` is not supplied, the command exits `65`.

| Exit code | Meaning |
|---:|---|
| `0` | Success. |
| `64` | Usage error, invalid command shape, unknown/foreign/repeated option, or missing required argument. |
| `65` | Input validation failure, profile mismatch, invalid timestamp/revision, or output/report conflict. |
| `66` | Required signing material is known but unavailable. |
| `70` | Internal failure after redaction. |

The scripted path does not create customer production artifacts by default. It
uses dev/QA signing material, `vendorId = "dobby.dev-qa"` for sample packages and
deterministic smoke fixtures so `AC-004`,
`AC-018`, `AC-034` and `AC-036` can be evidenced before full Admin UI is pulled
into scope. Production vendor signing keys remain governed by `ADR-0062` and are
not used for dev/QA sample artifacts.

## 4. Recovery və Signing Artifact Generation

- Phase 1 məcburi minimum Owner recovery response generation-dur.
- Admin Tool `.dobbyrecoveryrequest` oxuyur, vendor-side signing material ilə `.dobbyrecovery` response yaradır və operatora ayrıca managed user seçdirmir.
- Vendor private signing key host app-a və plugin-lərə verilmir.
- Request/response artifact raw secret deyil, amma response trust yalnız signature verification ilə yaranır.
- Admin tooling signing key-i **explicit artifact purpose** üzrə versiyalanmış key ring-dən seçir. Plugin package, plugin license, recovery response və BranchSetup authorization leaf purpose-ları bir-birini əvəz edə bilməz; bir purpose-un key-i digərinin artifact-ını imzalamaq üçün istifadə olunarsa command fail-closed olur (`ADR-0062`, `ADR-0058`, `ADR-0059`, `docs/02_ARCHITECTURE_MAP.md` § `Key Purpose Separation`).
- Bütün imzalanan artifact-lar canonical **versioned** payload emit edir: schema/wire version, purpose və key ID payload-ın imzalanan hissəsindədir. Signature encoding explicit-dir — ECDSA P-256/SHA-256, `IeeeP1363FixedFieldConcatenation` fixed-field format və unpadded base64url; overload default-una güvənilmir.
- Artifact-producing command-lər **fail-closed ordering** tələb edir: option shape (`64`), profile/purpose/timestamp/revision/intent validation (`65`) və output/report conflict yoxlaması (`65`) signing material resolve-dan **əvvəl** icra olunur. Usage və ya validation failure heç bir private key material-ına toxunmur, signing purpose seçimini işə salmır və output/report target-inə tək bayt da yazmır; artifact yalnız bütün gate-lər keçdikdən sonra yaranır. Signing material tanınan profile üçün əlçatan deyilsə nəticə `66`-dır və yenə heç bir artifact yaranmır.
- P1 approved CLI contract-ında artifact-producing command-lər üçün ayrıca `--dry-run` switch-i **yoxdur**: `Approved P1 CLI contract` cədvəlində sadalanmayan option unknown option kimi `64` verir. P1-in verification-only path-ı `verify-plugin-package`-dir — o heç bir kod install/execute etmir, private signing material istifadə etmir və yalnız verification report yazır. Gələcəkdə artifact-producing command-ə dry-run əlavə olunarsa, o əvvəlcə həmin cədvəldə option, default, report semantics və exit code-ları ilə approve edilməlidir; undocumented option kimi əlavə edilməsi qadağandır.
- Private key material heç bir halda stdout, stderr, report və ya diagnostics-ə çıxmır; key path da disclose edilmir.
- Recovery response original request-in `requestId`, nonce, device/deployment identity və `recoveryPublicKeyHash` dəyərlərinə bind olunur; bu bind-lərin biri uyğun gəlmirsə response yaradılmır (`ADR-0058`).
- BranchSetup authorization ayrıca artifact və ayrıca command-dir; license generator-un option-u kimi modelləşdirilmir (`ADR-0057`).
- Plugin license device binding input-u **digest-only**-dir: vendor tərəf yalnız hazır `deviceFingerprint` lowercase SHA-256 dəyərini qəbul edir. Raw `machineGuid` və `stableDeviceIdHex` field-ləri daşıyan input `invalid-device-binding` ilə fail-closed rədd olunur və artifact yaradılmır, çünki raw protected device material cihazdan çıxmır (`ADR-0063` § 3, `ADR-0058` § `Support identity`). Vendor tərəf digest-i özü hesablamır.

### Recovery wire compatibility

- Recovery request/response wire compatibility strict **v2**-dir. Admin command v1 recovery artifact-ını silent qəbul etmir; v1 input typed usage/validation failure ilə rədd olunur.
- Request yalnız `recoveryPublicKeyHash` export edir. `--device-public-key-path` ayrıca provision olunmuş public key-i verir və request possession proof-un validasiyası üçün istifadə olunur.
- Missing key, hash mismatch, failed possession proof, expired request, və ya selected recovery-response key ilə uyğun gəlməyən request vendor key ID — hər biri fail-closed nəticədir: exit code `65` və heç bir response artifact yaradılmır.

## 5. Runtime Data Boundary

- Admin Tool host `%LocalAppData%/Dobby` runtime data-sını birbaşa açmır.
- Admin Tool-un öz config/log/runtime data-sı vendor/operator environment-də ayrıca saxlanır.
- Müştəri data-sı yalnız explicit support/import/export artifact kimi daxil olur və raw secret material log-lanmır.

## 6. Audit və Diagnostics

- Admin Tool artifact generation və signing əməliyyatları operator-side diagnostics-də correlation id ilə görünür.
- Host audit trail-i yalnız host-da import/verification baş verəndə yaranır; vendor-side admin əməliyyatı host audit log-u kimi yazılmır.

## 7. Verification Obligations

Bu bölmə `NFR-027` behavior-first qaydasına tabedir: source-text/regex scan, fayl
mövcudluğu və ya "test list green" iddiası aşağıdakı sətirləri qapatmır.

| Obligation | Required proof |
|---|---|
| Approved option contract | `Approved P1 CLI contract` cədvəlindəki hər command üçün unknown option, foreign option, repeated single-value option və missing required value real CLI invocation ilə `64` verir; cədvəldə sadalanan hər optional option isə `64` **vermir**. |
| Doc/code option parity | Cədvəldəki command və option dəsti CLI-ın declared option dəsti ilə **eynidir**. Code-da qəbul olunan, lakin cədvəldə olmayan option undocumented public surface sayılır; cədvəldə olan, lakin code-da olmayan option operator üçün `64` deməkdir. Hər iki istiqamət yoxlanılır. |
| Runbook executability | `docs/09_BUILD_RUN_AND_RELEASE.md` § `Minimal scripted operator artifact handoff` blokundaki option adları bu cədvəldəki canonical adlardır; wrapper arqumentləri dəyişmədən ötürdüyü üçün PowerShell-style ad orada release-blocking `64` yaradır. |
| Fail-closed ordering | Usage/validation failure hallarında signing-material provider heç bir dəfə çağırılmır və output/report target-i yaradılmır — recording provider və filesystem observation ilə sübut olunur. |
| Signing material unavailability | Tanınan profile üçün material yoxdursa nəticə `66`-dır və artifact/report yazılmır. |
| Key purpose separation | Bir purpose-un key-i digər artifact-ı imzalamaq üçün istifadə edilə bilmir; recovery command plugin/license material-ına fallback etmir (`ADR-0062`). |
| Signed intent | `--intent revoked` yeni signed revision emit edir və intent imzalanan payload-dadır; default `granted`-dir, digər dəyər `65` verir. |
| Digest-only device binding | Raw `machineGuid`/`stableDeviceIdHex` daşıyan binding input `invalid-device-binding` ilə rədd olunur və artifact yaranmır (`ADR-0063` § 3). |
| Strict v2 recovery wire | v1 recovery input, hash mismatch, failed possession proof və key-ID mismatch hallarının hər biri `65` verir və response yaranmır. |
| Redaction | stdout/stderr/report-da private key material, key path, raw payload, full local path və stack trace görünmür (`NFR-026`). |

## 8. References

- `docs/adr/ADR-0023-admin-host-boundary.md`
- `docs/adr/ADR-0063-device-license-binding-derivation.md` — device binding
  input digest-only qaydası
- `docs/02_ARCHITECTURE_MAP.md` Row 84, Row 85, Row 86
- `docs/02_ARCHITECTURE_MAP.md` Future Scope row `Full Admin Tool UI`

## Branch Shared Setup Boundary Addendum

`Dobby.Admin` remains vendor-side signing/recovery/license tooling. It does not
become the customer-side Branch Shared setup executable.

Branch Shared setup is owned by `Dobby.BranchSetup.exe` in `ADR-0057` and
`docs/specs/onboarding-and-provisioning.md`. Admin may generate or sign support
artifacts that BranchSetup later applies, but Admin does not connect to customer
PostgreSQL as the branch setup operator.

Setup authorization artifact-ı ayrıca command və ayrıca leaf signing purpose
tələb edir; onu `create-plugin-license` option-u kimi əlavə etmək qadağandır.

## Revision History

- **2026-08-27:** Dry-run ziddiyyəti həll olundu. §4 hər artifact-producing command
  üçün "verification-only dry run" iddia edirdi, halbuki canonical `Approved P1 CLI
  contract` cədvəli sadalanmayan option-u `64` ilə rədd edir və `src`/`tools`/`tests`
  daxilində heç bir dry-run implementation-ı yoxdur; heç bir Accepted ADR belə switch
  tələb etmir. Stale iddia saxlanılmayıb — onun yerinə həqiqi və testable invariant
  yazılıb: validation gate-ləri signing material resolve-dan əvvəl icra olunur,
  usage/validation failure private key material-ına toxunmur və output/report yazmır;
  P1-in verification-only path-ı `verify-plugin-package`-dir; gələcək dry-run əvvəlcə
  contract cədvəlində approve olunmalıdır. Eyni passda `create-plugin-license`-in
  implementasiyada mövcud olan, lakin canonical cədvəldə itmiş `--intent` option-u
  (default `granted`, `granted|revoked`, əks halda `65`) contract-a əlavə olundu və
  signed-intent semantics `license-v3` schema/truth-table ilə bağlandı. Yeni §7
  `Verification Obligations` (`NFR-027`) bölməsi, `Phase` və `Sources` metadata
  sətirləri əlavə edildi; `References` §8-ə keçdi. Eyni reconciliation
  `docs/09_BUILD_RUN_AND_RELEASE.md` § `Minimal scripted operator artifact
  handoff` blokunu da düzəltdi: orada göstərilən `-PluginPackage` tipli
  PowerShell-style adlar wrapper tərəfindən CLI-a olduğu kimi ötürülüb `64`
  verirdi, yəni sənədləşdirilmiş release operator command-i icra olunmurdu; blok
  canonical `--option` adlarına keçirildi və "option table hələ freeze olunmayıb"
  iddiası ləğv edildi.

- **2026-08-26:** `ADR-0063` reconciliation. `create-plugin-license` device
  binding input-u digest-only kimi normativləşdirildi: raw `machineGuid` və
  `stableDeviceIdHex` daşıyan input `invalid-device-binding` ilə rədd olunur və
  vendor tərəf digest-i hesablamır.

- **2026-08-26:** `ADR-0058`…`ADR-0061` reconciliation. `2026-08 Signing and
  Recovery Amendment` bölməsi ləğv edildi; purpose-separated versioned key ring,
  canonical versioned payload/explicit signature encoding, verification-only dry
  run (bu bənd 2026-08-27-də ləğv edildi — yuxarıya bax), private-material
  redaction, recovery bind siyahısı və strict v2 wire
  compatibility qaydaları §4 və yeni `Recovery wire compatibility` bölməsinə,
  setup-authorization ayrılığı isə həm §4-ə, həm `Branch Shared Setup Boundary
  Addendum`-a köçürüldü.
