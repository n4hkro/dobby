# Skeleton Local Decision Log

This file is the human-readable decision anchor for skeleton-local release evidence. It does not approve implementation by itself; approval is valid only when the referenced evidence ledger rows are complete and the approver/date fields are filled.

## Phase A 2026-05-14

Anchor: `phase-a-2026-05-14`

Status: APPROVED - Phase A storage/toolchain evidence-layer approval completed on 2026-05-14.

Designated approver: n4hkro (architecture/security/release approver)

Patch note: AI-assisted evidence patch; human approval identity: n4hkro.

Scope:

- Sprint 4 storage materialization rule layer.
- Sprint 4 package/native asset pin evidence gate.
- Minimum pre-login schema draft review.

Required before closure:

- CLOSED: `storage-materialization.md` required rows have non-`TBD` evidence values, reviewed minimum schema, approver, and closure date.
- CLOSED: `toolchain-pin-gate.md` package/native asset rows have approval-day version/source/package URL/hash/signature/license/vulnerability/approver evidence.
- CLOSED: The Markdown table and fenced JSON pin representation in `toolchain-pin-gate.md` are consistent for Phase A evidence.
- CLOSED: `Phase B implementation may start` is `YES` after all required Sprint 4 evidence rows are complete.

Optional:

- A signed git tag may be added only if this workspace is an initialized git repository and signing is configured. It is not required for Phase A closure in the current workspace. If `.git` and signing configuration materialize during Sprint 4, a signed tag may be added later as additive audit evidence without reopening Phase A, provided the ledger content and approved evidence values do not change.

## Phase B Decisions 2026-05-14

Anchor: `phase-b-decisions-2026-05-14`

Status: APPROVED - Phase B Sprint 4 implementation-detail decisions approved on 2026-05-14.

Designated approver: n4hkro (architecture/security/release approver)

Patch note: AI-assisted decision-register patch; human approval identity: n4hkro.

Scope:

- Reconciled Sprint 4 Phase B decisions D-01 through D-12 for `SPRINT_4_EXECUTION_PLAN.md`.
- D-01 architecture mitigation note for the temporary `Dobby.Infrastructure -> Dobby.Application` print-boundary transitive isolation.
- Implementation-detail decisions that do not amend the frozen Phase A storage/toolchain ledgers.

Out of scope:

- Does not change approved package/native asset evidence in `toolchain-pin-gate.md`.
- Does not change approved storage primitive, schema, DB-at-rest, or provider evidence in `storage-materialization.md`.
- Does not create Sprint 4 implementation-complete evidence; that remains Step 9.14.
- Does not replace the Sprint 9 print-boundary sunset.

Decision ledger:

