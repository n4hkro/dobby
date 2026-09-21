# ADR-0019: Audit vs Diagnostics Boundary

**Status:** Accepted (atomicity and diagnostics-availability details amended by `ADR-0061` — see ## Revision History)
**Date:** 2026-04-22

## Context

Dobby-də audit trail və technical diagnostics eyni data kimi davranmamalıdır.
Audit compliance, accountability və kim-nə-vaxt-nə-etdi tarixçəsini saxlayır.
Diagnostics isə troubleshooting, support və runtime müşahidə məqsədi daşıyır.

Arxitektura xəritəsi bu sərhədi artıq bağlı qərar kimi qəbul edir, amma ADR
faylı ilə formal qeyd yox idi.

## Decision

Audit və diagnostics ayrı məsuliyyət daşıyır:

- audit: business/compliance yönümlü, rəsmi tarixçə
- diagnostics: technical/operational yönümlü, troubleshooting və health

Texniki log mövcud olması audit hadisəsi yaratmır. Eyni şəkildə audit qeydinin
olması diagnostics log formatı və retention qaydasını müəyyən etmir.

Audit trail texniki log sink-inin alias-ı deyil; öz semantic purpose-i olan
ayrı boundary-dir.

### P1 audit integrity scope

Phase 1 audit storage is append-only at the public API/service boundary: normal
application code must not expose audit update/delete operations. Cryptographic
tamper-evidence such as hash chain, Merkle proof or external notarization is not
part of P1 release evidence and must not be claimed by P1 docs, tests or UI.
Tamper-evident audit integrity can be introduced only by a future ADR or explicit
ADR-0019 amendment.

### Audit event taxonomy

Exact audit event names are owned by `docs/specs/audit-and-diagnostics.md`.
This ADR owns the boundary rule: audit records answer "who did what, when, and
under which authorization/security context"; diagnostics records answer "what
technical behavior happened while the system was running".

Audit viewer UI P1 blocker deyil. Audit data P1-də yazılır; viewer və advanced
filter/export UI ayrıca P2/P3 işi kimi qala bilər. Audit log-a baxış ayrıca
permission/capability ilə idarə edilməlidir.

### Minimum audit entry payload

Hər audit entry aşağıdakı minimum field set-ini daşımalıdır:

- `timestamp` — ISO 8601 UTC (həmişə UTC, local zone transformation UI-da edilir)
- `actor` — user ID (əgər system-generated hadisədirsə `system`)
- `action` — typed action identifier; exact audit event names are owned by
  `docs/specs/audit-and-diagnostics.md`
- `target` — entity ID (əgər tətbiq olunursa: plugin ID, user ID, license ID və s.)
- `result` — status: `success` / `failed` / `denied`
- `correlationId` — `ADR-0029 Correlation ID Ownership` ilə uyğun

Bu set FR-011-də sadalanan əməliyyatların hamısı üçün məcburidir. Əlavə
action-specific kontekst (məs., license `grace` səbəbi, plugin signature
failure təfərrüatı) ayrı `details` field-i daşıya bilər; minimum set-ə isə
təsir etmir.

### Diagnostics export və privacy

Dobby default olaraq vendor-a usage analytics, crash report, telemetry və ya
diagnostics upload etmir. Diagnostics local technical troubleshooting datasıdır.

Vendor/support-a göndərilə bilən diagnostics yalnız explicit user/Owner action ilə
local export package kimi yaradılır.

Host support diagnostics export recovery/support funksiyasıdır və heç bir plugin license
state-i (expired, mismatched, missing, grace) tərəfindən bloklanmır (`ADR-0061`). License
gate feature surface-lərə aiddir; diagnostics kanalını həmin gate-in arxasına qoymaq onu
ən çox lazım olduğu anda bağlayardı. Export package minimum redaction qaydalarına tabedir:

- raw password, token, private key, session material və remember-me secret çıxarılmır;
- raw license artifact/detail çıxarılmır; yalnız non-sensitive status/fingerprint
  summary verilə bilər;
- raw plugin exception detail user-facing UI-da göstərilmir, diagnostics-də isə
  support məqsədli saxlanıla bilər;
- export action özü audit trail-ə yazılır.

Localization missing-key hadisələri diagnostics event/log-dur, audit hadisəsi deyil.

## Consequences

- `Dobby.Audit` və `Dobby.Diagnostics` fərqli ownership və test məqsədi saxlayır
- Command pipeline, licensing və security axınları audit yaradır və bu, structured log-ı
  əvəz etmir. Audit yazısı mutation-ın optional yan effekti deyil: business state, asılı
  cache/index qeydləri, session invalidation/version və audit bir transaction daxilində,
  ya da durable outbox/repair protokolu ilə commit olunur; partial success qadağandır
  (`ADR-0061`). Ayrı connection üzərində yazılan və business transaction rollback olduqda
  itən audit bu qaydayı ödəmir
- Sonrakı implementation mərhələsində append-only audit storage və diagnostics
  sink-ləri bir-birinə qarışdırılmamalıdır
- Privacy default-local qalır; avtomatik telemetry/upload davranışı ayrıca explicit
  product/security qərarı olmadan əlavə edilmir
- Diagnostics export support flow-dur, telemetry deyil

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `Audit Trail` row
- `docs/02_ARCHITECTURE_MAP.md` — `ADR-0019` risk sətri
- `docs/03_TECH_STACK_AND_VERSIONS.md` — `Tam Phase 1 dependency siyahısı`, Storage/Audit package rows
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-011`, `FR-027`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-011`, `NFR-020`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-010`, `AC-033`
- `docs/specs/audit-and-diagnostics.md`
- `docs/adr/ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md` — audit
  mutation atomicity və diagnostics export availability detallarını amend edir;
  həmin bölmələr üçün current authority

## Branch Shared Audit Amendment

Standalone audit may remain local. In Branch Shared mode authoritative audit is
BranchShared data in PostgreSQL; technical diagnostics remain DeviceLocal and
support-safe.

The only canonical Branch Shared pre-auth audit events are
`user.login.failed`, `recovery.export`, `recovery.import` and
`security.verification.failed`. The whitelist is enforced by the audit writer /
application-layer path, not by PostgreSQL grants alone.

If PostgreSQL is unreachable before the canonical pre-auth audit INSERT can
occur, the attempt is DeviceLocal diagnostics/setup evidence only and is not
authoritative Branch Shared audit. Pending local audit buffering remains a
deferred fallback only if security review rejects the direct pre-auth INSERT
path.

BranchSetup setup-proof events use a `source=BranchSetup` family. Pre-schema
setup reports are authoritative setup evidence and are not re-injected into
PostgreSQL later.

Branch Shared audit origin is resolved by `DM-091`:

- `source` is a closed vocabulary: `Host`, `BranchSetup`, `Support`;
- `originDeviceId` is the Branch Shared server-issued `deviceId`;
- before registration, `originDeviceId = null` and a support-safe
  `localDeviceRef` snapshot may be recorded;
- `localDeviceRef` in audit payload is an event-time snapshot, not a live link
  to the current `branchshared.json` connection-profile value;
- adding another `source` value requires an ADR/spec amendment.

Branch Shared P1 app audit remains write-only from the application perspective
per `DM-089`. Audit viewer/export read grants are deferred until explicitly
scoped.

## Revision History

- **2026-08-31:** `ADR-0061` amendment-i dated record kimi yazıldı və amend olunmuş
  cümlələr düzəldildi. `**Status:**` sətri artıq atomicity və diagnostics-availability
  detallarının amend olunduğunu bildirirdi, lakin sənəddəki yeganə `## ... Amendment`
  bölməsi Branch Shared audit origin-i (`DM-089`/`DM-091`) haqqındadır və tarixsizdir,
  yəni `ADR-0061` amendment-inin heç bir dated qeydi yox idi (`docs/adr/README.md` iki
  şərti birgə tələb edir). İki düzəliş: (1) `## Consequences` əvvəl "audit yarada bilər"
  deyirdi -- audit-i mutation-ın optional yan effekti kimi göstərən bu ifadə audit-i ayrı
  connection-da yazan və business transaction rollback olduqda itirən implementasiya
  tərəfindən ödənilirdi; indi single-transaction/outbox qaydası açıq yazılıb. (2)
  § `Diagnostics export və privacy` availability qaydası daşımırdı, ona görə bu bölmədən
  export implementasiya edən reader-in onu license gate arxasına qoymamaq üçün səbəbi yox
  idi; `ADR-0061`-in "never blocked by a plugin license state" qərarı əlavə olundu.
  Audit/diagnostics boundary qərarı, payload vocabulary-si və redaction siyahısı dəyişmir;
  yeni öhdəlik əlavə edilmir, `ADR-0061`-in mövcud qərarı dəqiq ifadə olunur.
