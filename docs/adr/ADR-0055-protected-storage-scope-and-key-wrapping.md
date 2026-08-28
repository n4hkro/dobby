# ADR-0055: Protected Storage Scope and Key Wrapping

**Status:** Accepted (remembered-login material amended by ADR-0058; ADR-0039 recovery-secret references are historical — recovery material now follows the ADR-0058 device recovery key model; ADR-0051 license fingerprint references continue under ADR-0060 with unchanged binding semantics)
**Date:** 2026-04-28

## Context

Dobby Phase 1-də local-first Windows desktop host-dur. Host və plugin
persistence faylları data-at-rest qorunur (`ADR-0030`, `ADR-0054`), amma
secrets əlavə field-level/protected-storage qoruması almalıdır.

Mövcud sənədlər `System.Security.Cryptography.ProtectedData` / DPAPI-ni
primitive kimi seçir, lakin `DataProtectionScope.CurrentUser` və
`DataProtectionScope.LocalMachine` seçimini secret tipinə görə bağlamır. Bu
seçim gizli implementation detail kimi qalsa, multi-Windows-user privacy,
Owner recovery, remember-me, session invalidation və DB key wrapping davranışı
fərqli implementasiyalarda drift edə bilər.

Microsoft DPAPI semantikasına görə `CurrentUser` yalnız cari Windows user
context-i ilə unprotect oluna bilər. `LocalMachine` isə machine context-ə
bağlıdır və eyni maşında işləyən başqa account/process-lər üçün daha geniş
surface yaradır; buna görə `LocalMachine` təkbaşına security boundary kimi
qəbul edilmir.

Bu ADR Dobby-nin protected-storage scope mapping və key wrapping invariantlarını
bağlayır. Dobby local user modeli Windows user account modeli ilə eyni deyil;
ona görə qərar yalnız DPAPI enum seçimi yox, app-local user, recovery və
storage taxonomy boundary-lərinin birgə qərarıdır.

## Decision

Phase 1 protected-storage scope mapping secret/material tipinə görə seçilir.
Blanket `LocalMachine` və blanket `CurrentUser` qaydası yoxdur.

| Secret / material | P1 protected-store scope | Reason |
|---|---|---|
| DB-at-rest provider key / DEK wrap | Machine-bound protected store; P1 default DPAPI `LocalMachine` + Dobby application-level entropy | DB pre-login açılışda yoxlana bilməlidir; user-dən ayrıca DB password istənilmir; key təkcə `deviceId`-dən törəmir |
| Password hash / auth credential protected material | Machine-bound protected store; P1 default DPAPI `LocalMachine` + Dobby application-level entropy | Dobby app-local user modeli Windows user modeli deyil; login pre-auth işləməlidir; Windows profile silinməsi app-local user identity-ni itirməməlidir |
| Owner recovery secret / consumed recovery nonce material | Machine-bound protected store; P1 default DPAPI `LocalMachine` + Dobby application-level entropy | Owner lockout-dan sonra recovery import işləməlidir; recovery nonce state host-owned və device-bound qalır (`ADR-0039`) |
| Remember-me saved credential | DPAPI `CurrentUser` + Dobby user/slot entropy | Remember-me auto-login deyil, Windows profile-bound login rahatlığıdır; başqa Windows user account-a keçməməlidir |
| Session material | DPAPI `CurrentUser` + Dobby session entropy | Session cari Windows profile/app execution context-i ilə bağlıdır; profile migration/deletion session-u təbii invalidate edir |
| Plugin `UserScopedStorage` secret fields | DPAPI `CurrentUser` + Dobby user/compartment entropy | User-scoped plugin secret-ləri cross-Windows-user sızmamalıdır; plugin yenə key material görmür |
| Shared domain/customer/device secret fields | Machine-bound protected store; P1 default DPAPI `LocalMachine` + Dobby customer/deployment/compartment entropy | Shared domain data customer/device scope-dadır və fərdi Windows profile-a bağlanmamalıdır; visibility permission registry ilə gate edilir |
| Protected config secrets | Scope owner-luğa görə seçilir: host/device-wide config üçün machine-bound, per-Windows-user preference secret üçün `CurrentUser` | Config secret-in lifecycle-i hansı storage bucket-ə aid olduğuna görə müəyyən edilir |