- D-01: APPROVED - Project-graph isolation strategy is `PrivateAssets="all"` on the temporary `Dobby.Infrastructure -> Dobby.Application` print-boundary `ProjectReference`. Sprint 4 verifies this with two complementary tests: a metadata-aware transitive dependency check that treats `PrivateAssets="all"` project references as non-propagating, and a build-output smoke that locates the repo root by walking up from `AppContext.BaseDirectory` to `Dobby.sln`, then scans `src/Dobby.Audit/bin/**` and `src/Dobby.Security/bin/**` for absence of `Dobby.Application.dll`.
- D-02: APPROVED - Native encrypted SQLite DLL is repo-local at `src/Dobby.Infrastructure/native/win-x64/sqlite3mc_x64.dll` and is copied deterministically to `native/win-x64/sqlite3mc_x64.dll` under test/publish output.
- D-03: APPROVED - Protected-material files are canonical before DB open. `protected-material.initialized.json` is metadata-only and contains only `protectionVersion`, `algorithmVersion`, and `createdAtUtc`; it contains no secret material, user identifiers, license identifiers, or paths. Marker/material mismatch fails closed. File-backed stable device id material is canonical and the `DeviceIdentity` row is a DB mirror.
- D-04: APPROVED - Add `Dobby.Infrastructure -> Dobby.Contracts` only if Sprint 4 storage primitives actually consume `Dobby.Contracts` types. Do not add the edge speculatively.
- D-05: APPROVED - Startup milestone is `PersistenceOpened` with value `startup.persistence-opened`.
- D-06: ALREADY APPROVED - Windows/native adapter strategy remains the Phase A D4-07 choice in `storage-materialization.md`: existing `Dobby.Infrastructure` project with neutral `net10.0` surface plus Windows-specific `net10.0-windows` adapter slice/multi-target strategy. No new project is added for Sprint 4.
- D-07: APPROVED - Unit tests use fake seams for deterministic provider-independent behavior; integration tests perform the native encrypted provider proof.
- D-08: APPROVED - `PreLoginPersistenceOpener` belongs in `Dobby.Infrastructure.Persistence`; `HostBootstrapper` remains thin orchestration between `ConfigLoaded` and `LoginVisible`.
- D-09: APPROVED - Audit retention sweep is a one-shot, non-blocking post-`LoginVisible` Sprint 4 maintenance operation. Do not add a periodic timer in Sprint 4. The task must be observed and support-safe: use `BackgroundOperationReporter` or an equivalent observed wrapper so exceptions, duration, and success/failure are logged without leaking sensitive material.
- D-10: APPROVED - Create C# record types only for pre-login tables with a real Sprint 4 consumer (production code, migration helper, or integration-test fixture), and place them in the approved owner project/namespace. Infrastructure owns migration/DDL artifacts and Infrastructure-owned records only; it must not become the owner of feature repositories.
- D-11: RESOLVED - `SPRINT_4_EXECUTION_PLAN.md` no longer uses the unapproved `ISchemaMigrator` or `ITransactionalUnitOfWork` placeholders. The approved names are `IHostPersistenceMigrator` and `IHostPersistenceTransactionScope`.
- D-12: APPROVED - `PreLoginMigrationV1` carries migration body SQL as embedded constants inside the migration class. Sprint 4 does not add production external `.sql` files, embedded SQL resources, or runtime-loaded SQL assets.

## Phase B Sprint 5 Decisions 2026-05-15

Anchor: `phase-b-sprint5-decisions-2026-05-15`

Status: APPROVED - Phase B Sprint 5 authority decisions approved on 2026-05-15.

Designated approver: n4hkro (architecture/security/release approver)

Patch note: AI-assisted authority-first reconciliation patch; human approval identity: n4hkro.

Scope:

- Closes B-01/B-02 for `SPRINT_5_EXECUTION_PLAN.md` by treating `test-fixture-authority.md` as the approved Sprint 5 TEST-ONLY host-side recovery fixture closure authority, with production vendor key handover deferred to Sprint 11.
- Closes B-03 for `SPRINT_5_EXECUTION_PLAN.md` by approving `System.Management` for the `net10.0-windows` hardware-identity adapter slice only, backed by the completed row in `toolchain-pin-gate.md`.
- Closes B-04 for `SPRINT_5_EXECUTION_PLAN.md` by approving Host-coordinated transaction-aware Owner-bootstrap audit composition through `IHostTransactionAuditTrail`, with canonical `user.create` emission and a closed Owner-bootstrap details schema.
- Closes B-05 for `SPRINT_5_EXECUTION_PLAN.md` by approving the canonical Sprint 5 namespace placements recorded in `docs/08_CODING_STANDARDS.md`.

Out of scope:

- Does not approve production vendor private key material in Sprint 5.
- Does not allow the Sprint 5 TEST-ONLY recovery key or fixture path in production assemblies or release-candidate production custody evidence.
- Does not approve any `System.Management` use outside the `net10.0-windows` hardware-identity adapter slice.
- Does not approve new package pins beyond `System.Management`, or pull Argon2 into Sprint 5.
- Does not approve new audit event names for Owner bootstrap in Sprint 5.
- Does not create `Dobby.Security.Sessions` or `Dobby.Host.Authentication` as canonical Sprint 5 namespaces.

Required before Sprint 5 implementation starts:

- CLOSED: `SPRINT_IMPLEMENTATION_PLAN.md` records the Sprint 5 TEST-ONLY fixture amendment and Sprint 11 production vendor key handover.
- CLOSED: `toolchain-pin-gate.md` contains the approved `System.Management` row with version, source, package URL, hashes, signature, license, vulnerability scan result, approver, and date.
- CLOSED: B-04 Owner-bootstrap audit composition uses Host-coordinated transaction-aware audit append through `IHostTransactionAuditTrail`, with `Dobby.Host` as coordinator and no `Dobby.Security -> Dobby.Audit` reference.
- CLOSED: `docs/08_CODING_STANDARDS.md` contains canonical rows for `Dobby.Host.Login` and `Dobby.Infrastructure.HardwareIdentity`.
- CLOSED: `SPRINT_5_EXECUTION_PLAN.md` cites these authority anchors before marking B-01/B-02, B-03, B-04, and B-05 closed.

