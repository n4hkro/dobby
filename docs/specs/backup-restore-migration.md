# Backup, Restore, And Migration

**Tarix:** 2026-04-29
**Status:** Mixed-phase canonical spec — §§ 2.1–2.3 are P1-core (`FR-034`, `AC-054`); §§ 3–4 and the app-key lifecycle remain P2
**Scope:** Backup artifact model, restore trust boundary, audit immutability, maintenance audit evidence, plugin package restore source, cross-device restore, and future app key lifecycle.

## 1. Məqsəd

Backup/Restore self-service raw DB copy deyil. Bu spec host-owned online backup/restore flow-u, cross-device restore request/package modelini və restore zamanı plugin/package trust boundary-ni saxlayır.

## 2. Core Rules

- Backup raw DB file copy deyil; backup engine SQLite online backup API və ya equivalent consistent snapshot mechanism istifadə edir.
- Restore mövcud audit tarixçəsini overwrite etmir və audit immutability-ni pozmur.
- Cross-device restore explicit request/package flow tələb edir; folder copy ilə başqa cihazda app açmaq supported deyil.
- Backup artifact raw password, protected-storage secret, vendor private key və raw license secret detail daşımır.

## 2.1 Standalone Local Implementation

The Standalone host owns backup and restore as authenticated maintenance. It is
not constructed or started by the pre-auth/login-visible composition path. The
canonical requirement is `FR-034`, the acceptance gate is `AC-054` and the
architecture owner is `docs/02_ARCHITECTURE_MAP.md` Cədvəl 1 Row 91.

- Backup uses SQLite's online backup API in cancellable bounded page batches.
- The host forces a WAL checkpoint and verifies both source and snapshot
  integrity before publishing an artifact.
- Snapshot publication uses a create-new partial file, durable flush, SHA-256
  metadata sidecar and atomic promotion. Existing destinations are never
  overwritten.
- Destinations are canonical `.db` paths below the host-owned `backup/` root.
  Reparse-point traversal, insufficient free space and unsafe permissions fail
  closed. Retention removes the oldest snapshot and matching metadata only after
  a new snapshot has been verified and promoted.
- A Standalone snapshot remains encrypted with the host database key and is
  bound to the protected stable device identity. It is not a cross-device
  transport artifact.
- Restore first copies and verifies the candidate in staging. After an explicit
  Owner confirmation, it takes exclusive persistence ownership, checkpoints and
  verifies the current store, and overlays the current immutable `AuditLog`, the
  current `HostOutbox` delivery state and every security-monotonic row listed in
  § 2.2 into the staged candidate. Conflicting audit identifiers fail closed.
- Only then is the current database set moved to a unique support quarantine and
  the staged database promoted. Post-promotion integrity and identity checks are
  mandatory. Failures at either destructive transition restore the quarantined
  database and preserve the failed promoted store for support inspection.
- Startup recovery removes only validated `.tmp`/`.partial` incomplete outputs
  below the host temporary root. It does not follow reparse points or delete
  completed artifacts.
- Every snapshot-path boundary, metadata, integrity, schema/identity,
  evidence-merge and § 2.2 overlay guard surfaces as a typed persistence failure
  with rollback, never as an escaping exception, and its user-facing reason
  carries no full filesystem path or raw exception detail
  (`docs/04_RESULT_VS_EXCEPTION_POLICY.md`, `NFR-004`).
- Every snapshot attempt, restore intent and restore outcome leaves the audit
  evidence required by § 2.3, including refusals.

The current implementation deliberately does not claim cross-device restore.
That requires the request/package lifecycle in section 4 and target-bound key
wrapping rather than copying a local snapshot.

## 2.2 Restore And Security-Monotonic State

Restore is a data-recovery operation, not a security-state rollback vector.
Domain data, host configuration and the Owner credential legitimately return to
the snapshot's values. State whose only purpose is to make an event
unrepeatable must not: promoting an older verified snapshot may never re-arm a
spent single-use proof, clear an active penalty, lower an accepted revision or
resurrect a session.

Before the candidate is promoted, the host overlays the current store's
security-monotonic state into the staged candidate. The overlay is mandatory and
per-row:

| State | Controlling authority | Overlay rule on restore |
|---|---|---|
| `AuditLog` | `ADR-0019` | Current immutable evidence is merged in; a row that reuses an existing audit identifier with different content fails closed |
| `HostOutbox` | `ADR-0061` | Current delivery/lease state replaces the candidate's row for the same idempotency key |
| Consumed recovery nonce ledger | `ADR-0058`, `ADR-0040` | Union: a nonce consumed in either store stays consumed permanently |
| Owner recovery request consumption markers | `ADR-0058` | One-way: consumed markers carry forward, and a candidate request still pending whose nonce is in the merged consumed ledger is dropped |
| Durable login/unlock throttle ledger | `ADR-0058`, `FR-022` | Per bucket the more restrictive record wins: failed-attempt count and bucket version take the maximum, the penalty deadline takes the later value, and a bucket present only in the current store is inserted |
| `licenseRevision` per `revisionSeriesId` | `ADR-0060` | Revision floor: the accepted revision never decreases, and license import attestations are unioned |
| Session disposition | `ADR-0031` | Every session the candidate believes live is invalidated with an explicit restore reason; restore never yields an authenticated session |

Failure semantics:

- The overlay runs in a single transaction against the staged candidate, before
  the current store is quarantined. Any conflict, unreadable persisted value or
  missing target table fails closed with the live store untouched
  (`docs/04_RESULT_VS_EXCEPTION_POLICY.md`).
- Promotion remains one atomic transition with post-promotion integrity and
  device-identity verification (`ADR-0061`); a partially overlaid candidate is
  never promoted.
- Restoring a snapshot that predates a schema migration is accepted only while
  the overlay can still express every state above. Otherwise restore fails
  closed instead of silently dropping a monotonic ledger.
- Restore failure diagnostics stay non-disclosing: no raw nonce, nonce hash,
  credential material or full filesystem path (`ADR-0019`, `NFR-004`).
- Clock-dependent overlay decisions use the injected `TimeProvider`; wall-clock
  sleeps are not evidence (`ADR-0058`, `NFR-027`).

Verification is behavior-first (`NFR-027`): every row above is closed by an
executed restore whose promoted store still shows the non-rolled-back value.
Source inspection, schema shape and a green test list do not close these rows.

## 2.3 Maintenance Audit Evidence

The promotion step is a filesystem store-set move, so it cannot share a provider
transaction with its audit row. `ADR-0061`'s single-transaction rule therefore
applies to the in-database overlay, and this section is the ordering rule that
keeps the operation accountable without one. Event names are owned by
`docs/specs/audit-and-diagnostics.md`.

| Moment | Record | Required details |
|---|---|---|
| Snapshot succeeded | `persistence.snapshot.created` / `success` | `outputName` (bare artifact name), `sha256` |
| Snapshot failed | `persistence.snapshot.created` / `failed` | `outputName`, stable `reasonCode` |
| Before any destructive transition | `persistence.restore.requested` / `success` | `candidateName` |
| Owner confirmation missing | `persistence.restore.requested` / `denied` | `candidateName`, `reasonCode=QuarantineConfirmationMissing` |
| Authorization refused | `permission.denied` / `denied` | operation key target |
| After promotion succeeded | `persistence.restore.completed` / `success` | `candidateName`, `quarantineName` |
| After promotion failed | `persistence.restore.completed` / `failed` | `candidateName`, stable `reasonCode` |

Ordering and failure semantics:

- The intent record is appended to the **current** store before the candidate is
  quarantined. The § 2.2 `AuditLog` overlay then carries it into the promoted
  store, so a store that was replaced can never end up without evidence that a
  restore ran against it.
- If the intent append cannot be persisted, the operation fails closed with a
  typed persistence failure and the live store is not touched. A refusal whose
  audit record cannot be written is likewise reported as a persistence failure,
  never as a silent evidence loss.
- Refusals are recorded before the refusal is returned, so an unauthorized or
  unconfirmed maintenance attempt is itself durable security evidence.
- Details never carry a full path: only a bare artifact or quarantine directory
  name reaches the audit trail, and any name that is empty, oversized or carries
  a separator, drive letter, UNC prefix or control character is replaced by a
  non-disclosing placeholder (`NFR-004`).
- Verification is behavior-first (`NFR-027`): the pair is closed by an executed
  restore whose promoted store still returns the production-emitted intent record
  — where the snapshot was taken before that record existed, so a rolled-back
  audit log would have lost it. Source inspection does not close it.

## 3. Plugin Restore Source

- Plugin binaries backup artifact-a kor daxil edilmir.
- Restore plugin package-ləri signed package source-dan bərpa edir.
- `plugins-registry.json` və ya equivalent trusted registry package id, version, vendor və signature metadata-sını saxlayır.
- Restore zamanı package signature və compatibility host tərəfindən yenidən yoxlanır.
- Missing/untrusted plugin package restore-u fail-closed olur; domain data plugin code olmadan arbitrary şəkildə execute edilmir.