### Defense-in-depth invariant

- Machine-bound protected store-da saxlanılan material üçün DPAPI `LocalMachine`
  yalnız bir layer-dir; security boundary DPAPI `LocalMachine` + Dobby
  application-level entropy + host ACL/storage boundary kombinasiyasıdır.
- Dobby application-level entropy source host-installation-bound 256-bit random
  secret-dir. It is generated at first-run bootstrap, protected at rest with the
  machine-bound protected store, stored only as a protected blob under host-owned
  `%LocalAppData%/Dobby/data/` storage, never stored as plaintext, never logged and
  never exposed to plugins.
- Bu invariant olmadan `LocalMachine` shared Windows machine-də
  same-machine-different-Windows-user attack surface-ni kifayət qədər daraltmır.
- `deviceId` və ya hardware fingerprint key materialı kimi istifadə olunmur;
  onlar identity/support metadata olaraq qalır.

### Stable device identity relation

Phase 1 `Dobby-generated stable device id` (`ADR-0051` license fingerprint v1) is distinct from Dobby application-level entropy.

- `DobbyStableDeviceId` is a host-installation-bound random identity material generated during first-run bootstrap by `Dobby.Infrastructure / Hardware Identity`.
- It is stored as machine-bound protected material under host-owned `%LocalAppData%/Dobby/data/` storage and is never stored as plaintext config.
- It is consumed only for `ADR-0051` license device fingerprint calculation and support-safe redacted device identity display. It is not a DB encryption key, not DEK wrapping entropy and not a replacement for Dobby application-level entropy.
- Raw `DobbyStableDeviceId`, raw `MachineGuid` and full license device fingerprint are never logged, never exposed to plugins, never written to diagnostics export and never embedded in `.dobbyplugin` packages.
- If protected stable device identity is missing or cannot be decrypted after license/device-bound state exists, host must fail closed for license binding and show support-safe diagnostics. It must not silently reissue a new stable device id that would orphan or bypass existing license binding.

### Key material visibility

- Plugin provider, key material, DPAPI scope, entropy source, backing file path
  və raw protected blob detail-larını görmür.
- Plugin yalnız rəsmi storage/capability contract-ları ilə işləyir və host
  protected-storage wrapper-ından kənara çıxa bilmir.
- Protected-storage wrappers audit/diagnostics export zamanı secret materialı raw
  formada çıxarmamalıdır.

## Considered Alternatives

| Alternative | Decision | Reason |
|---|---|---|
| Blanket `DataProtectionScope.LocalMachine` | Rejected | Pre-login və recovery üçün əlverişlidir, amma remember-me, session və user-scoped plugin secrets üçün same-machine cross-user exposure riskini artırır |
| Blanket `DataProtectionScope.CurrentUser` | Rejected | Per-Windows-user privacy yaxşıdır, amma DB-at-rest key wrapping, Owner recovery və shared customer/device scope data üçün pre-login və cross-profile lifecycle problemləri yaradır |
| Key materialı `deviceId` / hardware fingerprint-dən törətmək | Rejected | `ADR-0030` və storage spec-in key-source boundary-si ilə ziddir; `deviceId` yalnız identity/support metadata-dır |
| Plugin-managed secret wrapping | Rejected | Plugin raw key/provider/backing detail görər və host-controlled storage/security boundary pozular |
| ASP.NET/.NET Data Protection stack-i P1-də əlavə etmək | Deferred | Dobby desktop/offline P1 üçün DPAPI + Dobby-owned wrapper kifayətdir; əlavə key-ring lifecycle və package surface P1-i böyüdür |

## Failure Modes and Recovery Notes

- OS reinstall machine-bound materialı invalidate edir. Bu hal self-service folder
  copy ilə dəstəklənmir; cross-device/new-device restore ayrıca vendor-assisted
  migration/recovery flow tələb edir.
