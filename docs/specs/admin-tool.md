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
  verification or I/O failure;
- stdout contains the produced artifact path and a redacted summary;
- stderr contains only typed, non-secret failure reason codes and never raw private
  key material, raw license payload, full local path disclosure or stack trace;
- wrapper folder names, command names, invocation through
  `Dobby.Admin.exe --cli <command>`, exit code, stdout and stderr behavior are
  canonical for P1 smoke automation;
- exact option names and option sets are blocked pending human/tooling decision
  unless an approved option table is supplied.

Future approved option tables must use this shape:

| Wrapper | Option | Argument type | Required? | Multiplicity | Default | Owner |
|---|---|---|---|---|---|---|
| `sign-plugin-package` | blocked pending human/tooling decision | - | - | - | - | - |
| `create-plugin-license` | blocked pending human/tooling decision | - | - | - | - | - |
| `create-recovery-response` | blocked pending human/tooling decision | - | - | - | - | - |
| `verify-plugin-package` (optional) | blocked pending human/tooling decision | - | - | - | - | - |

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