Decision ledger:

- D-13: APPROVED - B-01/B-02 Sprint 5 host-side recovery import closure uses the TEST-ONLY fixture authority in `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`. Sprint 5 may embed a TEST-ONLY public key resource in the integration-test assembly and commit pre-signed TEST-ONLY `.dobbyrecovery` fixtures under the test fixture tree; implementation must fill every `PENDING_IMPLEMENTATION_EVIDENCE` field in that ledger before Sprint 5 closure. Production vendor private keys are out of Sprint 5 scope; production vendor public key/resource custody and Admin-generated production `.dobbyrecovery` evidence are Sprint 10/11 release gates.
- D-14: APPROVED - B-03 permits `System.Management` version `10.0.8` for the `net10.0-windows` hardware-identity adapter slice only. The approval basis is ADR-0039's five-source fingerprint requirement for CPU ID, motherboard serial, and disk serial WMI reads. The package row in `toolchain-pin-gate.md#sprint-5-approved-pin-representation` is the required package-governance evidence before `Directory.Packages.props` or project files are edited.
- D-15: APPROVED - B-05 canonical placements are: session lifecycle and remember-me services under the existing `Dobby.Security.Authentication` namespace; login/lock/recovery WPF views and view models under `Dobby.Host.Login`; Owner bootstrap coordination and single-instance/pre-login orchestration under `Dobby.Host.Startup`; hardware identity adapters under `Dobby.Infrastructure.HardwareIdentity`. `Dobby.Security.Sessions` and `Dobby.Host.Authentication` are not created for Sprint 5.
- D-16: APPROVED - B-04 Owner-bootstrap audit composition uses Host-coordinated transaction-aware audit append. `Dobby.Host` owns `OwnerBootstrapCoordinator`, opens one `IHostPersistenceTransactionScope`, calls `Dobby.Security` `OwnerBootstrapService` for `Users`/`Credentials` mutations, and appends the bootstrap audit row through `Dobby.Audit` `IHostTransactionAuditTrail` resolved by Host DI. `Dobby.Security` must not reference `Dobby.Audit`. Owner bootstrap audit emission uses canonical `user.create` with closed details schema `{bootstrapKind=first-user-owner, principalRole=Owner, permissionState=is-owner-short-circuit, composition=B04-transaction-aware-host-audit}`; no new audit event names are added in Sprint 5. Schema enforcement is owned by `OwnerBootstrapCoordinator` emission tests; `AuditDetailsSanitizer` remains an event-agnostic secret-leakage guard.

## Phase B Sprint 5 Authority Decisions 2026-05-16

Anchor: `phase-b-sprint5-authority-2026-05-16`

Status: APPROVED - Phase B Sprint 5 authority reconciliation decisions approved on 2026-05-16.

Designated approver: n4hkro (architecture/security/release approver)

Patch note: AI-assisted authority reconciliation patch; human approval identity: n4hkro.

Scope:

- Closes B-06 / HD-01 for `SPRINT_5_EXECUTION_PLAN.md` by approving the Phase 1 Owner bootstrap permission-state representation.
- Closes B-07 / HD-02 for `SPRINT_5_EXECUTION_PLAN.md` by approving the deterministic `.dobbyrecovery` response wire format and signature/canonicalization rules.
- Closes B-08 / HD-03 for `SPRINT_5_EXECUTION_PLAN.md` by approving an internal recovery-payload-scoped JCS canonical writer and no new package pin.

Out of scope:

- Does not approve Sprint 6 runtime authorization registry, managed-user CRUD, operation target inventory, or permission key parsing in Sprint 5.
- Does not approve any `Permissions` row shape beyond the Phase 1 Owner bootstrap clarification recorded in D-17.
- Does not approve production vendor private key material, production vendor public key custody, or Admin-generated production `.dobbyrecovery` evidence in Sprint 5.
- Does not approve any new package pin beyond the already approved `System.Management` `10.0.8` row.
- Does not change ADR-0033 key-rotation scope; Phase 1 `keyVersion` remains numeric JSON value `1`.

