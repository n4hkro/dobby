# Admin Tool

**Tarix:** 2026-04-29
**Status:** Split spec — P1-core minimal owner recovery response generator; P1-if-time/P2 full Admin UI
**Scope:** Vendor-side Admin Tool UI/CLI boundary, shared application-service reuse, recovery/signing artifact generation, and no business-logic duplication with the host.

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
| `verify-plugin-package` | Optional P1 debug/smoke support; full UI P1-if-time/P2 | `.dobbyplugin` | verification result | Package trust, manifest və signature smoke/debug flow-u; install path yenə ayrıca verification edir. |
| `create-cross-device-restore-package` (indicative, P2-deferred) | P2 | backup artifact və restore request materialı | cross-device restore package | Cross-device restore sub-spec açılana qədər release blocker deyil, amma inventory-də silent buraxılmır. |

P1 sample verification may use a minimal scripted/operator path for producing a signed sample `.dobbyplugin` and detached `.dobbylicense`. This path is not the full Admin UI; it exists only to support plugin install/license smoke verification before the full UI is pulled into scope.

`verify-plugin-package` remains optional P1 debug/smoke support. Install-time
package verification is the required customer trust gate; the standalone wrapper
becomes required only if release automation explicitly requires standalone
verification evidence.

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
| `verify-plugin-package` | `Dobby.Admin` + optional `tools/verify-plugin-package/` wrapper | signed `.dobbyplugin` | verification report | P1 debug/smoke support |
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
| `create-plugin-license` | `--plugin-package-path`, `--signing-profile`, `--customer-id`, `--deployment-id`, `--device-binding-fixture-path`, `--valid-from`, `--valid-until`, `--revision`, `--output-path` | `--report-path`, `--overwrite` | `--report-path` defaults to `<output-path>.report.json`; `--valid-from` and `--valid-until` are ISO-8601 UTC values with `Z` suffix and `valid-until > valid-from`; `--revision` is a positive integer. |
| `create-recovery-response` | `--request-path`, `--device-public-key-path`, `--signing-profile`, `--output-path` | `--report-path`, `--overwrite` | The provisioned P-256 public key must hash to the request binding and verify its possession proof. `--report-path` defaults to `<output-path>.report.json`; production signing material remains governed by `HD-S10-002` and `ADR-0033`. |
| `verify-plugin-package` (optional) | `--plugin-package-path`, `--report-path` | `--trust-profile`, `--overwrite` | Optional command only; not a Sprint 10 release gate. `--trust-profile` defaults to `production`. |

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

`--output-path` is required for artifact-producing commands. `--report-path` is
required for `verify-plugin-package` because verification produces only a report
and must not write beside the input package implicitly. `--overwrite` defaults
to `false`; if output or report targets already exist and `--overwrite` is not
supplied, the command exits `65`.

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
into scope. Production vendor signing keys remain governed by `ADR-0033` and are
not used for dev/QA sample artifacts.

## 4. Recovery və Signing Artifact Generation

- Phase 1 məcburi minimum Owner recovery response generation-dur.
- Admin Tool `.dobbyrecoveryrequest` oxuyur, vendor-side signing material ilə `.dobbyrecovery` response yaradır və operatora ayrıca managed user seçdirmir.
- Vendor private signing key host app-a və plugin-lərə verilmir.
- Request/response artifact raw secret deyil, amma response trust yalnız signature verification ilə yaranır.

## 5. Runtime Data Boundary

- Admin Tool host `%LocalAppData%/Dobby` runtime data-sını birbaşa açmır.
- Admin Tool-un öz config/log/runtime data-sı vendor/operator environment-də ayrıca saxlanır.
- Müştəri data-sı yalnız explicit support/import/export artifact kimi daxil olur və raw secret material log-lanmır.

## 6. Audit və Diagnostics

- Admin Tool artifact generation və signing əməliyyatları operator-side diagnostics-də correlation id ilə görünür.
- Host audit trail-i yalnız host-da import/verification baş verəndə yaranır; vendor-side admin əməliyyatı host audit log-u kimi yazılmır.

## 7. References

- `docs/adr/ADR-0023-admin-host-boundary.md`
- `docs/02_ARCHITECTURE_MAP.md` Row 84, Row 85, Row 86
- `docs/02_ARCHITECTURE_MAP.md` Future Scope row `Full Admin Tool UI`

## Branch Shared Setup Boundary Addendum

`Dobby.Admin` remains vendor-side signing/recovery/license tooling. It does not
become the customer-side Branch Shared setup executable.

Branch Shared setup is owned by `Dobby.BranchSetup.exe` in `ADR-0057` and
`docs/specs/onboarding-and-provisioning.md`. Admin may generate or sign support
artifacts that BranchSetup later applies, but Admin does not connect to customer
PostgreSQL as the branch setup operator.

## 2026-08 Signing and Recovery Amendment (Controlling)

Admin tooling selects a versioned key ring by explicit artifact purpose. Plugin,
license, recovery-response and setup-authorization leaf purposes are not
interchangeable. It emits canonical versioned payloads and explicit
ECDSA/P1363/base64url signatures, supports verification-only dry runs, and never
prints private material. Recovery responses bind the original request ID, nonce,
device/deployment identity and recovery public-key hash. Setup authorization is
a distinct artifact and command, not a license-generator option.

Recovery request and response wire compatibility is strict v2. The Admin command
does not silently accept v1 recovery artifacts. The request exports only the
recovery public-key hash; `--device-public-key-path` supplies the separately
provisioned public key used to validate request possession. A missing key, a hash
mismatch, a failed possession proof, an expired request, or a request vendor key
ID that does not match the selected recovery-response key fails closed with exit
code `65` and produces no response artifact.