## 4. Cross-Device Restore

- Restore request target device/context identity-ni non-secret support artifact kimi daşıyır.
- Restore package yalnız host verification-dan sonra qəbul edilir.
- Device/customer/deployment binding license lifecycle qaydalarını bypass etmir.

## Backup request and protected content rules

Protected secret backing file-lar raw şəkildə backup/restore package payload-ına yığılmamalıdır. Əgər həmin material gələcək cross-device restore üçün lazımdırsa, yalnız owning protected-storage/restore contract vasitəsilə wrapped və target-bound formada daşınmalıdır.

Shared Data Tools P2 aktivləşdirildikdə mapping profile artifacts backup scope-a yalnız host-owned storage contract vasitəsilə daxil edilir; physical path bu spec-də əvvəlcədən bağlanmır.

Cross-device restore P2/future scope-dan P1-ə çəkilmir. Həmin scope aktivləşdiriləndə cross-device restore request materialı aşağıdakı lifecycle-i saxlamalıdır:

- default expiry 24 saatdır, owning config/policy başqa qərar verməyibsə; bu
  pəncərə Owner recovery artifact pəncərəsindən ayrıdır və onunla eyni state-i
  paylaşmır (`docs/specs/session-lifecycle.md` § `Validity window və single use`);
- Admin Tool və ya package generator expired request əsasında restore package yaratmır;
- generated restore package expiry-si source request `expiresAt` dəyərindən götürülür;
- eyni anda yalnız 1 aktiv restore request saxlanılır; yeni request köhnə request materialını əvəz edir;
- successful consume-dan sonra request materialı delete/invalidate edilir və təkrar istifadə edilə bilməz.

## 5. App Key Lifecycle

Future restore/migration key materialı login-first SLA-nı bloklamır. Local app-side asymmetric key generation/rotation lazımdırsa:

- login UI first paint-dən əvvəl heavy key generation başlamır;
- key generation first-paint sonrası və ya explicit restore/recovery action zamanı lazy/on-demand işləyir;
- key source və protected-storage boundary `ADR-0055` və storage spec ilə uyğun qalır;
- key lifecycle raw key materialı diagnostics/audit export-a daxil etmir.

## 6. Settings Surface

Settings/About yalnız backup/restore status, last backup metadata və support-facing safe metadata göstərə bilər. Raw artifact content, secret key materialı və raw license detail göstərilmir.

## 7. References

- `docs/02_ARCHITECTURE_MAP.md` Cədvəl 1 Row 91 `Host Persistence / Support Snapshot
  And Restore` (P1-core lokal floor) və Future Scope row `Cross-device restore /
  customer-device migration` (P2)
- `FR-034` — lokal support snapshot/verified restore tələbi
- `AC-054` — icra olunmuş snapshot/restore acceptance gate-i
- `FR-011`, `FR-020`, `FR-022`, `FR-033`, `NFR-004`, `NFR-018`, `NFR-027`
- `docs/specs/audit-and-diagnostics.md` — maintenance audit event name owner (§ 2.3)
- `docs/specs/settings-and-about.md`
- `docs/specs/plugins-manager.md`
- `docs/specs/storage-and-data-protection.md`
- `docs/specs/data-retention.md`
- `ADR-0055`
- `ADR-0058` — credential, throttle and Owner recovery v2 authority
- `ADR-0060` — license revision anti-rollback authority
- `ADR-0061` — atomic mutation and outbox boundary

## Branch Shared Backup / Restore Addendum

Standalone backup remains local SQLite/protected-storage oriented.

Branch Shared authoritative backup uses PostgreSQL-native backup/dump tooling
selected by `docs/specs/postgres-deployment-ops.md`. Branch Shared restore must
run post-restore schema/grant validation before the branch is considered
healthy.

Restore-to-production preserves `branchDeploymentId`. Restore-to-test/staging
requires explicit BranchSetup reassignment so cloned environments do not collide
with production identity.

Owner recovery password flow remains consistent with `ADR-0058` (recovery v2
request/response binding, nonce consumption and atomic credential reset) and
`ADR-0055` (protected-material scope). `ADR-0039` is superseded history and is
not a restore-time authority.
BranchSetup does not auto-generate `.dobbyrecoveryrequest` and does not convert
setup proof into recovery proof.

Main-device failure recovery is Tier B/customer-release runbook scope. It is not
a Branch Shared documentation-tier or Tier A skeleton implementation requirement
beyond being
captured in `docs/specs/postgres-deployment-ops.md`.