Required before Sprint 5 implementation starts:

- CLOSED: `docs/adr/ADR-0038-local-first-authentication.md` records the Phase 1 Owner permission-state amendment for first-user Owner bootstrap.
- CLOSED: `docs/specs/user-management.md` records the matching First-User Owner Bootstrap clarification and keeps Owner Guard behavior tied to the same `Users.IsOwner=1` source.
- CLOSED: `docs/adr/ADR-0039-owner-password-recovery.md` records the deterministic `.dobbyrecovery` response envelope, `payload.keyVersion`, JCS payload canonicalization, SHA-256, ECDSA P-256 IEEE P1363 64-byte signature output, standard base64 with padding, and DER rejection.
- CLOSED: `SPRINT_5_EXECUTION_PLAN.md` cites D-17, D-18, and D-19 before marking B-06, B-07, and B-08 closed.

Decision ledger:

- D-17: APPROVED - Phase 1 first-user Owner bootstrap represents the required Owner all-grants/protected-principal permission state with the `Users.IsOwner=1` row, not with a separate Owner `Permissions` row. This is the Phase 1 interpretation of ADR-0038's `Users`/`Permissions`/`AuditLog` transaction invariant: the `Users.IsOwner=1` row is the permission-state seed for Owner all-grants short-circuit behavior and participates in the same transaction as the first credential and audit row. This decision is applied together with the ADR-0038 First-user Owner bootstrap invariant amendment and `docs/specs/user-management.md` First-User Owner Bootstrap amendment. D-16 remains the audit-composition decision; its `permissionState=is-owner-short-circuit` audit detail describes this model but does not independently amend the storage model.
- D-18: APPROVED - `.dobbyrecovery` response artifacts use a top-level `payload` object and a top-level `signature` string. `keyVersion` is `payload.keyVersion`, a numeric JSON value with Phase 1 value `1`; `signature.keyVersion` is not part of the wire format. The signed bytes are the UTF-8 no-BOM RFC 8785 (JCS) canonical JSON bytes of the `payload` object only. The payload bytes are hashed with SHA-256 and signed with ECDSA P-256. Signature output is IEEE P1363 fixed-length 64-byte `r||s`, encoded using RFC 4648 §4 standard base64 with padding and the `+/=` alphabet. ASN.1 DER signatures are rejected. This decision is applied together with the ADR-0039 §4 sample and prose amendment.
- D-19: APPROVED - Sprint 5 implements the RFC 8785/JCS canonicalization needed for `.dobbyrecovery` verification as an internal recovery-payload-scoped writer, not as a new NuGet package. No `toolchain-pin-gate.md` row is added and no package-governance amendment is opened. The internal writer is not a general-purpose JSON canonicalization library; tests must cover byte-exact canonical output for the recovery response payload, input key reordering, signature verification with IEEE P1363, DER rejection, and standard base64 parsing.

## Phase B Sprint 6 Authority Decisions 2026-05-25

Anchor: `phase-b-sprint6-authority-2026-05-25`

Status: APPROVED - Phase B Sprint 6 User Management operation-target and Settings namespace/preference authority decisions approved on 2026-05-25.

Designated approver: current human requester in this Codex thread (architecture/security/release approver for this decision)

Patch note: AI-assisted authority-anchor patch; human approval recorded from the current Codex thread.

Scope:

- Closes the Sprint 6 built-in User Management operation target id gate for `SPRINT_6_EXECUTION_PLAN.md`.
- Establishes the exact built-in User Management permission target id used by registry entries, permission keys, grant/revoke rows, authorization tests, and non-disclosing deny assertions.
- Keeps the canonical User Management verb set aligned with `docs/specs/user-management.md` and `docs/adr/ADR-0048-operation-based-authorization.md`.
- Closes the Sprint 6 Settings namespace and durable preference ownership gate for `SPRINT_6_EXECUTION_PLAN.md`.

Out of scope:

- Does not implement the Sprint 6 authorization registry, managed-user CRUD, grant/revoke behavior, Settings/About source behavior, or audit coordination.
- Does not approve any new package pin, schema widening, audit event name, Core `ErrorCode`, public Contracts/SDK surface, or plugin activation/seeding behavior.
- Does not move rendered Settings tree, TopToolbar interaction, theme snapshots, or manual visual evidence from Sprint 7 into Sprint 6.
- Does not amend `docs/02_ARCHITECTURE_MAP.md` or approve a direct `Dobby.Configuration -> Dobby.Infrastructure` project edge.

Required before Sprint 6 implementation starts:

- CLOSED: `SPRINT_6_EXECUTION_PLAN.md` cites `D-20` before registry, permission-key, grant/revoke, and User Management tests are created.
- CLOSED: Sprint 6 implementation uses `users.management` exactly for the built-in User Management operation target id.
- CLOSED: `SPRINT_6_EXECUTION_PLAN.md` cites `D-21` before creating `Dobby.Host.Settings` files or Settings preference surface/persistence tests.
- CLOSED: Sprint 6 implementation treats `Dobby.Host.Settings` as Settings/About orchestration only and keeps durable Settings preference ownership in `Dobby.Configuration` with Host-wired persistence.

Decision ledger:

- D-20: APPROVED - Built-in User Management operation target id is `users.management`. Supported verbs are `view`, `create`, `update`, `delete`, `execute`, and `grant`.
- D-21: APPROVED - Sprint 6 may create `Dobby.Host.Settings` only for authenticated Settings/About source orchestration, bindable UI-facing source models, Host wiring, and Host-mediated Security/Audit coordination. This decision authorizes the namespace placement only; it does not authorize `Dobby.Host.Settings` to own durable preference storage. Host-owned durable preference store classes under `Dobby.Host.Settings` are not approved. Durable Settings preference ownership remains `Dobby.Configuration`. Sprint 6 may create a typed `Dobby.Configuration` Settings preference surface and abstractions for Appearance theme preference and Security/Session source state, backed by the existing `HostConfiguration` table through Host-wired persistence. The `Dobby.Configuration` surface may define typed preference behavior and abstractions, but concrete `HostConfiguration` table access remains supplied by Host-wired persistence outside `Dobby.Configuration`. This decision does not approve an exact durable preference abstraction class name; implementation names must remain within this authority and may not imply Host storage ownership. No direct `Dobby.Configuration -> Dobby.Infrastructure` edge, new schema, package, public Contracts/SDK settings surface, plugin-facing settings access, Architecture Map amendment, or Sprint 7 rendered Settings work is approved unless separately documented.

## Phase B Sprint 7 Package Approval 2026-05-26

Anchor: `phase-b-sprint7-package-approval-2026-05-26`

Status: APPROVED - `HD-S7-001` / `Q1` Sprint 7 package approval completed on 2026-05-26.

Patch note: AI-assisted evidence patch; human approval identity: n4hkro.

Scope:
- Closes `HD-S7-001` / `Q1` for `SPRINT_7_EXECUTION_PLAN.md`.
- Primary package evidence is `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md#sprint-7-package-approval---hd-s7-001--q1`; this decision ledger does not repeat the package evidence table.
- Approves only the exact Sprint 7 runtime UI shell/theme/localization and test-only snapshot/unit/integration package IDs and versions recorded in the Sprint 7 package approval evidence table.
- Does not approve any new package ID, package source, native asset, upgrade, downgrade, package-reference target expansion, or Sprint 7 use of another existing `Directory.Packages.props` pin not listed in the Sprint 7 package approval evidence table, including `xunit.runner.visualstudio`.
- Sprint 7 implementation evidence must still prove no `Directory.Packages.props` drift, no new package source, no native asset, and green package-governance tests.

Decision:
- `HD-S7-001` / `Q1`: APPROVED - Sprint 7 may use the exact existing package set recorded in `toolchain-pin-gate.md#sprint-7-package-approval---hd-s7-001--q1` for the scoped runtime UI shell/theme/localization and test-only snapshot/unit/integration verification work. Any package drift or added package scope requires a separate human decision before package-bearing work proceeds.

## Phase B Sprint 8 Authority Decisions 2026-06-08

Anchor: `phase-b-sprint8-authority-2026-06-08`