- Windows profile deletion/migration `CurrentUser`-bound materialı invalidate edə
  bilər. Remember-me və session material itir, amma app-local Dobby user identity
  machine-bound host persistence saxlanılırsa itmir.
- Domain join/leave və Windows profile SID dəyişiklikləri `CurrentUser`-bound
  material üçün re-auth/remember-me reset tələb edə bilər.
- `CurrentUser` material decrypt edilə bilmədikdə remember-me/session rahatlığı
  fail-closed invalidate olunur; user normal password login flow-a qaytarılır.
- Machine-bound material decrypt edilə bilmədikdə host bunu local data protection
  failure kimi göstərir və raw cryptographic detail UI-da göstərilmir; diagnostics
  secret daşımadan local support package ilə çıxarıla bilər.

## Out of Scope

Aşağıdakı material DPAPI scope qərarından kənardır:

- Plugin signature verification public key (`ADR-0032`) — host binary-də embedded
  public key-dir və DPAPI-da saxlanmır.
- Vendor signing private key ownership (`ADR-0033`) — vendor/admin boundary-dir,
  host local protected storage məsuliyyəti deyil.
- Owner recovery response signature public verification materialı (`ADR-0039`) —
  plugin signature trust primitive-ləri ilə paylaşılır və DPAPI scope-u deyil.
- Plugin manifest schema faylları — public validation asset-ləridir.
- Logging, diagnostics və audit content — `ADR-0019`, `NFR-020` və
  audit/diagnostics spec boundary-ləri ilə idarə olunur; secrets raw export
  olunmur.

## Consequences

- `ADR-0030`, `ADR-0038`, `ADR-0039` və `ADR-0054` secret/protected-storage
  scope detail-ları üçün bu ADR-ə istinad edir.
- Skeleton/security implementation round `IProtectedStorage` və ya ekvivalent
  wrapper surface-də secret purpose/scope seçimini explicit model-ləşdirməlidir;
  call-site-lar raw DPAPI enum seçməməlidir.
- Release verification secret materialın plaintext saxlanmadığını yoxlamaqla
  yanaşı, remember-me/session materialın `CurrentUser`-bound davranışını və
  machine-bound materialın plugin-ə görünmədiyini yoxlamalıdır.
- Machine-bound və CurrentUser-bound material üçün failure mode-lar user-facing
  error handling və diagnostics boundary-lərində ayrıca yoxlanmalıdır.

## Cross-references

- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md` — storage taxonomy və key-source boundary
- `docs/adr/ADR-0038-local-first-authentication.md` — local auth, password hash, remember-me və session material
- `docs/adr/ADR-0039-owner-password-recovery.md` — Owner recovery secret və consumed nonce material (superseded by `ADR-0058` device recovery key model)
- `docs/adr/ADR-0054-db-at-rest-protection-provider.md` — DB-at-rest provider key / DEK wrapping
- `docs/specs/storage-and-data-protection.md` — storage/data protection executable spec
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-007`, `NFR-020`
- `docs/02_ARCHITECTURE_MAP.md` — Row 40 `Secrets / Secure Storage`
- Microsoft Learn DPAPI `DataProtectionScope` enum — `CurrentUser` və `LocalMachine` semantics: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.dataprotectionscope

## Revision History

- **2026-04-28:** ADR yaradıldı; per-secret protected-storage scope mapping,
  machine-bound defense-in-depth invariantı, out-of-scope material və DPAPI
  failure mode-ları Phase 1 üçün bağlandı.

## Branch Shared Credential Amendment

Branch Shared runtime credential `dobby_app` and setup credential `dobby_setup`
are protected-storage material. Machine-bound material uses DPAPI
`LocalMachine` only with Dobby application-level entropy and restrictive
host/setup ACL/storage boundaries.

`Dobby.Host` may load only runtime material needed for Branch Shared operation.
It never loads `dobby_setup`. `dobby_setup` is visible only to setup-authorized
BranchSetup context. `HD-003` is resolved by `DM-090`: normal seller runtime
profiles persist only `dobby_app`; `dobby_setup` remains BranchSetup-only and
must not be persisted as seller-device runtime material unless the `DM-090`
reopen condition is met.
