# Storage Materialization Phase A Decisions

Status: APPROVED - Phase A storage materialization decisions and evidence were human-approved for Sprint 4 implementation on 2026-05-14 by `n4hkro (architecture/security/release approver)`.

This file records the Sprint 4 storage materialization decisions approved before package-bearing or schema-bearing implementation begins. It approves exact package versions, native asset hashes, table schema evidence, and host-internal primitive names for Sprint 4. It does not approve implementation code beyond the boundaries recorded here.

## Sources

- `SPRINT_IMPLEMENTATION_PLAN.md` - Sprint 4 scope and sequencing authority.
- `SPRINT_4_EXECUTION_PLAN.md` - Sprint 4 execution plan.
- `docs/specs/storage-and-data-protection.md` - storage behavior and P1 materialization authority.
- `docs/adr/ADR-0040-internal-host-adapters-boundary.md` - host-internal adapter and anti-name-invention authority.
- `docs/adr/ADR-0054-db-at-rest-protection-provider.md` - encrypted SQLite provider authority.
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md` - protected-storage scope mapping authority.
- `docs/03_TECH_STACK_AND_VERSIONS.md` - pin-tier and package/toolchain authority.
- `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` - package pin approval gate.

## Cross-Reference Invariant

Every approved decision in this file must link back to an ADR/spec/master-plan section. If this file diverges from an ADR or spec, the ADR/spec must be amended or superseded before implementation uses the divergent decision. This file must not become a second independent source of truth.

## Decision Ledger

| ID | Decision | Approved Direction | Status | Authority / Evidence Link |
| --- | --- | --- | --- | --- |
| D4-01 | Toolchain pin representation | Use `toolchain-pin-gate.md` as the approval ledger; do not create a parallel package approval mechanism here. | APPROVED | `toolchain-pin-gate.md` |
| D4-02 | Sprint 4 package set | Required package/asset scope is `Microsoft.Data.Sqlite.Core`, `SQLitePCLRaw.core`, approved native encrypted SQLite provider asset/package, required `SQLitePCLRaw.provider.dynamic_cdecl` wiring shim, and `Dapper`. All package-bearing changes remain blocked until `toolchain-pin-gate.md` has complete pin evidence. | APPROVED | Tech-stack pin tier + Sprint 4 scope |
| D4-03 | Native supply chain | Prefer the canonical upstream `SQLite3MultipleCiphers` signed/prebuilt release when its source/tag/SHA/license/vulnerability evidence is complete; otherwise use a self-built reproducible Windows native asset. Both paths require the same base evidence fields; self-built additionally requires reproducible Windows native build evidence. | APPROVED | ADR-0054 |
| D4-04 | DB-at-rest cipher/KDF profile | Use provider mode `SQLite3MultipleCiphers` and cipher scheme `sqlcipher_v4`: AES-256-level confidentiality and integrity, PBKDF2-HMAC-SHA512, 256000 iterations, 4096-byte pages, HMAC enabled, plaintext header size 0, install-directory native DLL loading, startup SHA-256 verification, explicit provider initialization, and fail-closed PRAGMA/probe handling. DB-at-rest KDF is not Sprint 5 login KDF / Argon2 scope. | APPROVED | ADR-0054 |
| D4-05 | Primitive surface | Use only the primitive/backing adapter surface listed below under `Dobby.Infrastructure.Persistence`; owner-project use cases remain owned by their ADR-0040 projects. | APPROVED | ADR-0040 section 3.1 / storage spec section 5a |
| D4-06 | ErrorCode registry | Keep the 17-entry Core ErrorCode registry unchanged. Use local `PersistenceError` with a closed sub-code shape unless this ledger explicitly allocates new Core storage codes. | APPROVED | Result/exception policy + ADR-0040 |
| D4-07 | TFM / platform strategy | Use the existing `Dobby.Infrastructure` project with a neutral `net10.0` slice plus Windows-specific `net10.0-windows` adapter slice/multi-target strategy. Do not add `Dobby.Infrastructure.Windows` in Sprint 4 without architecture-map amendment. | APPROVED | Tech-stack TFM guidance + architecture map |
| D4-08 | DeviceIdentity shape | Follow the storage spec: protected `DobbyStableDeviceId` material plus algorithm/protection/version/created metadata. External reference-only blob storage requires explicit approval here and ADR/spec alignment. | APPROVED | Storage spec + ADR-0055 |
| D4-09 | Provider proof | Use named provider-proof assertions and a test-only sentinel marker. Provider-disabled startup, native asset hash mismatch, or wrong provider must fail closed and must not emit `PersistenceOpened`. | APPROVED | AC-037 / AC-038 / AC-042 + ADR-0054 |
| D4-10 | Argon2 scope | `Konscious.Security.Cryptography.Argon2` remains Sprint 5 login KDF scope; no Sprint 4 pin unless a separate human-approved scope pull amends the plan. | APPROVED | Master plan Sprint 5 + AC-051 split |
| D4-11 | Schema authority | Use the closed pre-login table set, `StorageMetadata` as the schema/migration ledger, numeric per-step migration IDs, and only the minimum pre-login required column set. Post-login feature tables and extra columns may only be materialized through later numbered migrations after the authenticated-session seam. | APPROVED | Storage spec section 3.1 / section 5a |
| D4-12 | Evidence placement | This file holds storage materialization rule-layer decisions; `toolchain-pin-gate.md` holds package pin approval evidence. Both must cross-link to ADR/spec authority. | APPROVED | Sprint 4 plan + release evidence tests |

## Package / Asset Approval Table

Rows below are implementation-approved for Sprint 4 when their status is `APPROVED`. Version/asset, license, vulnerability scan date, source proof, SHA-256, approver, and decision link were filled in the Phase A 2026-05-14 closure patch.

| Package / Asset | Required For Sprint 4 | Version / Asset Identifier | Source URL / Commit / Tag | SHA-256 | License | Vulnerability Scan Date | Approver | Decision Link | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Microsoft.Data.Sqlite.Core` | Required | `10.0.8` | `https://github.com/dotnet/dotnet` commit `94ea82652cdd4e0f8046b5bd5becbd11461482ca` | `dc31e393411cfabf62134176a649c5c60993bab4472ef863c0e44a48eb335656` | MIT | 2026-05-14; NuGet advisory index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14` | APPROVED |
| `SQLitePCLRaw.core` | Required | `3.0.3` | `https://github.com/ericsink/SQLitePCL.raw` commit `f3f967adafad853375597a6387cacb7573c5b343` | `30bb8decacdf856c955f78b8a62e79290a7d27c251f37b020af79e52eb032aac` | Apache-2.0 | 2026-05-14; NuGet advisory index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14` | APPROVED |
| Approved native encrypted SQLite provider asset/package | Required | `SQLite3MultipleCiphers v2.3.4` / `sqlite3mc-2.3.4-sqlite-3.53.1-win64.zip` | `https://github.com/utelle/SQLite3MultipleCiphers/releases/tag/v2.3.4`; tag object `6cc1882e85731a3e4d12c746a941d5ac434fe1bc`; release commit `04a7a56392d77fc46007f0023d3eb4b5b701474b` | `f685ab7aed02f79985c6876b7dfa463bb81ebc1f6acd3ce536382cc276467b15` | MIT | 2026-05-14; OSV/NVD SQLite 3.53.1 0 hits; GitHub advisories/issues 0 hits; release notes reviewed | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14` | APPROVED |
| `SQLitePCLRaw.provider.dynamic_cdecl` shim | Required for explicit provider wiring | `3.0.3` | `https://github.com/ericsink/SQLitePCL.raw` commit `f3f967adafad853375597a6387cacb7573c5b343` | `ecc9f8cc6f100c7da4155a5d7b73f4fc4400fe78fc4dfd5589741ca75f71fcf8` | Apache-2.0 | 2026-05-14; NuGet advisory index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14` | APPROVED |
| `Dapper` | Selected for Sprint 4 storage implementation; package reference blocked until pin evidence is complete | `2.1.72` | `https://github.com/DapperLib/Dapper` commit `288730e69b05c32cac898d9b55ebea219ea8a2d1` | `f422b2cfbdbf569e7f188a3127397b4405d38f832f2938058bfedf1b2851a9fe` | Apache-2.0 | 2026-05-14; NuGet advisory index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14` | APPROVED |
| `Konscious.Security.Cryptography.Argon2` | Out of Sprint 4 scope | N/A | N/A | N/A | N/A | N/A | N/A | Sprint 5 / AC-051 | DEFERRED_TO_SPRINT_5 |

Invariant: Sprint 4 implementation may reference `Dapper` because the `Dapper` row here and the matching `toolchain-pin-gate.md` row contain complete version/source/SHA/license/vulnerability-scan/approver evidence. Any future Dapper version/source/hash drift reopens Phase A.

## Native Supply Chain Evidence

The approved native path must be one of:

- Upstream signed/prebuilt release with recorded source URL/tag, SHA-256, license, vulnerability scan date, approver, and decision link.
- Self-built native asset with the same evidence fields plus reproducible Windows native build evidence, including toolchain pin, build script/path, build environment description, and produced artifact SHA-256.

Chosen path: Prefer the canonical upstream `SQLite3MultipleCiphers` signed/prebuilt release artifact when complete release/source/tag/SHA/license/vulnerability-scan evidence is available; use a self-built reproducible Windows native asset if upstream release evidence is incomplete or unacceptable.

Rationale / threat-model note: This matches ADR-0054 supported-path wording. The canonical upstream `SQLite3MultipleCiphers` release is not an unknown third-party prebuilt binary. Both paths still require recorded source URL/tag, SHA-256, license, vulnerability scan date, approver, and decision link before package-bearing implementation; self-built assets additionally require toolchain pin, build script/path, build environment description, and reproducible artifact evidence.

Native asset evidence must keep these fields separate because they prove different things:

- asset local SHA-256 computed from the downloaded or produced asset;
- upstream-published asset digest from the canonical release source, when available;
- git tag signature/provenance evidence for the release tag, when available;
- release commit or source commit/tag targeted by the release;
- asset URL and asset file name used by the host packaging path.

For an upstream release-tag path, approval-day tag verification evidence must record the exact verification method:

- tag reference API endpoint, for example `/repos/utelle/SQLite3MultipleCiphers/git/refs/tags/<tag>`;
- tag object API endpoint, for example `/repos/utelle/SQLite3MultipleCiphers/git/tags/<tag-object-sha>`;
- tag verification result, including `verified: true` and `reason: valid`;
- tag object SHA;
- release commit SHA targeted by the signed tag;
- verification timestamp in ISO-8601 format;
- maintainer GPG key fingerprint when it is available from local `git tag -v` or equivalent GPG verification.

Signed git tag evidence proves release-source provenance; it does not replace asset integrity evidence. Asset integrity still requires local asset SHA-256 and upstream-published digest comparison.

## Native Asset Vulnerability Review Procedure

Approval-day native asset review must record:

1. SQLite version carried by the selected asset and NVD/OSV check result for that SQLite version.
2. `SQLite3MultipleCiphers` GitHub Security advisories and relevant Issues check result for the selected tag/release.
3. Release-notes review for cipher/security-relevant changes, including hardcoded key/nonce/IV or similar cryptographic anti-pattern concerns.
4. Bundled native component inventory from the selected asset, including any separately versioned bundled component that needs an additional vulnerability check.

The native asset row in `toolchain-pin-gate.md` must link to this review output before package-bearing or native-asset-bearing implementation starts.

## DB-At-Rest Cipher / KDF Profile

The following profile is required before implementation. Do not fill with examples unless they are the approved values.

| Field | Approved Value | Status |
| --- | --- | --- |
| Provider mode name | `SQLite3MultipleCiphers`. This field names the provider library/config mode only; the cipher profile is recorded separately below. | APPROVED |
| Cipher scheme | `sqlcipher_v4`. SQLite3MultipleCiphers SQLCipher v4-compatible profile; AES-256-level confidentiality and integrity. | APPROVED |
| DB-at-rest KDF algorithm | PBKDF2-HMAC-SHA512 for DB-at-rest provider key handling. | APPROVED |
| KDF iteration count / cost | 256000 iterations. | APPROVED |
| Salt size / source | Provider-generated 16-byte random SQLCipher salt stored in the encrypted DB header. Plaintext-header mode must remain 0, so no external salt storage path is approved. | APPROVED |
| Page size | 4096 bytes. | APPROVED |
| HMAC / integrity setting | HMAC enabled; SQLCipher v4-compatible per-page integrity using SHA-512-family HMAC. | APPROVED |
| Plaintext header size | 0 bytes. The raw DB header must not remain `SQLite format 3\0`. | APPROVED |
| Provider initialization method | Load `native/win-x64/sqlite3mc_x64.dll` from the install directory under `AppContext.BaseDirectory`; do not load from `%LocalAppData%`, `PATH`, or another user-writable discovery location. Before provider registration, compute the native DLL SHA-256 and compare it to the approved asset evidence. Then explicitly register `SQLitePCLRaw.provider.dynamic_cdecl`; do not use encrypted `SQLitePCLRaw.bundle_*` packages or bundle-style `Batteries_V2.Init()` defaults. Initialization must apply the approved PRAGMA sequence, including `cipher_compatibility=4`, key application, HMAC/integrity settings, `foreign_keys=ON`, and bounded `busy_timeout`. | APPROVED |
| Fail-closed error mapping | Any native DLL hash mismatch, provider load failure, missing/wrong key, provider-disabled state, unsupported PRAGMA result, missing guardrail, or failed provider proof maps to local `PersistenceError`, blocks startup, records support-safe diagnostics only, and does not emit `PersistenceOpened`. | APPROVED |

Boundary invariant: DB-at-rest KDF evidence in Sprint 4 does not approve Sprint 5 login KDF runtime selection, Argon2 package pinning, password KDF floors, or AC-051 latency evidence.

## DB Key Material / Protected Storage Wrapping

Sprint 4 uses two protected material tiers:

- Tier 1 `DBOpenSecret`: 32 random bytes generated by a CSPRNG. This is the DB open secret used by the encrypted SQLite provider. It is protected at rest with `ProtectedData.Protect(DBOpenSecret, optionalEntropy=AppEntropySecret, scope=LocalMachine)`.
- Tier 2 `AppEntropySecret`: 32 random bytes generated by a CSPRNG during first-run bootstrap. It is protected at rest with `ProtectedData.Protect(AppEntropySecret, optionalEntropy=null, scope=LocalMachine)` and stored as a host-owned protected blob under `%LocalAppData%/Dobby/data/`.

`DobbyStableDeviceId` is identity-only material. It is not a DB encryption key, not DEK wrapping entropy, and not a replacement for `AppEntropySecret`.

## Approved Primitive Surface

Primitive names below are approved for Sprint 4. Exact method signatures remain implementation-phase details constrained by ADR-0040 and this ledger. No new Core ErrorCode entry is approved.

| Primitive Role | Approved Type / Namespace | Public Surface Summary | Result / Error Mapping | Owner Project | Status |
| --- | --- | --- | --- | --- | --- |
| Persistence connection factory | `Dobby.Infrastructure.Persistence.IHostPersistenceConnectionFactory` | Creates/opens host-controlled encrypted SQLite connections through the approved provider path and applies connection guardrails. Exact method signatures remain implementation-phase details constrained by ADR-0040 and this ledger. | `Result<T>`/local `PersistenceError`; infrastructure exceptions are translated at the boundary or fail closed. | `Dobby.Infrastructure` | APPROVED |
| Transaction scope | `Dobby.Infrastructure.Persistence.IHostPersistenceTransactionScope` | Owns explicit connection/transaction/cancellation scope for multi-step writes and rollback semantics. This is a transaction primitive, not a domain Unit of Work or change-tracking abstraction. Exact method signatures remain implementation-phase details constrained by ADR-0040 and this ledger. | Transaction conflict/rollback failures use closed local `PersistenceError` sub-codes. | `Dobby.Infrastructure` | APPROVED |
| Schema migrator | `Dobby.Infrastructure.Persistence.IHostPersistenceMigrator` | Applies approved migration IDs idempotently and records migration checksums in the approved ledger table. Exact method signatures remain implementation-phase details constrained by ADR-0040 and this ledger. | Schema mismatch, checksum mismatch, and migration failure use closed local `PersistenceError` sub-codes. | `Dobby.Infrastructure` | APPROVED |
| Command/query executor | `Dobby.Infrastructure.Persistence.IHostPersistenceCommandExecutor` | Provides host-internal parameterized command/query execution over the approved connection/transaction primitives. It is not an owner-project repository or plugin-visible contract. | Constraint, IO, serialization, and query failures use closed local `PersistenceError` sub-codes; Core ErrorCode remains unchanged. | `Dobby.Infrastructure` | APPROVED |
| Local persistence error type | `Dobby.Infrastructure.Persistence.PersistenceError` | Closed sub-code shape with support-safe message/details and optional mapping to existing Core ErrorCode where already defined. Do not use free-form string-only errors. | Local by default; Core ErrorCode only if explicitly allocated | `Dobby.Infrastructure` | APPROVED |

Closed `PersistenceError` sub-code set:

- `ConstraintViolation`: SQLite `FOREIGN KEY`, `UNIQUE`, `CHECK`, or equivalent constraint failure.
- `TransactionConflict`: concurrent write conflict, rollback conflict, or busy-timeout exhaustion while performing a transaction-bound operation.
- `SchemaMismatch`: expected migration ID, schema version, table, column, or checksum does not match the approved migration ledger.
- `MigrationFailed`: migration application failed before the approved schema state was reached.
- `ProviderUnavailable`: native DLL load, approved-hash verification, provider initialization, PRAGMA guardrail, DB key, or provider-proof failure.
- `IoFailed`: filesystem or host data-root IO failure not better classified by a more specific sub-code.
- `SerializationFailed`: serialization/deserialization failure for host persistence payloads.
- `QueryFailed`: parameterized command/query execution failure not caused by a known constraint, transaction, schema, provider, IO, or serialization failure.
- `Unknown`: last-resort classification for an unclassified provider/native-layer failure. If `Unknown` appears in production diagnostics, Phase A must be reopened to allocate a new closed sub-code or explicitly preserve the classification with evidence.

Adding, removing, renaming, or widening a `PersistenceError` sub-code requires Phase A reopen and matching ledger/test updates. Implementers must not invent storage sub-codes during Sprint 4 implementation.

Default rule: storage-specific failures remain in the approved local persistence error type. The Core 17-entry `ErrorCode` registry remains unchanged unless this file explicitly allocates new storage codes and names the matching docs/test updates.

Owner-project guardrail: audit append, recovery nonce, settings, license cache, plugin storage backing behavior, and other use-case repositories remain owned by their ADR-0040 owner projects. `Dobby.Infrastructure.Persistence` provides primitive/backing adapters only; it must not become the owner for feature repositories such as user, audit, settings, licensing, or plugin repositories.

## Schema / Migration Authority

Pre-login table set is closed by the storage spec: `Users`, `Credentials`, `RememberMeSlot`, `Sessions`, `Permissions`, `AuditLog`, `ConsumedRecoveryNonces`, `CustomerDeploymentContext`, `HostConfiguration`, `StorageMetadata`, `DeviceIdentity`.

The approved schema table must include only the minimum pre-login required column set needed for login UI first paint, credential verification, pre-auth audit insertion, idempotent bootstrap, and provider proof. Columns not required for those pre-login behaviors are intentionally left for later numbered migrations.

For each pre-login table, the evidence-layer schema approval must include:

- table name;
- owner project/namespace;
- minimum pre-login columns and types;
- minimum pre-login constraints;
- minimum pre-login indexes;
- pre-login or post-login classification;
- migration phase;
- migration ID.

Migration ledger decision: approved. Use `StorageMetadata` as the approved schema/migration ledger table. The schema table below is approved evidence for the Sprint 4 minimum pre-login bootstrap.

| Migration Area | Approved Convention / Shape | Status |
| --- | --- | --- |
| Migration ledger table | `StorageMetadata` records at least `MigrationId TEXT PRIMARY KEY`, `AppliedAtUtc TEXT`, and `ChecksumSha256 TEXT`; provider-proof/bootstrap metadata may be added only as approved schema fields. | APPROVED |
| Migration ID convention | Numeric prefix plus snake-case step name: `NNNN_descriptive_step_name`. Future migrations append new IDs instead of replacing earlier IDs. | APPROVED |
| Pre-login migration ID | `0001_prelogin_bootstrap` | APPROVED |
| Post-login migration ID | `0002_postlogin_bootstrap` reserved for the first consumer sprint that runs a post-login migration after the authenticated-session seam; subsequent consumer migrations append `0003_*`, `0004_*`, ... | APPROVED |

Post-login boundary: post-login feature tables, non-minimum columns, and feature-specific persistence may be materialized only after the authenticated-session seam is reached; they must not expand the closed pre-login table set. Later additions must use numbered migrations such as `0003_*`, `0004_*`, with `IF NOT EXISTS`, column-exists checks, migration metadata, or equivalent re-entrancy guards so bootstrap -> migration ordering remains idempotent.

## Minimum Pre-Login Schema Draft

Status: APPROVED - this table is the approved minimum pre-login schema evidence for Sprint 4 Phase A closure.

All primary keys are `TEXT` stable identifiers unless noted. Timestamp fields are UTC ISO-8601 text. Hash fields store non-reversible encoded hashes, not raw secrets. Text JSON fields are support-safe payloads that must not contain raw password, token, key, remember-me, recovery, session, license payload, or protected-storage material.

| Table | Owner project / namespace | Minimum pre-login columns and SQLite types | Minimum constraints | Minimum indexes | Classification | Migration ID | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `Users` | `Dobby.Security.Authentication` | `UserId TEXT`, `UsernameNormalized TEXT`, `DisplayName TEXT`, `IsOwner INTEGER`, `IsActive INTEGER`, `CreatedAtUtc TEXT`, `UpdatedAtUtc TEXT` | `PRIMARY KEY (UserId)`; `UsernameNormalized NOT NULL`; `IsOwner IN (0,1)`; `IsActive IN (0,1)` | `UNIQUE (UsernameNormalized)`; filtered/validated single-owner invariant enforced by migration/test logic where SQLite support permits | Pre-login | `0001_prelogin_bootstrap` | ADR-0038 Owner bootstrap; storage spec pre-login set |
| `Credentials` | `Dobby.Security.Authentication` | `CredentialId TEXT`, `UserId TEXT`, `KdfMode TEXT`, `KdfVersion TEXT`, `PasswordHash BLOB`, `Salt BLOB`, `SaltBitLength INTEGER`, `CreatedAtUtc TEXT`, `UpdatedAtUtc TEXT`, `PasswordChangedAtUtc TEXT` | `PRIMARY KEY (CredentialId)`; `FOREIGN KEY (UserId) REFERENCES Users(UserId)`; `SaltBitLength >= 128`; `KdfMode NOT NULL`; `PasswordHash NOT NULL`; `Salt NOT NULL` | `UNIQUE (UserId)` | Pre-login | `0001_prelogin_bootstrap` | ADR-0038 credential verification and salt floor |
| `RememberMeSlot` | `Dobby.Security.Authentication` | `SlotId TEXT`, `UserId TEXT`, `MaskedUsername TEXT`, `ProtectedRememberMeBlob BLOB`, `ProtectionVersion TEXT`, `CreatedAtUtc TEXT`, `UpdatedAtUtc TEXT`, `ExpiresAtUtc TEXT` | `PRIMARY KEY (SlotId)`; `FOREIGN KEY (UserId) REFERENCES Users(UserId)`; one logical slot only by fixed `SlotId`; protected blob required when slot is active | `UNIQUE (SlotId)`; `INDEX (UserId)` | Pre-login | `0001_prelogin_bootstrap` | ADR-0038 remember-me is prefill, not auto-login |
| `Sessions` | `Dobby.Security.Authentication` | `SessionId TEXT`, `UserId TEXT`, `CreatedAtUtc TEXT`, `LastActivityAtUtc TEXT`, `ExpiresAtUtc TEXT`, `InvalidatedAtUtc TEXT`, `InvalidationReason TEXT`, `SessionVersion INTEGER` | `PRIMARY KEY (SessionId)`; `FOREIGN KEY (UserId) REFERENCES Users(UserId)`; `SessionVersion >= 1` | `INDEX (UserId)`; `INDEX (ExpiresAtUtc)` | Pre-login | `0001_prelogin_bootstrap` | ADR-0031 session lifecycle minimum |
| `Permissions` | `Dobby.Security.Authorization` | `PermissionId TEXT`, `UserId TEXT`, `PermissionKey TEXT`, `CreatedAtUtc TEXT`, `UpdatedAtUtc TEXT` | `PRIMARY KEY (PermissionId)`; `FOREIGN KEY (UserId) REFERENCES Users(UserId)`; `PermissionKey NOT NULL` | `UNIQUE (UserId, PermissionKey)`; `INDEX (UserId)` | Pre-login | `0001_prelogin_bootstrap` | ADR-0038 Owner all-grants bootstrap; Sprint 6 expands permission model |
| `AuditLog` | `Dobby.Audit` | `AuditId TEXT`, `OccurredAtUtc TEXT`, `EventName TEXT`, `ActorId TEXT`, `Target TEXT`, `Result TEXT`, `CorrelationId TEXT`, `DetailsJson TEXT` | `PRIMARY KEY (AuditId)`; `OccurredAtUtc NOT NULL`; `EventName NOT NULL`; `Result IN ('success','failed','denied')`; `(application-enforced)` `DetailsJson` must pass audit sanitizer before insert | `INDEX (OccurredAtUtc)`; `INDEX (EventName)`; `INDEX (CorrelationId)` | Pre-login | `0001_prelogin_bootstrap` | Sprint 3 audit surface; `AuditEvent` canonical payload |
| `ConsumedRecoveryNonces` | `Dobby.Security.Recovery` | `NonceHash TEXT`, `ConsumedAtUtc TEXT`, `RequestId TEXT` | `PRIMARY KEY (NonceHash)`; raw nonce must never be stored | `INDEX (RequestId)`; `INDEX (ConsumedAtUtc)` | Pre-login | `0001_prelogin_bootstrap` | ADR-0039 consumed nonce replay prevention |
| `CustomerDeploymentContext` | `Dobby.Licensing.Context` | `ContextId TEXT`, `CustomerId TEXT`, `DeploymentId TEXT`, `Source TEXT`, `ProtectionScope TEXT`, `SchemaVersion INTEGER`, `CreatedAtUtc TEXT`, `UpdatedAtUtc TEXT` | `PRIMARY KEY (ContextId)`; one active row by fixed `ContextId`; `CustomerId NOT NULL`; `DeploymentId NOT NULL`; `SchemaVersion >= 1` | `UNIQUE (CustomerId, DeploymentId)` | Pre-login | `0001_prelogin_bootstrap` | ADR-0052 minimum field set |
| `HostConfiguration` | `Dobby.Configuration` | `ConfigKey TEXT`, `ConfigValue TEXT`, `ValueKind TEXT`, `ProtectionScope TEXT`, `Source TEXT`, `UpdatedAtUtc TEXT` | `PRIMARY KEY (ConfigKey)`; security floor values must not weaken ADR-0027/ADR-0054/ADR-0055 floors | `INDEX (Source)` | Pre-login | `0001_prelogin_bootstrap` | Storage spec startup-safe config floor metadata |
| `StorageMetadata` | `Dobby.Infrastructure.Persistence` | `MigrationId TEXT`, `AppliedAtUtc TEXT`, `ChecksumSha256 TEXT`, `AppliedByVersion TEXT` | `PRIMARY KEY (MigrationId)`; `ChecksumSha256 NOT NULL` for schema migrations | `INDEX (AppliedAtUtc)` | Pre-login | `0001_prelogin_bootstrap` | Migration ledger decision; provider proof metadata owner |
| `DeviceIdentity` | `Dobby.Infrastructure.HardwareIdentity` | `IdentityId TEXT`, `ProtectedStableDeviceIdBlob BLOB`, `AlgorithmVersion TEXT`, `ProtectionVersion TEXT`, `CreatedAtUtc TEXT`, `UpdatedAtUtc TEXT`, `RedactedSupportPrefix TEXT` | `PRIMARY KEY (IdentityId)`; one active row by fixed `IdentityId`; raw stable device id must never be stored as plaintext | SQLite partial unique index on `RedactedSupportPrefix` where non-null/support-safe, e.g. `CREATE UNIQUE INDEX ... WHERE RedactedSupportPrefix IS NOT NULL` | Pre-login | `0001_prelogin_bootstrap` | ADR-0051 device fingerprint v1; ADR-0055 stable device identity relation |

Draft review notes:

- `Permissions` intentionally models only minimal Owner/all-grants and activation gate storage. Row presence represents grant per the ADR-0048 default-deny model; row absence represents no grant/deny, revocation is row deletion, there is no `GrantState` column, and explicit deny storage is structurally impossible. Role/grant matrix behavior remains Sprint 6 scope.
- `Credentials` intentionally omits KDF parameter tuning payload fields from the minimum pre-login schema. Sprint 5+ may add `KdfParametersJson` or an equivalent per-mode parameter field by later numbered ALTER migration.
- `Sessions` intentionally stores only lifecycle metadata needed for expiry/invalidation checks. Refresh-token semantics remain Sprint 5 scope.
- `ProtectionScope` values in this draft are storage/protected-material scope labels such as `'machine-bound'` and `'currentuser-bound'`; they are not plugin storage taxonomy buckets.
- License import state and plugin package license cache tables are not part of the closed pre-login set; they are post-login P1 host state and must use later numbered migrations unless explicitly approved during evidence-layer schema review.
- Any column that would store protected secret material must store only protected blobs or non-reversible hashes according to ADR-0055.

## DeviceIdentity Shape

Approved default: `DeviceIdentity` stores protected `DobbyStableDeviceId` material plus `algorithmVersion`, `protectionVersion`, `createdAt`, and redacted support-prefix metadata per the storage spec.

External protected-blob/reference-only storage: NOT APPROVED unless this section is amended with an explicit human decision and corresponding ADR/spec cross-reference.

## Provider-Proof Assertions

Sprint 4 provider proof must include these named assertions:

- Open encrypted DB without key fails closed.
- Open encrypted DB with wrong key fails closed.
- Open encrypted DB with correct key succeeds and raw header first bytes are not `SQLite format 3\0`.
- Test setup writes the test-only sentinel marker `__dobby_storage_marker_v1__`; production code never writes this marker.
- DB/WAL/journal/temp plaintext scans do not contain `__dobby_storage_marker_v1__`.
- Wrong provider or provider-disabled startup fails closed and does not write the `PersistenceOpened` milestone.

## Implementation Approval State

Sprint 4 implementation may proceed. Required Sprint 4 storage rows above are approved and `toolchain-pin-gate.md` contains complete package/native asset evidence for every selected dependency. `Konscious.Security.Cryptography.Argon2` remains deferred to Sprint 5 and is not a Sprint 4 implementation dependency.

Decision link rule: Phase A closure requires a decision-log anchor under `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-<date>`. A signed git tag is optional and conditional on this workspace being an initialized git repository with signing configured; it is not a Phase A prerequisite in this workspace. If `.git` and signing configuration materialize during Sprint 4, a signed tag may be added later as additive audit evidence without reopening Phase A, provided the ledger content and approved evidence values do not change.

Freeze window: after Phase A closure and before the first Sprint 4 implementation commit/task, these ledger files must not change. Any required ledger change during that window reopens Phase A and requires a separate approval pass before implementation continues.

Phase A closure approver: n4hkro (architecture/security/release approver)

Phase A closure date: 2026-05-14

Phase B implementation may start: YES

## Sprint 4 Implementation Evidence

Date: 2026-05-15 (Asia/Baku)

Status: IMPLEMENTED AND VERIFIED for the approved Sprint 4 Phase B storage-materialization slice.

Implemented evidence:

- Provider-neutral persistence primitives were added under `src/Dobby.Infrastructure/Persistence/` without adding new `Dobby.Core` error codes or changing plugin storage contracts.
- Protected material bootstrap was added under `src/Dobby.Infrastructure/ProtectedStorage/`; runtime material is DPAPI-protected and the initialized marker remains metadata-only.
- The encrypted SQLite provider uses the approved `SQLite3MultipleCiphers` mode and the approved 11-table pre-login migration set with migration ID `0001_prelogin_bootstrap`.
- `StorageMetadata` is the migration ledger; post-login migration infrastructure is available through later numbered migrations and does not expand the closed pre-login table set before login.
- `DeviceIdentity` stores protected stable-device material only; raw stable device IDs are not persisted as plaintext.
- `AuditLog` is backed by the durable audit trail implementation and uses the existing audit sanitizer before insert.
- `ProviderNeutralSkeleton` remains valid outside `Production`; `Production` configuration now requires `SQLite3MultipleCiphers`.
- Startup emits `startup.persistence-opened` only after protected material and pre-login persistence open successfully; provider-disabled or unavailable startup fails closed before the login-visible milestone.

Verification evidence:

- `dotnet build .\Dobby.sln -c Debug --no-restore -warnaserror` passed on 2026-05-15 with 0 warnings and 0 errors.
- `dotnet test .\Dobby.sln -c Debug --no-build --logger "console;verbosity=minimal"` passed on 2026-05-15 with 198 total tests, 0 failures.
- New provider-proof tests verify encrypted-header behavior, wrong-key failure, WAL/journal guardrails, busy timeout, foreign keys, transaction rollback, closed pre-login table set, post-login migration ordering, no sentinel plaintext, no telemetry calls in persistence/audit, and crash recovery integrity.