Status: APPROVED - Sprint 8 package-scope, plugin trust-resource, durable installed-state/cache, and namespace authority decisions approved on 2026-06-08 and reconciled on 2026-06-09.

Designated approver: current human requester in this Codex thread (architecture/security/release approver for this decision)

Patch note: AI-assisted authority patch; human approval recorded from the current Codex thread.

Scope:
- Closes `HD-S8-001` for `SPRINT_8_EXECUTION_PLAN.md` by approving scoped use of the centrally pinned `JsonSchema.Net` package for plugin manifest schema validation only.
- Closes `HD-S8-002` for `SPRINT_8_EXECUTION_PLAN.md` by approving the production/dev-QA trust-resource model with host-controlled release/build-time trust inputs and fail-closed production constraints.
- Closes `HD-S8-003` for `SPRINT_8_EXECUTION_PLAN.md` by approving host-owned durable post-login plugin install/state/cache persistence with authoritative `InstalledPluginState`, durable derived metadata cache, host-owned migration/backing ownership, and recovery/cleanup invariants.
- Closes `HD-S8-004` for `SPRINT_8_EXECUTION_PLAN.md` by approving narrow, owner-specific Sprint 8 namespace authority constraints and rejecting `Dobby.PluginFramework.Authorization` for authorization policy/enforcement or registry seeding.

Out of scope:
- Does not approve any package ID, version change, package-source change, or package use beyond plugin manifest schema validation with the existing central `JsonSchema.Net` pin.
- Does not approve private keys, placeholder keys, test fixture keys, arbitrary/user-writable runtime trust-anchor paths, or dynamic runtime trust-anchor loading.
- Does not supply the actual production or dev/QA public-key bytes; implementation evidence must still record resource paths/packaging location, key fingerprints or SHA-256 hashes, and approval evidence.
- Does not approve production packaging that includes active dev/QA trust material.
- Does not pre-name final durable installed-state/cache schema/table names; implementation evidence must record schema/table names or the approved backing abstraction, post-login migration ownership, staging layout, durable transaction boundary, cache rebuild/repair behavior, and recovery/cleanup verification before durable writes are implemented.

Required before Sprint 8 implementation starts:
- CLOSED: `SPRINT_8_EXECUTION_PLAN.md` records `HD-S8-001`, `HD-S8-002`, `HD-S8-003`, and `HD-S8-004` in section 13.
- CLOSED: Sprint 8 package-bearing work may use `JsonSchema.Net` only for plugin manifest schema validation.
- CLOSED: `PluginTrustPublicKeyProvider` is framed as a host-controlled trust material resolver, not a user-configurable trust-anchor loader.
- CLOSED: Sprint 8 plugin install/state/cache is host-owned durable post-login persistence; `InstalledPluginState` is authoritative, metadata cache is a durable derived read model, and `Dobby.PluginFramework` must not take a direct `Dobby.Infrastructure` or concrete persistence-adapter dependency.
- CLOSED: Sprint 8 namespace placements require narrow, owner-specific `docs/08_CODING_STANDARDS.md` authority; `Dobby.PluginFramework.Authorization` is not approved for policy/enforcement or registry seeding, and PluginFramework operation-target work must use contribution builder/validator naming only.

Remaining implementation evidence gates:
- Before adding or relying on a Sprint 8 `JsonSchema.Net` `PackageReference`, implementation evidence must prove no package ID, version, source, or use drift.
- CLOSED on 2026-06-09: trust verification wiring records canonical production/dev-QA resource paths, logical resource names, SHA-256 hashes, and explicit current-requester approval. The approved bootstrap private keys remain outside the workspace and require rotation before production release.
- CLOSED on 2026-06-09: explicit Production and DevQa `Dobby.Security` builds plus `PluginTrustResourcePackagingTests` prove each profile embeds only its selected anchor; production contains no dev/QA trust resource.
- Before durable installed-state/cache writes are implemented, evidence must record schema/table names or the approved backing abstraction, post-login migration ownership, staging layout, durable state transaction boundary, cache rebuild/repair behavior, and post-login recovery/cleanup tests.
- Before new Sprint 8 public/plugin-visible or skeleton-critical host-internal namespace files are created, evidence must record the exact narrow `docs/08_CODING_STANDARDS.md` authority rows/docs amendments or relocation into covered placements.

Trust resource implementation evidence (2026-06-09):
- Explicit approval: the current human requester authorized Codex to generate provisional non-main keys, complete the wiring, and leave rotation to a later replacement step.
- Production source: `src/Dobby.Host/Trust/vendor-master-public-key.pem`; logical resource: `Dobby.Security.Trust.vendor-master-public-key.pem`; SHA-256: `00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f`.
- Dev/QA source: `src/Dobby.Host/Trust/dev-qa-public-key.pem`; logical resource: `Dobby.Security.Trust.dev-qa-public-key.pem`; SHA-256: `cb0d35dafea42031e18c778ff208d7b142b280d73ba68983d5f3afccb6a08afc`.
- Private counterparts are intentionally outside the workspace at `C:\Users\Evaks\.dobby-signing-keys\sprint8-bootstrap`, whose ACL grants only the current owner full control. They are bootstrap material and must be rotated before production release.
- Verification: `PluginTrustResourcePackagingTests` passed; explicit `DobbyPluginTrustProfile=Production` and `DobbyPluginTrustProfile=DevQa` builds each succeeded with 0 warnings/errors and exposed only the selected profile resource.

Decision ledger:
- D-22: APPROVED - Sprint 8 may use the centrally pinned `JsonSchema.Net` package only for plugin manifest schema validation. Any new package ID, version change, package-source change, or non-manifest-validation use requires separate approval.
- D-23: APPROVED WITH CONSTRAINTS - Sprint 8 trust verification will use approved production and dev/QA public-key resource files as release/build-time trust inputs resolved by `PluginTrustPublicKeyProvider` from host-controlled internal resources. Production and dev/QA trust anchors must be separate. Resource paths/packaging location, key fingerprints or SHA-256 hashes, and approval evidence must be recorded. The implementation must fail closed if a required key is missing, malformed, mismatched, or if production includes or accepts dev/QA trust. Production runtime must not include an active dev/QA trust anchor; production packaging should not include dev/QA trust material at all. Private keys, placeholder keys, test fixture keys, and arbitrary/user-writable runtime trust-anchor paths are forbidden in production/runtime resources.
- D-24: APPROVED WITH CONSTRAINTS - Sprint 8 plugin install/state/cache is host-owned durable post-login persistence. `InstalledPluginState` is the authoritative durable state for install, enablement, package fingerprint, validation summary, and runtime path. `InstalledPluginMetadataCache` is a durable, derived read model for login-visible display and manager list/detail; it is not the trust, enablement, install, or package-validity truth source and must be rebuildable/repairable from authoritative installed state. Implementation evidence must record schema/table names or the approved backing abstraction, post-login migration ownership, staging layout, durable state transaction boundary, and recovery/cleanup tests. Database and filesystem work must use staging plus recovery/cleanup invariants rather than a simple distributed atomic move claim. `Dobby.PluginFramework` may define domain ports and typed outcomes but must not directly depend on `Dobby.Infrastructure` or a concrete persistence adapter.
- D-25: APPROVED WITH CONSTRAINTS - Planned Sprint 8 namespace/file placements may be kept only after narrow, owner-specific `docs/08_CODING_STANDARDS.md` authority rows/docs amendments are recorded. `Dobby.Security.Trust`, `Dobby.Shell.PluginPages`, `Dobby.Host.Plugins`, and `Dobby.PluginFramework.Packaging` may be authorized through those rows. `Dobby.PluginFramework.Authorization` is not approved for policy/enforcement or registry seeding; PluginFramework may only produce or validate plugin operation-target contributions using contribution builder/validator naming, while actual seeding is host-wired and authorization policy/enforcement plus registry ownership remain `Dobby.Security`-owned.

## Sprint 8 Closure 2026-06-10

Status: IMPLEMENTED AND VERIFIED.

Verification:

- `dotnet build .\Dobby.sln -c Release --no-restore -warnaserror -v minimal` passed with 0 warnings and 0 errors.
- `dotnet test .\Dobby.sln -c Release --no-restore --no-build --logger "console;verbosity=minimal"` passed with 0 failures.
- Supporting suite evidence: Unit 447, Integration 197, Smoke 6.
