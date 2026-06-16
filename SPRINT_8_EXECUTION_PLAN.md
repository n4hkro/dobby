# Sprint 8 Execution Plan - Plugin Package Trust and Activation Runtime

Readiness verdict: HUMAN DECISIONS RECORDED; TRUST RESOURCE EVIDENCE STILL REQUIRED BEFORE TRUST WIRING.

This plan is grounded in the current repo state, the sprint master plan, architecture docs, ADRs, acceptance criteria, and release evidence. It deliberately keeps the implementation path inside the existing plugin framework, security, shell, contracts, and host composition seams.

## 1. Summary

**Goal:** implement the signed, offline, post-login plugin runtime foundation.

**Deliverable:** a trusted local `.dobbyplugin` install/validate/activate pipeline with manifest and package verification, deterministic compatibility and dependency handling, cancellable post-login activation, per-plugin load context isolation, and host-safe extension hosting including navigation, pages, events, and background-operation reporting.

**Why this sprint exists:** Sprint 8 is the first release point where plugins become real runtime participants rather than only static contracts. The work has to land without weakening the login-first rule, the package pin gate, the trust gate, or the plugin-to-host boundary.

**Readiness:** HD-S8-001, HD-S8-002, HD-S8-003, and HD-S8-004 are recorded in section 13. Implementation remains evidence-gated where the recorded decisions require concrete package, namespace-authority, storage-recovery, or trust-resource evidence:
- `JsonSchema.Net` use is approved only for plugin manifest schema validation; any package ID, version, source, or use drift still requires separate approval;
- plugin install/state/cache is approved as host-owned durable post-login persistence; `InstalledPluginState` is the authoritative durable state, metadata cache is a durable derived read model that is not a trust, enablement, or install truth source, and implementation must prove staging, durable transaction, and post-login recovery/cleanup invariants instead of claiming a simple cross-filesystem/database atomic move;
- exact namespace/file placements that create new public/plugin-visible or skeleton-critical host-internal namespaces are approved with constraints through narrow, owner-specific `docs/08_CODING_STANDARDS.md` authority rows; PluginFramework may prepare or validate plugin operation-target contributions, but authorization policy/enforcement and registry ownership remain `Dobby.Security` and actual seeding remains host-wired;
- production and dev/QA plugin trust public key source files must be supplied at the canonical `src/Dobby.Host/Trust/*.pem` paths and embedded as `Dobby.Security` internal compile-time resources, with logical resource names, profile-specific inclusion/exclusion, fingerprints or SHA-256 hashes, approval evidence, and a committed-public-PEM or build-time injected-source strategy recorded before trust wiring is treated as complete.

**Strategy:** keep the runtime inside the existing Architecture Map ownership split:
- `Dobby.PluginFramework` owns package discovery, compatibility, dependency order, activation orchestration, and plugin-facing runtime contracts;
- `Dobby.Security` owns trust verification, embedded plugin trust resource identity/access, and capability boundary enforcement;
- `Dobby.Host` wires login-gated activation into the authenticated runtime;
- `Dobby.Navigation` owns host-normalized plugin route descriptors and navigation composition;
- `Dobby.Shell` owns the allowlisted plugin page render registry and shell page-hosting state; `Dobby.Host/MainWindow.xaml` presents the host-owned WPF surface and background-operation progress after activation through documented contracts.

**Dependency summary:** Sprint 8 depends on Sprints 2-7 and on ADR-0006 through ADR-0013, ADR-0015, ADR-0032, ADR-0033, ADR-0036, ADR-0037, ADR-0045, and ADR-0049. The manifest schema and plugins manager spec are the primary contract references.

## 2. Sources Read

Coverage claim: bounded to the paths named in this section. This section no longer claims a fully enumerated source-path audit; implementation workers must re-check these exact sources and add any extra paths they inspect to their own evidence notes.

Read in four clusters:
- sprint master planning and adjacent sprint execution plans;
- architecture, acceptance, tech-stack, build/release, storage, audit/diagnostics, and coding docs;
- ADRs covering plugin packaging, trust, activation, dependency ordering, and boundary rules;
- current code, tests, samples, and evidence artifacts.

Source set used for this plan:
- `SPRINT_IMPLEMENTATION_PLAN.md`
- `SPRINT_1_EXECUTION_PLAN.md`
- `SPRINT_2_EXECUTION_PLAN.md`
- `SPRINT_3_EXECUTION_PLAN.md`
- `SPRINT_4_EXECUTION_PLAN.md`
- `SPRINT_5_EXECUTION_PLAN.md`
- `SPRINT_6_EXECUTION_PLAN.md`
- `SPRINT_7_EXECUTION_PLAN.md`
- `.gitignore`
- `docs/02_ARCHITECTURE_MAP.md`
- `docs/03_TECH_STACK_AND_VERSIONS.md`
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md`
- `docs/05_FUNCTIONAL_REQUIREMENTS.md`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md`
- `docs/07_ACCEPTANCE_CRITERIA.md`
- `docs/08_CODING_STANDARDS.md`
- `docs/09_BUILD_RUN_AND_RELEASE.md`
- `Directory.Packages.props`
- `docs/specs/storage-and-data-protection.md`
- `docs/specs/audit-and-diagnostics.md`
- `docs/schemas/dobby-plugin-manifest.schema.json`
- `docs/specs/plugins-manager.md`
- `docs/specs/shell-ui.md`
- `docs/specs/sample-plugin.md`
- `docs/adr/ADR-0006-plugin-di-registration.md`
- `docs/adr/ADR-0007-plugin-load-context-strategy.md`
- `docs/adr/ADR-0008-deferred-plugin-activation.md`
- `docs/adr/ADR-0009-plugin-dependency-graph.md`
- `docs/adr/ADR-0010-plugin-communication-policy.md`
- `docs/adr/ADR-0011-plugin-sandbox-enforcement-principle.md`
- `docs/adr/ADR-0012-plugin-capability-boundary.md`
- `docs/adr/ADR-0013-routing-and-deep-link-model.md`
- `docs/adr/ADR-0015-background-operation-reporting.md`
- `docs/adr/ADR-0032-plugin-signature-trust-model.md`
- `docs/adr/ADR-0033-signing-key-ownership.md`
- `docs/adr/ADR-0036-plugin-manifest-schema.md`
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md`
- `docs/adr/ADR-0041-login-first-startup-policy.md`
- `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md`
- `docs/adr/ADR-0048-operation-based-authorization.md`
- `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md`
- `artifacts/release-evidence/skeleton-local/architecture-baseline.md`
- `artifacts/release-evidence/skeleton-local/storage-materialization.md`
- `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`
- `artifacts/release-evidence/skeleton-local/decision-log.md`
- `src/Dobby.PluginFramework/Manifest/PluginManifest.cs`
- `src/Dobby.PluginFramework/Manifest/PluginManifestValidator.cs`
- `src/Dobby.PluginFramework/Activation/PluginDependencySorter.cs`
- `src/Dobby.PluginFramework/Activation/PluginExceptionBoundary.cs`
- `src/Dobby.PluginFramework/Dobby.PluginFramework.csproj`
- `src/Dobby.Contracts/PluginFramework/PluginContracts.cs`
- `src/Dobby.Contracts/PluginPages/PluginPageContracts.cs`
- `src/Dobby.Contracts/Events/EventContracts.cs`
- `src/Dobby.Contracts/BackgroundOperations/BackgroundOperationContracts.cs`
- `src/Dobby.Contracts/PluginStorage/PluginStorageContracts.cs`
- `src/Dobby.Contracts/Policies/PolicyContracts.cs`
- `src/Dobby.Security/Authorization/OperationTargetRegistry.cs`
- `src/Dobby.Security/Authorization/BuiltInOperationTargets.cs`
- `src/Dobby.Navigation/NavigationContribution.cs`
- `src/Dobby.Navigation/NavigationComposer.cs`
- `src/Dobby.Navigation/RouteRegistry.cs`
- `src/Dobby.Shell/ShellViewModel.cs`
- `src/Dobby.Shell/ShellRouteHost.cs`
- `src/Dobby.Shell/Dobby.Shell.csproj`
- `src/Dobby.Host/App.xaml.cs`
- `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`
- `src/Dobby.Host/Login/HostLoginRuntime.cs`
- `src/Dobby.Host/Login/PostLoginSessionRuntime.cs`
- `src/Dobby.Host/MainWindow.xaml`
- `src/Dobby.Host/MainWindow.xaml.cs`
- `src/Dobby.Host/ShellBackgroundOperationCoordinator.cs`
- `src/Dobby.Host/Dobby.Host.csproj`
- `src/Dobby.Security/Dobby.Security.csproj`
- `src/Dobby.Host/Trust/README.md`
- `src/Dobby.Configuration/PluginsOptions.cs`
- `src/Dobby.Configuration/DobbyOptionsValidator.cs`
- `src/Dobby.Host/appsettings.json`
- `src/Dobby.Audit/AuditEventName.cs`
- `src/Dobby.Messaging/InMemoryEventBus.cs`
- `src/Dobby.Application/BackgroundOperationReporter.cs`
- `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`
- `samples/Dobby.Sample.Plugin/Dobby.Sample.Plugin.csproj`
- `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json`
- `samples/Dobby.Sample.Plugin/SamplePlugin.cs`
- `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs`
- `tests/Dobby.UnitTests/Host/PostLoginSessionRuntimeTests.cs`
- `tests/Dobby.UnitTests/Host/ShellBackgroundOperationCoordinatorTests.cs`
- `tests/Dobby.UnitTests/Navigation/NavigationComposerTests.cs`
- `tests/Dobby.UnitTests/Contracts/PluginFrameworkContractTests.cs`
- `tests/Dobby.UnitTests/Contracts/PluginContractDefaultsTests.cs`
- `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`
- `tests/Dobby.IntegrationTests/Architecture/PluginNoInfrastructureReferenceTests.cs`
- `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`
- `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`
- `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`
- `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`
- `tests/Dobby.IntegrationTests/Architecture/Sprint7ReleaseEvidenceTests.cs`

Inspection notes for the added ADR/spec authorities:
- ADR-0006 through ADR-0009 shape the service registration bridge, load context, post-login activation, and dependency result behavior used in sections 6, 10, 12, and 14.
- ADR-0010 through ADR-0013, ADR-0015, and ADR-0048/ADR-0049 shape plugin communication, sandbox/capability boundaries, authorization target seeding, background-operation reporting, and host-owned navigation/page hosting used in sections 6, 12, and 14.
- ADR-0032, ADR-0033, ADR-0036, ADR-0037, ADR-0041, ADR-0045, and ADR-0053 shape manifest, trust, compatibility, login-first, package, and runtime-mode boundaries used in sections 6, 10, 12, 14, and 19.
- ADR-0036 and the plugins manager spec require fail-closed procedural manifest validation that JSON Schema cannot fully express: plugin-owned `operationTargets` namespaces, target-label requirements, duplicate target/id regression coverage, extension id uniqueness, `parentId` existence, and parent-cycle rejection before registry seeding or activation.
- `.gitignore` currently ignores `*.pem` and `*.dobbyplugin`; Sprint 8 must either add narrow exceptions for approved committed public trust PEM resources and any committed package fixtures, or use build/test-time generation or injection strategies with evidence proving the resulting resources/packages are present where the plan expects them.
- `docs/specs/storage-and-data-protection.md` is the source for post-login plugin install/lifecycle/cache state and `%LocalAppData%/Dobby/plugins/` ownership referenced by install-state and metadata-cache planning.
- `docs/specs/audit-and-diagnostics.md` is the source for plugin install/trust/lifecycle audit taxonomy and redacted diagnostics markers for package validation and plugin activation failures.
- AC-018 and ADR-0045 require explicit package-validation coverage for duplicate/case-collision entries, symlink entries, and proof that package fingerprinting ignores raw zip/container metadata and is based only on canonical signed content.

## 3. Sprint Scope

Master goal quote:
> Implement the signed, offline, post-login plugin runtime foundation.

In this sprint, "plugin runtime" means:
- local package validation before load;
- host-owned installed-state and metadata-cache update after successful validation, with no partial install left behind on failure;
- compatibility and dependency resolution before activation;
- activation only after login;
- cooperative activation cancellation on lock, logout, session expiry, or session disposal, with retry on the next post-login activation cycle;
- plugin service registration through the ADR-0006 bridge before activation hooks run;
- host-owned extension hosting through contracts, event bus, page contracts, navigation composition, and background-operation reporting;
- fault isolation so a bad plugin cannot destabilize the host.

Not in scope here:
- licensing runtime enforcement as the primary owner;
- sample plugin finish work as a release artifact;
- plugin marketplace or update/repair/downgrade machinery;
- process isolation, unload, or out-of-process worker support as a primary P1 deliverable.

## 4. Continuity

| Sprint | What already exists | Why Sprint 8 depends on it | Readiness note |
|---|---|---|---|
| 1 | toolchain, architecture, and pin-list guardrails | prevents accidental package drift and boundary violations | ready only when the existing pin evidence is respected |
| 2 | plugin-facing contracts, result policy, shared primitives | supplies the public surfaces plugins may use | ready |
| 3 | config, runtime folders, diagnostics, audit | provides host infrastructure and failure reporting | ready |
| 4 | protected persistence materialization rules | sets the persistence boundary for post-login state | ready, but plugin state schema decisions stay cautious |
| 5 | login-first auth and sessions | plugin activation must wait for login and cancel cooperatively when the authenticated session ends | ready |
| 6 | authorization registry and grant-only rules | plugin-declared operation targets seed into this registry | ready |
| 7 | authenticated shell, navigation, page hosting, work sessions | plugin UI has a place to live only after shell exists | ready |

Sprint 8 is the first sprint where plugin packages become executable host participants. The earlier sprints already provide the contract, shell, security, and login seams that keep this work bounded.

## 5. Current Codebase State

| Area | Current state | Implication for Sprint 8 |
|---|---|---|
| manifest model | `PluginManifest` is skeletal and does not yet express the full signed-package shape from the schema | package parsing and validation need new plumbing, not just a tweak |
| manifest validator | current checks cover schema version, plugin id, semver, host version, capabilities, extension targets, and operation targets | new validation layers must add package completeness, file hashes, fingerprinting, signature, trust, and ADR-0036 procedural validation before registry seeding or activation; compatibility must remain an explicit activation rejection rather than a generic manifest failure |
| dependency sort | existing topological sort exists, but ADR-0009 result semantics are still thin | dependency resolution needs explicit activation-skipped behavior for missing, disabled, circular, min-version, and dependency-activation-failed cases, plus manifest validation failure for self-dependency |
| exception boundary | plugin exceptions already map to host-safe result categories | the runtime can reuse this pattern and extend it for package and activation failures |
| plugin contracts | `IPluginModule`, `IPluginServiceCollection`, `PluginLifetime`, and activation context already exist in Contracts/SDK shape | Sprint 8 must implement the ADR-0006 bridge without exposing host `IServiceProvider` |
| Plugin Service Registration Bridge | no current bridge converts `RegisterServices` declarations into plugin-private services and host extension-point registrations | add explicit bridge, lifetime tracking, plugin-owned singleton disposal, and boundary tests |
| installed state/cache | no current owner is named for installed package state or cached plugin metadata | add an installer/state/cache owner with staging, durable transaction, derived-cache rebuild/repair, post-login recovery/cleanup, and no-partial-install behavior |
| host login runtime | host runtime exists but has no plugin activator in the login path | activation wiring belongs in the authenticated runtime composition |
| login-to-shell transition | `src/Dobby.Host/App.xaml.cs` owns the successful-login branch, shell ViewModel creation, `MainWindow` construction, and background operation wiring | plugin activation coordination must be attached after successful login through this transition without blocking shell first paint |
| post-login session lifecycle | `src/Dobby.Host/Login/PostLoginSessionRuntime.cs` owns lock, logout, expiry, and invalidation transitions | the authenticated plugin activation operation must receive session-owned cancellation and stop at activation step boundaries without persistent status transition or audit, then retry on the next login cycle |
| background operations | plugin-visible `IBackgroundOperationReporter` contracts, a host reporter, shell coordinator, and shell progress rendering already exist | plugin-owned background-operation snapshots must flow through the official reporter with `BackgroundOperationOwnerKind.Plugin`; no custom progress channel or direct shell mutation is allowed |
| shell and page rendering | shell and route hosting exist, but plugin activation is not yet wired into them; `src/Dobby.Host/MainWindow.xaml` renders static built-in content inside `MainContent`, and no host-owned `IPluginPageModel` render registry exists | add an allowlisted `Dobby.Shell` render registry, active plugin page state, and a concrete `MainWindow.xaml` ContentControl surface without accepting plugin-supplied WPF types |
| trust resources | trust key material is not in repo and is explicitly blocked until real bytes are supplied; `Dobby.Security.csproj` does not yet embed the canonical `src/Dobby.Host/Trust/*.pem` inputs; `.gitignore` ignores `*.pem` | production/dev-QA trust verification cannot be faked; `Dobby.Security` must own linked compile-time resources and provider access, with profile-specific inclusion/exclusion evidence plus either narrow committed-public-PEM ignore exceptions or build-time injected-source evidence |
| audit | plugin-related audit event names already exist | host-owned coordination must map typed PluginFramework outcomes to required install, trust, activation, rejection, skipped, and runtime-fault audit records with redacted diagnostics markers without inventing new taxonomy |
| sample plugin | sample package is present as a fixture boundary, not a release-complete deliverable | use dedicated valid/invalid package fixtures generated into test temp/output paths for Sprint 8 package/schema validation unless the sample manifest is explicitly brought into schema alignment; the current sample must not be treated as the canonical signed-package fixture |

## 6. Docs and ADR Alignment

| Requirement cluster | Source basis | Implementation consequence |
|---|---|---|
| plugin package layout and validation | manifest schema, plugins manager spec, ADR-0045, AC-018 | package reader must validate layout, canonical content, duplicate/case-collision entries in manifest and actual package entries, symlink entries, forbidden paths, and raw-container-metadata-independent fingerprinting before activation |
| procedural manifest validation | ADR-0036, plugins manager spec | validator must fail closed on plugin-owned `operationTargets` namespace violations, target-label failures, duplicate target/id regressions, extension id uniqueness failures, missing `parentId` references, and parent cycles before registry seeding or activation |
| trust and signature | ADR-0032, ADR-0033, host trust README | canonical PEM source inputs stay at `src/Dobby.Host/Trust/*.pem`, but `Dobby.Security` owns their linked embedded-resource identity and provider access; runtime filesystem trust loading and placeholder keys are forbidden |
| audit and diagnostics | audit/diagnostics spec, ADR-0032, plugins manager spec, architecture map dependency table | `Dobby.PluginFramework` returns typed, non-disclosing validation/activation outcomes and facts without referencing `Dobby.Audit` or `Dobby.Diagnostics`; host-owned coordination maps those outcomes to required install, trust, activation, skipped, rejected, and runtime-fault audit events plus redacted diagnostics markers without raw secrets or exception text |
| activation timing and cancellation | ADR-0008, ADR-0041 | plugin discovery and activation start only after login; lock, logout, expiry, or session disposal cooperatively cancel in-flight activation without persistent plugin status transition or audit, and the next login retries normally |
| Plugin Service Registration Bridge | ADR-0006, ADR-0008, architecture map row 30 | `IPluginModule.RegisterServices` runs after assembly load and before `OnActivated`; plugin-private services, extension points, lifetimes, and disposal stay host-owned |
| dependency ordering | ADR-0009 | missing, disabled, circular, min-version, and dependency-activation-failed cases are activation skipped with explicit reasons; activation-failure skips cascade to dependents while independent plugins continue; self-dependency is manifest validation failure before activation |
| compatibility | ADR-0037 | stable SemVer and `minHostVersion` are evaluated before activation; pre-release plugin versions fail manifest validation and host version below minimum is `plugin.activation.rejected` with `host-version-below-min` |
| communication boundary | ADR-0010, ADR-0012 | plugin-to-plugin and plugin-to-host communication stays on public contracts and event bus only |
| sandbox and host implementation boundary | ADR-0011, ADR-0012, AC-015, AC-043 | package/runtime validation and tests must reject forbidden host implementation, Infrastructure, other-plugin references, and representative reflection or dynamic-load attempts before activation |
| load context isolation | ADR-0007 | each plugin gets its own non-collectible `AssemblyLoadContext`; shared host assemblies stay pinned, private dependencies stay plugin-scoped, and unmanaged resolution follows the plugin resolver |
| routing and UI hosting | ADR-0013, ADR-0049, architecture map | plugin route descriptor normalization and navigation composition stay in `Dobby.Navigation`; `Dobby.Shell` maps allowlisted `IPluginPageModel` contract types through a host-owned render registry, and `Dobby.Host/MainWindow.xaml` presents the resulting host-owned WPF view without accepting plugin WPF types |
| background-operation reporting | ADR-0015, master Sprint 8 extension-hosting task | plugin progress, cancellation capability, correlation, completion, and failure snapshots use the official background-operation contracts and reach the shell through the host-owned reporter/coordinator path |
| permission target seeding | ADR-0048, AC-022 | validated signed manifest `operationTargets` become PluginFramework-built contributions, then host-owned coordination seeds them into the `Dobby.Security` authorization registry before activation |
| install state and metadata cache | plugins manager spec local file install step 5, storage spec plugin cache rows, ADR-0045 | verified packages move into authoritative durable installed state only after validation succeeds; metadata cache is a durable derived read model, and failed installs/interrupted staging leave no installed/runtime state or unrecoverable artifact |
| runtime mode and licensing boundary | ADR-0053, plugins manager spec | license denial is not an install failure; runtime mode enforcement stays separate |

## 7. Files To Create

These are the implementation files the sprint plan expects to appear once the work starts.

Namespace placement authority: paths in this table that introduce new public/plugin-visible or skeleton-critical host-internal namespace placements are approved only under HD-S8-004's narrow, owner-specific constraints. Before creating files under `Dobby.PluginFramework/Packaging`, `Dobby.Shell/PluginPages`, `Dobby.Security/Trust`, or `Dobby.Host/Plugins`, implementation must cite and verify the exact `docs/08_CODING_STANDARDS.md` table row authority; any uncovered placement must stop for relocation or a new docs amendment. `Dobby.PluginFramework.Authorization` is not an approved placement for policy/enforcement or registry seeding; PluginFramework may only expose narrowly named operation-target contribution builders/validators.

| File | Purpose | Notes |
|---|---|---|
| `src/Dobby.PluginFramework/Packaging/PluginPackageReader.cs` | read `.dobbyplugin` package shape and canonicalize file inventory | read-only package parsing |
| `src/Dobby.PluginFramework/Packaging/PluginPackageValidator.cs` | enforce package completeness, file hashes, duplicate/case-collision rejection, symlink rejection, forbidden paths, and schema checks | package-bearing work stays behind the package-scope gate |
| `src/Dobby.PluginFramework/Packaging/PluginPackageFingerprint.cs` | compute canonical package fingerprint | fingerprint must be based on signed canonical content, not raw zip bytes or raw container metadata such as compression level, timestamps, or entry ordering |
| `src/Dobby.PluginFramework/Packaging/PluginPackageValidationReason.cs` | carry typed validation reasons for non-disclosing failures | reason strings stay local and never become raw exception text |
| `src/Dobby.PluginFramework/Packaging/PluginPackageDescriptor.cs` | capture validated package metadata for activation and install state | keep this separate from raw manifest payload |
| `src/Dobby.PluginFramework/Packaging/PluginPackageInstaller.cs` | move a verified package into host-owned installed state | use staging plus host-owned durable state transaction and post-login recovery/cleanup invariants; do not rely on a simple database/filesystem atomic move claim |
| `src/Dobby.PluginFramework/Packaging/InstalledPluginStateStore.cs` | define the domain port and typed outcomes for authoritative installed plugin lifecycle state | records plugin id, version, package fingerprint, enabled/disabled state, validation result summary, and runtime path; no direct persistence adapter or `Dobby.PluginFramework -> Dobby.Infrastructure` dependency is allowed |
| `src/Dobby.PluginFramework/Packaging/InstalledPluginMetadataCache.cs` | define the domain port and typed outcomes for lightweight cached metadata for login-visible display and manager list/detail | cache manifest display fields and safe status summaries without requiring plugin code execution; this is a durable derived read model, not a trust/install/enablement truth source, and must be rebuildable/repairable from authoritative installed state |
| `src/Dobby.Infrastructure/Persistence/Migrations/0002_postlogin_bootstrap.sql` | create the first post-login plugin install/state/cache backing schema | owns `InstalledPluginState`, `InstalledPluginMetadataCache`, and staging/recovery metadata under the existing post-login migration runner; pre-login bootstrap remains a closed login-visible set |
| `src/Dobby.Infrastructure/Persistence/PostLoginPluginPersistenceMigrations.cs` | expose the deterministic post-login migration dictionary entry for plugin install/state/cache backing | passes the approved first-consumer migration id `0002_postlogin_bootstrap` and SQL into `HostPersistenceMigrator.ApplyPostLoginMigrations`; must not reference `Dobby.PluginFramework` |
| `src/Dobby.PluginFramework/Activation/PostLoginPluginActivator.cs` | orchestrate post-login scan, verify, and activate flow | matches ADR-0008 naming and login gating |
| `src/Dobby.PluginFramework/Activation/PluginLoadContext.cs` | per-plugin load context boundary | follows ADR-0007 |
| `src/Dobby.PluginFramework/Activation/PluginServiceRegistrationBridge.cs` | translate `IPluginModule.RegisterServices` declarations into plugin-private service descriptors and host extension-point descriptors | never passes host `IServiceProvider` or root `IServiceCollection` to plugins |
| `src/Dobby.PluginFramework/Activation/PluginServiceCollectionBuilder.cs` | collect `AddInternal` and `AddExtensionPoint` registrations during activation | validates lifetime and contract registration shape before activation hook |
| `src/Dobby.PluginFramework/Activation/PluginExtensionPointRegistry.cs` | store plugin-provided extension point implementations separately from built-in host services | multiple plugin implementations are allowed; replacement of built-ins stays unsupported |
| `src/Dobby.PluginFramework/Activation/PluginLifetimeManager.cs` | activate, track, and dispose plugin lifetimes | follows ADR-0006, including plugin-owned singleton disposal; Sprint 8 exposes runtime primitives only, not Sprint 10 management workflows |
| `src/Dobby.PluginFramework/Activation/PluginActivationStatus.cs` | surface active, skipped, rejected, and faulted outcomes | aligns with plugins manager spec |
| `src/Dobby.PluginFramework/Activation/PluginDependencyGraph.cs` | isolate dependency resolution and reasoned skips | can wrap the existing sorter or replace it with richer semantics |
| `src/Dobby.PluginFramework/Activation/PluginOperationTargetContributionBuilder.cs` | produce validated plugin operation-target contributions from manifest data | required input for AC-022; does not seed the registry, make authorization decisions, or own authorization policy/enforcement |
| `src/Dobby.PluginFramework/Activation/PluginOperationTargetContributionValidator.cs` | validate plugin operation-target contribution shape before host-owned seeding | keeps PluginFramework language to contribution preparation/validation only; actual seeding is host-wired into the `Dobby.Security` registry |
| `src/Dobby.Navigation/PluginNavigationContributionMapper.cs` | map validated contract-level plugin page metadata into host-normalized navigation contributions | keep route descriptor normalization and composition in the `Dobby.Navigation` owner without adding either a `Dobby.PluginFramework -> Dobby.Navigation` or `Dobby.Navigation -> Dobby.PluginFramework` dependency |
| `src/Dobby.Shell/PluginPages/PluginPageRenderRegistry.cs` | map explicitly approved `IPluginPageModel` contract types to host-owned WPF view factories/presentation descriptors | fail closed for unregistered model types; never accept a plugin `FrameworkElement`, `UserControl`, XAML path, view key, assembly-qualified view type, or host implementation type |
| `src/Dobby.Security/Trust/PluginTrustVerifier.cs` | verify vendor trust, key version, and signature payloads | must use approved key resources only |
| `src/Dobby.Security/Trust/PluginTrustPublicKeyProvider.cs` | resolve only the profile-selected trust anchor from the `Dobby.Security` assembly's internal embedded resources | do not read trust anchors from the runtime filesystem, Host assembly resources, user-writable paths, or configuration; fail closed on missing, mixed, malformed, or unexpected resource identity |
| `src/Dobby.Host/Trust/vendor-master-public-key.pem` | canonical source input for the production plugin trust public key embedded by `Dobby.Security.csproj` | create only from approved public bytes with recorded source path, logical resource name `Dobby.Security.Trust.vendor-master-public-key.pem`, fingerprint or SHA-256 hash, and approval evidence; if committed, add a narrow `.gitignore` exception for this public PEM path; never create a placeholder |
| `src/Dobby.Host/Trust/dev-qa-public-key.pem` | canonical source input for the dev/QA plugin trust public key embedded by `Dobby.Security.csproj` only in the dev/QA trust profile | create only from approved dev/QA public bytes with logical resource name `Dobby.Security.Trust.dev-qa-public-key.pem`; if committed, add a narrow `.gitignore` exception for this public PEM path; exclude from the production `Dobby.Security` assembly and production packaging |
| `src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs` | connect login success to plugin runtime activation and emit host-owned audit/diagnostics records from typed PluginFramework outcomes | wiring and Audit/Diagnostics dependencies belong in host composition; PluginFramework remains free of Audit/Diagnostics references |
| `src/Dobby.Host/Plugins/HostInstalledPluginStateStore.cs` | implement the `InstalledPluginStateStore` port with host-owned persistence primitives | this is the adapter that may reference both `Dobby.PluginFramework` ports and `Dobby.Infrastructure` persistence primitives; writes run inside the durable transaction boundary recorded by HD-S8-003 |
| `src/Dobby.Host/Plugins/HostInstalledPluginMetadataCache.cs` | implement the `InstalledPluginMetadataCache` port with host-owned persistence primitives | updates only the derived cache, supports rebuild/repair from authoritative installed state, and never becomes the trust/install/enablement truth source |
| `src/Dobby.Host/Plugins/PluginInstallStagingArea.cs` | own the package staging layout and post-login cleanup/recovery scan used by `PluginPackageInstaller` | cleanup must recover staged packages and state rows to the last consistent state after failed validation, copy, activation-prep, crash, kill, or power loss |
| `tests/Dobby.UnitTests/PluginFramework/PluginPackageValidationTests.cs` | manifest/package validation coverage | unit tests first |
| `tests/Dobby.UnitTests/PluginFramework/PluginPackageInstallerTests.cs` | recovery-safe installed-state and no-partial-install coverage | prove failed validation/copy/activation-prep and staged interruptions do not create partial installed state |
| `tests/Dobby.UnitTests/PluginFramework/InstalledPluginStateStoreTests.cs` | installed state and metadata cache behavior | covers enable/disable state, fingerprint, cached display metadata, and safe status summaries |
| `tests/Dobby.UnitTests/Host/HostInstalledPluginStateStoreTests.cs` | host adapter coverage for authoritative installed state | proves typed port outcomes, transaction use, and non-disclosing persistence failures without putting adapter logic in PluginFramework |
| `tests/Dobby.UnitTests/Host/HostInstalledPluginMetadataCacheTests.cs` | host adapter coverage for durable metadata cache | proves cache update, rebuild/repair from installed state, and no trust/install/enablement authority |
| `tests/Dobby.UnitTests/Host/PluginInstallStagingAreaTests.cs` | staging layout and cleanup/recovery coverage | proves interrupted staging leaves no durable partial install/runtime state or unrecoverable artifact |
| `tests/Dobby.UnitTests/PluginFramework/PluginDependencyGraphTests.cs` | dependency ordering and skip behavior | deterministic, reasoned results |
| `tests/Dobby.UnitTests/PluginFramework/PluginLoadContextTests.cs` | ADR-0007 load-context isolation coverage | proves distinct non-collectible contexts, host-pinned shared assemblies, plugin-private dependency isolation, and unmanaged resolution behavior |
| `tests/Dobby.UnitTests/PluginFramework/PluginServiceRegistrationBridgeTests.cs` | ADR-0006 registration bridge coverage | proves internal services, extension points, lifetimes, and no `IServiceProvider` exposure |
| `tests/Dobby.UnitTests/Shell/PluginPageRenderRegistryTests.cs` | allowlisted host-owned plugin page rendering coverage | proves approved models resolve to host views and unregistered/plugin-WPF model shapes fail closed |
| `tests/Dobby.UnitTests/Security/PluginTrustVerifierTests.cs` | trust and signature verification coverage | use test-only fixture keys, not production resources |
| `tests/Dobby.UnitTests/Security/PluginTrustPublicKeyProviderTests.cs` | trust anchor resolution coverage | fail closed on missing/malformed/mismatched keys, production/dev-QA mixups, placeholder/test keys, and user-writable paths |
| `tests/Dobby.IntegrationTests/Plugins/PluginPackageFixtureBuilder.cs` | generate dedicated valid/invalid `.dobbyplugin` packages for integration tests | default strategy is runtime generation into test temp/output paths so committed binary `.dobbyplugin` fixtures are not required under the current ignore rule |
| `tests/Dobby.IntegrationTests/Plugins/PluginPackageInstallIntegrationTests.cs` | install path from package to validated state | end-to-end local package workflow |
| `tests/Dobby.IntegrationTests/Plugins/PluginInstallStateIntegrationTests.cs` | package install/state/cache integration | verifies successful install updates state/cache and failed install leaves no installed/runtime state |
| `tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs` | post-login migration and durable adapter integration | applies `0002_postlogin_bootstrap`, verifies table shape through public persistence behavior, transaction rollback, cache rebuild/repair, and recovery cleanup after staged interruption |
| `tests/Dobby.IntegrationTests/Plugins/PluginActivationIntegrationTests.cs` | login-gated activation, session-end cancellation, compatibility, dependency-failure cascading, and fault isolation | host must continue after plugin failure; cancellation creates no persistent status/audit and retries on next login |
| `tests/Dobby.IntegrationTests/Plugins/PluginLoadContextIntegrationTests.cs` | runtime ALC isolation behavior | proves multiple plugins use distinct non-collectible contexts with shared-host type identity, private dependency isolation, and plugin-native resolution |
| `tests/Dobby.IntegrationTests/Plugins/PluginBoundaryReferenceRejectionTests.cs` | package/runtime boundary rejection coverage | rejects forbidden host implementation, Infrastructure, other-plugin references, and representative reflection/dynamic-load attempts before activation |
| `tests/Dobby.IntegrationTests/Plugins/PluginServiceRegistrationBridgeIntegrationTests.cs` | activation-time DI bridge integration | verifies `RegisterServices` precedes `OnActivated`, extension points register, and plugin-owned singleton disposal runs |
| `tests/Dobby.IntegrationTests/Plugins/PluginOperationTargetSeedingTests.cs` | validated manifest seeds registry before activation | AC-022 contribution evidence |
| `tests/Dobby.IntegrationTests/Plugins/PluginEventAndPageHostingTests.cs` | contracts, event bus, and page hosting behavior | plugin UI and events stay on official surfaces |
| `tests/Dobby.IntegrationTests/Plugins/PluginBackgroundOperationReportingTests.cs` | official plugin background-operation reporting behavior | proves plugin-owned progress reaches the shell through host contracts/coordinator without a custom channel |
| `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs` | production/dev-QA trust resource packaging evidence | proves production runtime has no active dev/QA trust anchor and trust resources match recorded path/hash evidence |
| `tests/Dobby.IntegrationTests/Architecture/Sprint8ReleaseEvidenceTests.cs` | verify the sprint did not weaken boundaries | protects release evidence integrity |

Assumptions carried by this file:
1. package validation can be expressed without inventing a new host trust model;
2. plugin trust verification will use only `Dobby.Security` internal embedded resources linked from the canonical `src/Dobby.Host/Trust/*.pem` source inputs, with project-file packaging evidence proving production/dev-QA trust separation;
3. host plugin activation can be wired through the `App.xaml.cs` successful-login transition and authenticated runtime without widening `IServiceProvider` into plugins or blocking shell first paint;
4. manifest parsing will stay package-local and not bleed into host persistence;
5. validated manifest `operationTargets` can become PluginFramework-built operation-target contributions that host-owned coordination seeds into the existing `Dobby.Security` registry before activation;
6. installed package state and metadata cache are host-owned durable post-login persistence under HD-S8-003: `InstalledPluginState` is authoritative, metadata cache is a durable derived read model, PluginFramework owns domain ports/typed outcomes without direct Infrastructure or persistence-adapter dependency, and the concrete baseline is the approved first-consumer `0002_postlogin_bootstrap` post-login migration plus Host-owned adapters in `Dobby.Host.Plugins`;
7. durable plugin install/state/cache implementation must use `PluginInstallStagingArea`, a host-owned durable state transaction, and post-login recovery/cleanup invariants so failed validation, copy, activation-prep, crash, kill, or power loss leaves no partial installed/runtime durable state and either cleans up or returns to the last consistent state on the next post-login scan;
8. host/plugin page hosting uses `Dobby.Navigation` for route normalization, `Dobby.Shell/PluginPages/PluginPageRenderRegistry.cs` for allowlisted model-to-host-view mapping, and `Dobby.Host/MainWindow.xaml` for the concrete WPF presentation surface without turning plugin pages into raw WPF internal types;
9. plugin background-operation reporting can reuse the existing official contracts, reporter, coordinator, and shell rendering path without a custom channel;
10. Sprint 8 package/schema validation will use dedicated valid/invalid package fixtures generated by a test helper into temp/output paths unless the sample manifest is explicitly brought into schema alignment, because sample plugin completion remains out of scope;
11. proposed namespace/file placements are approved with HD-S8-004 constraints through narrow, owner-specific `docs/08_CODING_STANDARDS.md` authority rows; PluginFramework operation-target files must use contribution builder/validator language, while actual seeding and authorization registry ownership remain Host/Security wiring.

## 8. Files To Modify

| File | Expected change |
|---|---|
| `src/Dobby.PluginFramework/Manifest/PluginManifest.cs` | expand the manifest model to cover the schema-owned fields already missing from the skeletal record |
| `src/Dobby.PluginFramework/Manifest/PluginManifestValidator.cs` | expand schema-owned and semantic manifest validation while keeping package validation in `PluginPackageValidator`, trust/signature verification in `PluginTrustVerifier`, and host-version compatibility in the activation compatibility decision |
| `src/Dobby.PluginFramework/Activation/PluginDependencySorter.cs` | replace the thin sort with deterministic ADR-0009 activation-skipped, dependency-activation-failed cascade, and manifest-validation-failure semantics plus reason codes |
| `src/Dobby.PluginFramework/Activation/PluginExceptionBoundary.cs` | keep plugin faults mapped to non-disclosing result categories |
| `src/Dobby.PluginFramework/Dobby.PluginFramework.csproj` | add only approved package references if the package-scope gate is cleared |
| `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj` | include the `Persistence/Migrations/0002_postlogin_bootstrap.sql` migration artifact if the implementation reads SQL from an embedded or content file; do not add a `Dobby.PluginFramework` reference |
| `src/Dobby.Contracts/PluginFramework/PluginContracts.cs` | verify the existing `IPluginModule` and `IPluginServiceCollection` surface remains ADR-0006 compliant; change it only if tests prove a contract gap |
| `src/Dobby.Security/Authorization/OperationTargetRegistry.cs` | accept seeded plugin operation targets from validated manifests |
| `src/Dobby.Security/Authorization/BuiltInOperationTargets.cs` | leave built-in targets intact; do not overload them with plugin metadata |
| `src/Dobby.Host/App.xaml.cs` | start or attach plugin runtime coordination only after successful login, while preserving immediate shell construction and shell-first-paint behavior |
| `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs` | apply `PostLoginPluginPersistenceMigrations` after successful authentication and compose the post-login plugin runtime into the authenticated host runtime only after migration success |
| `src/Dobby.Host/Login/HostLoginRuntime.cs` | carry plugin runtime state if composition requires it |
| `src/Dobby.Host/Login/PostLoginSessionRuntime.cs` | cancel in-flight plugin activation on lock, logout, expiry, invalidation, or session disposal and allow normal retry on the next authenticated cycle |
| `src/Dobby.Host/MainWindow.xaml` | add the host-owned `x:Name="PluginPageHost"` ContentControl/presentation region inside `MainContent`, preserving built-in content and binding only to Shell-owned presentation state produced by `PluginPageRenderRegistry` |
| `src/Dobby.Host/MainWindow.xaml.cs` | initialize plugin runtime only after login success and connect the Shell-owned plugin page presentation state to `PluginPageHost` without accepting plugin-provided WPF objects |
| `src/Dobby.Host/ShellBackgroundOperationCoordinator.cs` | carry plugin-owned official background-operation snapshots to the shell without a plugin-specific progress channel |
| `src/Dobby.Host/Dobby.Host.csproj` | do not embed or dynamically load plugin trust anchors; consume the `Dobby.Security` assembly and keep production packaging checks able to prove no dev/QA trust resource is present transitively |
| `src/Dobby.Security/Dobby.Security.csproj` | own plugin trust resource embedding by linking from the canonical `../Dobby.Host/Trust/*.pem` source paths as `Dobby.Security.Trust.vendor-master-public-key.pem` or `Dobby.Security.Trust.dev-qa-public-key.pem`; define `DobbyPluginTrustProfile` with safe default `Production` and explicit non-production opt-in `DevQa`, embed only the selected profile's approved anchor, never embed dev/QA trust in production, and fail the build for unknown profiles or missing/mixed selected-profile resources |
| `.gitignore` | change only for narrow exceptions when approved public trust PEMs or committed `.dobbyplugin` fixtures are intentionally tracked; keep broad private-material ignores intact, and prefer generated test packages over committed `.dobbyplugin` binaries |
| `src/Dobby.Shell/ShellViewModel.cs` | accept plugin navigation/page contributions after activation and expose only Shell-owned active plugin page presentation state for the WPF host surface |
| `src/Dobby.Shell/ShellRouteHost.cs` | host plugin page routing and lifecycle via the official route/page contracts, then resolve loaded `IPluginPageModel` values through `PluginPageRenderRegistry` before presentation |
| `src/Dobby.Navigation/NavigationComposer.cs` | merge plugin contributions into the shell navigation model |
| `src/Dobby.Configuration/PluginsOptions.cs` | keep pre-login discovery disabled and avoid broadening the preauth surface |
| `src/Dobby.Configuration/DobbyOptionsValidator.cs` | continue to reject pre-login discovery |
| `src/Dobby.Host/appsettings.json` | preserve the login-first plugin posture |
| `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs` | extend existing unit coverage rather than duplicating it in a new style |
| `tests/Dobby.UnitTests/Host/PostLoginSessionRuntimeTests.cs` | prove lock, logout, expiry, invalidation, and disposal signal activation cancellation without persistent plugin status/audit and allow next-login retry |
| `tests/Dobby.UnitTests/Host/ShellBackgroundOperationCoordinatorTests.cs` | prove plugin-owned official snapshots reach shell progress rendering without a plugin-specific channel |
| `tests/Dobby.UnitTests/Navigation/NavigationComposerTests.cs` | prove contract-level plugin descriptors are host-normalized and composed inside `Dobby.Navigation` |
| `tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs` | extend post-login migration coverage to prove `0002_postlogin_bootstrap` uses the existing migration runner, records metadata, and remains distinct from pre-login bootstrap |
| `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs` | add Sprint 8 `JsonSchema.Net` Markdown/JSON parity and no package ID/version/source/use drift verification |
| `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs` | require `MainWindow.xaml` `PluginPageHost` and prove MainWindow never references plugin WPF types, XAML paths, view keys, or assembly-qualified plugin view types |
| `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs` | render the plugin page-host surface with an approved test `IPluginPageModel` mapping and prove built-in content remains available without plugin activation |
| `docs/08_CODING_STANDARDS.md` | verify and cite the existing Sprint 8 namespace authority rows for `Dobby.PluginFramework.Packaging`, `Dobby.Security.Trust`, `Dobby.Shell.PluginPages`, and `Dobby.Host.Plugins`; amend only if implementation introduces a placement not covered by those rows |
| `docs/specs/storage-and-data-protection.md` | no narrowing expected; keep the documented post-login durable plugin install/lifecycle/cache materialization requirement and add implementation evidence only if tests require clarifying the recovery/cleanup invariant |
| `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` | add the human-readable and test-readable Sprint 8 `JsonSchema.Net` package-use evidence representation before relying on its `PackageReference` |
| `artifacts/release-evidence/skeleton-local/architecture-baseline.md` | add `## Sprint 8 Implementation Evidence`, the exact Sprint 8 evidence markers named in section 14, and recorded verification outcomes consumed by `Sprint8ReleaseEvidenceTests` |

## 9. Files Not Touched

Keep these out of this sprint unless a separate decision explicitly opens them:
- `SPRINT_IMPLEMENTATION_PLAN.md` and the earlier sprint plan files;
- docs, ADRs, schema files, and specs, except for the named `toolchain-pin-gate.md` and `architecture-baseline.md` evidence updates after implementation, `docs/08_CODING_STANDARDS.md` verification or a narrow namespace-placement amendment only when a proposed placement is not already covered by existing rows, and storage spec clarifications only if tests prove the approved recovery/cleanup invariant needs explicit wording;
- `global.json`, `Directory.Build.props`, and `Directory.Packages.props` unless package approval is recorded;
- trust placeholder PEMs, private keys, fake certificates, or test keys committed as production resources;
- sample plugin finish work, sample signing artifacts, sample admin tools, or treating the current sample manifest as the canonical signed-package fixture;
- licensing runtime enforcement code paths owned by Sprint 9;
- update, repair, downgrade, marketplace, and out-of-process worker features;
- Branch Shared, Tier-B, and other deferred release surfaces;
- infrastructure or database project references outside the approved architecture map.

## 10. Error And Result Policy

The sprint must keep host-visible failures inside the existing result policy and never leak raw plugin exceptions.

| Condition | Host-facing result shape | Local reason / audit style |
|---|---|---|
| invalid manifest or malformed package | validation failure | `invalid-manifest` |
| missing or extra forbidden file | validation failure | `forbidden-content` |
| file hash mismatch | validation failure | `hash-mismatch` |
| manifest `signature` block missing or invalid | validation failure | `signature-failed` |
| trust anchor mismatch or unapproved vendor | validation failure | `trust-failed` |
| pre-release plugin version | validation failure | `invalid-manifest` before compatibility or activation |
| host version too low | rejected | `plugin.activation.rejected` with `host-version-below-min: required=X.Y.Z installed=A.B.C` |
| missing dependency | skipped | `missing-dependency` |
| dependency version below minimum | skipped | `dependency-version-below-min` |
| circular dependency | skipped | `circular-dependency` with `plugin.activation.skipped` |
| dependency activation failed | skipped | `dependency-activation-failed: <pluginId>` with `plugin.activation.skipped`; cascade to dependents while independent plugins continue |
| self-dependency | validation failure | `self-dependency` |
| activation hook failure | faulted | `activation-failed` |
| session ends during activation | cancelled operation, no plugin status transition | no plugin lifecycle audit; diagnostics may record cancellation; retry on next post-login activation cycle |
| runtime exception from plugin code | faulted | `runtime-faulted` |

Rules:
- keep result values typed and non-disclosing;
- keep package validation and activation outcomes/facts in `Dobby.PluginFramework`, but map them to audit events and diagnostics markers only in host-owned coordination; `Dobby.PluginFramework` must not reference `Dobby.Audit` or `Dobby.Diagnostics`;
- do not print raw exception detail in the host UI or audit trail;
- have host-owned coordination emit the required plugin audit events from the existing taxonomy for `plugin.install.attempted`, `plugin.install.verified`, `plugin.install.rejected`, `plugin.install.signature-failed`, `plugin.install.trust-failed`, `plugin.activation.skipped`, `plugin.activation.rejected`, and `plugin.runtime.faulted`;
- have host-owned coordination emit redacted diagnostics markers for package validation and plugin activation failures without raw secrets, signatures, file contents, or exception detail;
- keep exception boundaries at the plugin runtime edge, not inside plugin code;
- evaluate compatibility separately from manifest shape so host-version mismatch cannot collapse into generic validation failure;
- stop cooperatively at activation step boundaries when the authenticated session ends;
- preserve existing `PluginInternalError`, `InvalidInput`, `UnsupportedOperation`, `Cancelled`, and related host error categories where they already fit.

## 11. Technology Stack And Versions

- target runtime remains the repo baseline: .NET 10 with the current Windows desktop host stack;
- plugin packaging uses BCL zip and crypto primitives, not explicit package references for `System.IO.Compression`, `System.Text.Json`, `System.Security.Cryptography`, or `AssemblyLoadContext`;
- `NuGet.Versioning` is already in use for stable SemVer checks;
- `JsonSchema.Net` is centrally pinned and its manifest-validation-only use is approved by HD-S8-001, but Sprint 8 must add a `dobby.sprint8.toolchainPins.v1` human/test-readable evidence representation and parity/no-drift verification before relying on its `PackageReference`;
- `Microsoft.Extensions.*` should remain within the existing host/shared dependency pattern and not leak service-locator behavior into plugins;
- the plugin runtime should continue to obey the repo's UTF-8, no-BOM, no-trailing-whitespace, and deterministic build rules;
- approved trust public key source files require explicit linked `EmbeddedResource` inclusion in `Dobby.Security.csproj`, stable internal logical resource names, and profile-specific assembly/package evidence; production output must not include an active dev/QA trust anchor;
- no native trust material or private signing material belongs in the source tree.

## 12. Code Work Plan

Before any Sprint 8 implementation file is created, cite and verify the HD-S8-004 namespace authority rows for new public/plugin-visible or skeleton-critical host-internal placements. Before installed-state/cache persistence is implemented, record HD-S8-003 storage evidence confirming the concrete baseline in sections 7 and 8: approved first-consumer migration `0002_postlogin_bootstrap`, `PostLoginPluginPersistenceMigrations`, `HostInstalledPluginStateStore`, `HostInstalledPluginMetadataCache`, `PluginInstallStagingArea`, the durable state transaction boundary, and recovery/cleanup invariant tests. If evidence rejects that baseline or changes file paths, schema/table names, migration ownership, staging layout, transaction ownership, or governing docs, update sections 7, 8, 14, 16, and 18 before continuing.

1. add the `dobby.sprint8.toolchainPins.v1` human/test-readable `JsonSchema.Net` package-use evidence representation and failing `PackageGovernanceTests` parity/no-drift coverage before adding or relying on its Sprint 8 `PackageReference`;
2. write failing unit tests for package validation, ADR-0036 procedural manifest validation, trust failure, canonical hash checks, install-state/no-partial-install behavior, compatibility outcomes, DI bridge behavior, dependency activation-skipped and dependency-activation-failed typed outcome behavior, per-plugin ALC isolation, host-owned audit/diagnostics mapping, and self-dependency manifest validation failure;
3. wire the package reader and validator to the manifest schema and canonical file inventory, including rejection of duplicate/case-collision package entries, symlink entries, forbidden host implementation, Infrastructure, other-plugin references, and representative reflection/dynamic-load attempts before activation;
4. add fail-closed ADR-0036 procedural manifest validation for plugin-owned `operationTargets` namespaces, target-label requirements, duplicate target/id regression coverage, extension id uniqueness, `parentId` existence, and parent-cycle rejection before registry seeding or activation;
5. separate activation compatibility from manifest shape: reject pre-release plugin versions as invalid manifests and reject host versions below `minHostVersion` with `plugin.activation.rejected` and `host-version-below-min`;
6. add trust verification and signature handling behind `PluginTrustPublicKeyProvider`, which reads only `Dobby.Security.Trust.vendor-master-public-key.pem` or `Dobby.Security.Trust.dev-qa-public-key.pem` from its own assembly according to `DobbyPluginTrustProfile`; link approved source PEMs from the canonical `src/Dobby.Host/Trust/*.pem` paths through `Dobby.Security.csproj`; use safe-default `Production` and explicit opt-in `DevQa`; record logical resource name plus path/hash/approval evidence; and prove each profile embeds only its approved anchor;
7. implement `PluginPackageInstaller`, `InstalledPluginStateStore`, and `InstalledPluginMetadataCache` through the HD-S8-003 approved host-owned durable post-login backing: approved first-consumer migration `0002_postlogin_bootstrap` owns the schema, `PostLoginPluginPersistenceMigrations` feeds the existing post-login migration runner, `HostInstalledPluginStateStore` and `HostInstalledPluginMetadataCache` implement the PluginFramework ports from `Dobby.Host.Plugins`, `PluginInstallStagingArea` owns staging and cleanup, PluginFramework exposes only domain ports/typed outcomes, and no `Dobby.PluginFramework -> Dobby.Infrastructure` dependency is introduced;
8. replace the thin dependency sort with explicit ADR-0009 semantics: missing, disabled, minimum-version, circular, and dependency-activation-failed cases return reasoned activation-skipped outcomes that host-owned coordination maps to `plugin.activation.skipped` audit records; failure skips cascade to dependents while independent plugins continue; self-dependency is rejected as manifest validation failure before activation;
9. implement the ADR-0006 service registration bridge: load `IPluginModule`, call `RegisterServices`, collect `AddInternal` and `AddExtensionPoint` declarations, keep plugin-private services out of host DI, register extension points in a separate host-owned registry, and track plugin-owned singleton disposal in `PluginLifetimeManager`;
10. add login-gated activation orchestration through the successful-login path in `src/Dobby.Host/App.xaml.cs` and the authenticated runtime, preserving immediate shell construction and proving plugin activation does not block shell first paint;
11. connect activation lifetime to `PostLoginSessionRuntime` so lock, logout, expiry, invalidation, and session disposal cooperatively cancel at activation step boundaries without persistent plugin status transition or lifecycle audit, and the next successful login retries normally;
12. build and validate plugin operation-target contributions only after procedural manifest validation succeeds; have host-owned coordination seed those contributions into the existing `Dobby.Security` authorization registry without moving authorization policy/enforcement into PluginFramework;
13. add one distinct non-collectible load context per plugin, pin host-shared assemblies, isolate plugin-private dependencies, resolve plugin-native dependencies through the plugin resolver, and run `RegisterServices` after assembly load and before `OnActivated`;
14. map validated plugin navigation/page metadata inside `Dobby.Navigation`; add `Dobby.Shell/PluginPages/PluginPageRenderRegistry.cs` as the allowlisted `IPluginPageModel`-to-host-view mapping boundary; route page lifecycle/presentation through `ShellRouteHost` and `ShellViewModel`; and render the Shell-owned presentation state through `x:Name="PluginPageHost"` in `Dobby.Host/MainWindow.xaml` without accepting plugin-provided WPF objects or adding a `Dobby.PluginFramework -> Dobby.Navigation` dependency;
15. route plugin-owned progress, cancellation capability, correlation, completion, and failure snapshots through `IBackgroundOperationReporter` and the host shell coordinator with `BackgroundOperationOwnerKind.Plugin`;
16. have `Dobby.PluginFramework` return typed, non-disclosing validation/activation outcomes and facts, then have host-owned coordination emit required audit events and redacted diagnostics markers for install attempts, install verification/rejection, signature failure, trust failure, activation skipped/rejected, and runtime faults; keep `Dobby.PluginFramework` free of Audit/Diagnostics references and leave session-end activation cancellation unaudited per ADR-0008;
17. add integration tests for install, installed state/cache, DI registration, compatibility, activation, session-end cancellation/retry, dependency activation-failure cascading, ALC isolation, event/page/navigation hosting through the concrete Shell registry and `MainWindow.xaml` page-host surface, background-operation reporting, AC-015 host-boundary rejection, `Dobby.Security` trust-resource production/dev-QA exclusion, audit/diagnostics emission, and fault isolation;
18. append `## Sprint 8 Implementation Evidence` and the required verification markers to `architecture-baseline.md`, then run the architecture boundary and release-evidence tests to prove the host boundary still holds.

## 13. Decisions And Risks

Resolved human decisions:
- **HD-S8-001:** APPROVED. Sprint 8 may use the centrally pinned `JsonSchema.Net` package only for plugin manifest schema validation. Any new package ID, version change, package-source change, or non-manifest-validation use requires separate approval.
- **HD-S8-002:** APPROVED WITH CONSTRAINTS. Sprint 8 trust verification will use approved production and dev/QA public-key source files at the canonical `src/Dobby.Host/Trust/*.pem` paths, linked and embedded as internal compile-time resources owned by the `Dobby.Security` assembly. `DobbyPluginTrustProfile` has safe default `Production` and explicit non-production opt-in `DevQa`; each profile embeds only its approved anchor under `Dobby.Security.Trust.vendor-master-public-key.pem` or `Dobby.Security.Trust.dev-qa-public-key.pem`. `PluginTrustPublicKeyProvider` resolves only the selected internal resource from its own assembly; Host-loaded injection, runtime filesystem/configuration trust-anchor loading, and user-writable trust paths are not permitted. Source paths, logical resource names, assembly/packaging location, key fingerprints or SHA-256 hashes, and approval evidence must be recorded. The implementation must fail closed if a required key is missing, malformed, mismatched, mixed, or if production includes or accepts dev/QA trust. Private keys, placeholder keys, and test fixture keys are forbidden in production/runtime resources.
- **HD-S8-003:** APPROVED. Sprint 8 plugin install/state/cache is host-owned durable post-login persistence. `InstalledPluginState` is the authoritative durable state for install, enablement, package fingerprint, validation summary, and runtime path. `InstalledPluginMetadataCache` is a durable, derived read model for login-visible display and manager list/detail; it is not the trust, enablement, install, or package-validity truth source and must be rebuildable/repairable from authoritative installed state. The concrete implementation baseline is approved first-consumer migration `0002_postlogin_bootstrap`, `PostLoginPluginPersistenceMigrations`, Host-owned state/cache adapters, `PluginInstallStagingArea`, and tests for the durable state transaction boundary plus post-login recovery/cleanup invariants. Because the database and filesystem cannot be treated as one simple distributed atomic move, failed validation, copy, activation-prep, crash, kill, or power loss must leave no durable installed/runtime state or must be recovered to the last consistent state by the next post-login scan. `Dobby.PluginFramework` may define domain ports and typed outcomes but must not take a direct dependency on `Dobby.Infrastructure` or a concrete persistence adapter, and plugins must not see persistence details.
- **HD-S8-004:** APPROVED WITH CONSTRAINTS. Planned Sprint 8 namespace/file placements may be kept only after the existing narrow, owner-specific `docs/08_CODING_STANDARDS.md` namespace authority rows are cited and verified; add a docs amendment only for a placement not already covered by those rows. `Dobby.Security.Trust`, `Dobby.Shell.PluginPages`, `Dobby.Host.Plugins`, and `Dobby.PluginFramework.Packaging` may be authorized through those rows. `Dobby.PluginFramework.Authorization` is not approved for policy/enforcement or registry seeding; PluginFramework may only produce or validate plugin operation-target contributions using contribution builder/validator naming, while actual seeding is host-wired and authorization policy/enforcement plus registry ownership remain `Dobby.Security`-owned.

Remaining implementation evidence gates:
- record the `dobby.sprint8.toolchainPins.v1` human/test-readable `JsonSchema.Net` package-use representation and pass Markdown/JSON parity plus package ID/version/source/use no-drift checks before adding or relying on a Sprint 8 `PackageReference`;
- record the canonical trust source materialization mode before trust verification wiring is treated as complete: committed approved public PEMs with narrow `.gitignore` exceptions for `src/Dobby.Host/Trust/vendor-master-public-key.pem` and `src/Dobby.Host/Trust/dev-qa-public-key.pem`, or build-time injection into those canonical source paths before `Dobby.Security.csproj` resource embedding;
- record the canonical source paths, logical resource names `Dobby.Security.Trust.vendor-master-public-key.pem` and `Dobby.Security.Trust.dev-qa-public-key.pem`, assembly/packaging location, fingerprints or SHA-256 hashes, and approval evidence before trust verification wiring is treated as complete;
- record `Dobby.Security.csproj` `DobbyPluginTrustProfile=Production` and `DobbyPluginTrustProfile=DevQa` inclusion/exclusion evidence, including proof that each assembly embeds only its approved anchor and production runtime has no active dev/QA trust anchor;
- record that dedicated `.dobbyplugin` package fixtures are generated under test temp/output paths by `PluginPackageFixtureBuilder`; if committed binary fixtures are later chosen, list their exact paths and add narrow `.gitignore` exceptions before relying on them;
- record HD-S8-003 storage evidence before implementing durable installed-state/cache writes by confirming approved first-consumer migration `0002_postlogin_bootstrap`, `PostLoginPluginPersistenceMigrations`, Host-owned state/cache adapters, `PluginInstallStagingArea`, the durable state transaction boundary, recovery/cleanup invariants, and tests proving cache rebuild/repair from authoritative installed state; if evidence changes this baseline, update sections 7, 8, 14, 16, and 18 first;
- cite and verify HD-S8-004 namespace authority before creating proposed new public/plugin-visible or skeleton-critical host-internal namespace placements; update `docs/08_CODING_STANDARDS.md` with a new narrow, owner-specific row or relocate the file before implementation if a placement is not covered.

Known risks:
- trust material policy and `Dobby.Security` resource ownership are approved, but real source path/logical-name/hash/evidence must still be recorded; implementation must not fake it;
- approved public trust PEMs will be silently untracked unless the committed-resource path adds narrow `.gitignore` exceptions, or the build-time injection path proves resource identity and package inclusion another way;
- trust resource packaging can accidentally include dev/QA trust in production or expose runtime filesystem loading unless `Dobby.Security.csproj` inclusion/exclusion and provider access are tested;
- package validation can drift into ad hoc parsing if the schema and canonical content rules are not kept together;
- manifest validation can appear complete while missing ADR-0036 procedural rules that JSON Schema cannot enforce;
- audit and diagnostics coverage or ownership can be missed if tests only assert result codes without proving host-owned event/marker mapping and the absence of PluginFramework Audit/Diagnostics references;
- AC-015 can be missed if package validation only checks project references and DI exposure but not forbidden assembly references, reflection, or dynamic load attempts;
- install can leave unsafe partial state unless package staging, durable installed-state transaction, metadata-cache projection update, and post-login recovery/cleanup are ordered and tested against the HD-S8-003 approved backing strategy;
- namespace drift can violate coding standards if implementation creates Sprint 8 folders outside HD-S8-004's narrow, owner-specific namespace authority; PluginFramework operation-target files can also blur ownership if they use authorization/seeding language instead of contribution builder/validator language;
- plugin DI can leak host services unless the bridge keeps `IServiceProvider` and host root DI out of plugin-visible surfaces;
- plugin activation can accidentally start before login if host composition is not kept strict;
- plugin activation can continue after lock, logout, expiry, invalidation, or session disposal unless its lifetime is bound to authenticated-session cancellation;
- host-version mismatch can be misclassified as generic manifest failure or skipped activation unless ADR-0037 rejection semantics are explicit;
- a failed dependency can leave dependents eligible for activation unless `dependency-activation-failed` cascading is explicit;
- ALC creation can appear complete while still loading shared or private assemblies into the wrong context unless isolation and native resolution are tested;
- plugin UI contribution can leak WPF internals if `PluginPageRenderRegistry` is bypassed or `MainWindow.xaml` accepts plugin-provided presentation objects directly;
- navigation ownership can drift if plugin route normalization is placed in `Dobby.PluginFramework` instead of `Dobby.Navigation`;
- plugin progress can bypass host contracts or mutate shell state directly unless official background-operation reporting is enforced;
- dependency handling can become nondeterministic unless activation-skipped, dependency-activation-failed, and manifest-validation-failure reasons are explicit.

Mitigation:
- write tests around the exact host-visible failure shapes before wiring the host;
- keep all trust decisions, logical resource identities, and provider access in `Dobby.Security`;
- make linked `Dobby.Security` trust-resource embedding explicit and assert each profile contains only its approved anchor, production output has no active dev/QA trust anchor, and provider code has no runtime filesystem/Host-resource fallback;
- make `PluginPackageInstaller` the only transition into installed state and assert no partial install remains after failure;
- make `PluginPackageValidator` fail closed on ADR-0036 procedural manifest rules before authorization seeding or activation;
- make `PluginPackageValidator` reject forbidden host implementation, Infrastructure, other-plugin references, and representative reflection/dynamic-load attempts before activation;
- assert host-owned mapping of typed PluginFramework outcomes to required plugin install/trust/activation audit events and redacted diagnostics markers, plus the forbidden PluginFramework Audit/Diagnostics references, in tests;
- make `PluginServiceRegistrationBridge` the only activation-time DI path and assert plugins never receive host `IServiceProvider`;
- keep activation orchestration in the authenticated runtime and `App.xaml.cs` successful-login transition only;
- bind activation cancellation to `PostLoginSessionRuntime` and assert cancellation has no persistent status transition or lifecycle audit and retries on the next login;
- keep compatibility as an explicit pre-activation decision with exact ADR-0037 rejection reasons;
- assert dependency activation failures cascade only to dependents while independent plugins continue;
- assert each plugin uses a distinct non-collectible ALC with host-shared pinning, private dependency isolation, and plugin-native resolution;
- keep navigation normalization/composition in `Dobby.Navigation`; make `PluginPageRenderRegistry` the only model-to-host-view mapping path; and bind `MainWindow.xaml` only to Shell-owned presentation state from approved page models;
- route plugin progress only through the official background-operation contracts and host shell coordinator;
- make `Sprint8ReleaseEvidenceTests` assert the named `architecture-baseline.md` Sprint 8 markers.

## 14. Test Plan

Unit tests:
- manifest schema and package completeness;
- ADR-0036 procedural manifest validation for plugin-owned `operationTargets` namespaces, target-label requirements, duplicate target/id regression coverage, extension id uniqueness, `parentId` existence, and parent-cycle rejection;
- canonical path normalization and forbidden path rejection;
- duplicate/case-collision rejection across manifest `files` entries and actual package entries;
- symlink entry rejection;
- hash and fingerprint calculation, including proof that fingerprints are based on canonical signed content and not raw zip/container bytes, compression level, timestamps, or entry ordering metadata;
- installed-state transition and metadata-cache update only after successful verification, using the HD-S8-003 approved durable post-login backing strategy;
- no-partial-install cleanup for validation, trust, copy, activation-prep, and state-store failures;
- staging plus durable state transaction plus post-login recovery/cleanup behavior for filesystem/database interruption points, including crash simulation where feasible and cleanup/last-consistent-state recovery on the next post-login scan;
- metadata-cache rebuild/repair from authoritative `InstalledPluginState`, proving cached metadata is not the trust, enablement, install, or package-validity truth source;
- signature and trust verification;
- trust public key provider behavior for missing, malformed, mismatched, placeholder/test, user-writable, production/dev-QA mixed, unexpected logical-name, Host-assembly, and runtime-filesystem trust resources;
- compatibility tests for stable SemVer comparison, pre-release manifest rejection, and exact `minHostVersion` rejection behavior;
- dependency graph ordering, activation-skipped results for missing, disabled, minimum-version, circular, and dependency-activation-failed cases, transitive failure cascading, independent-plugin continuation, and manifest validation failure for self-dependency;
- distinct non-collectible per-plugin ALCs, host-pinned shared assemblies, plugin-private dependency isolation, and unmanaged dependency resolution;
- ADR-0006 service registration bridge behavior for `AddInternal`, `AddExtensionPoint`, plugin lifetimes, and plugin-owned singleton disposal;
- `PluginPageRenderRegistry` allowlisting, approved `IPluginPageModel` mapping, unregistered-model rejection, and rejection of plugin-supplied WPF presentation shapes;
- boundary tests proving plugins never receive host `IServiceProvider`, root `IServiceCollection`, host DI internals, or another plugin's private services;
- package/runtime boundary tests proving forbidden host implementation, Infrastructure, other-plugin references, and representative reflection/dynamic-load attempts are rejected before activation;
- typed, non-disclosing PluginFramework outcome/fact mapping to host-owned audit and diagnostics emission for `plugin.install.attempted`, `plugin.install.verified`, `plugin.install.rejected`, `plugin.install.signature-failed`, `plugin.install.trust-failed`, `plugin.activation.skipped`, `plugin.activation.rejected`, and `plugin.runtime.faulted`, with redacted package-validation and activation-failure diagnostics;
- plugin exception boundary mapping.

Integration tests:
- install from local signed package to validated state;
- successful install updates authoritative installed state and derived metadata cache through the HD-S8-003 approved backing strategy, including post-login migration/materialization behavior;
- failed install leaves no installed package directory, no runtime state, no stale metadata-cache entry, and no unrecoverable staged package/state artifact;
- activation after login only, through the `App.xaml.cs` successful-login transition and without blocking shell first paint;
- cooperative activation cancellation on lock, logout, expiry, invalidation, and session disposal, with no persistent plugin status transition or lifecycle audit and normal retry on the next login;
- host version below `minHostVersion` produces `plugin.activation.rejected` with `host-version-below-min`, while pre-release plugin versions fail manifest validation;
- `RegisterServices` runs after assembly load and before `OnActivated`;
- extension point registrations become host-owned shell/event/page contributions without replacing built-in services;
- host-owned seeding of PluginFramework-built operation-target contributions into the `Dobby.Security` authorization registry only after namespace ownership, labels, duplicates, extension parent existence, and cycle checks pass;
- missing dependency, circular dependency, and dependency-activation-failed cascade behavior, including exact `plugin.activation.skipped` audit reasons and independent-plugin continuation;
- plugin-owned singleton disposal through runtime lifetime-end/cleanup primitives; complete user-facing enable/disable/uninstall workflows remain Sprint 10;
- activation failure and runtime fault isolation;
- host-owned install/trust/activation audit events and redacted diagnostics markers mapped from typed PluginFramework outcomes for validation, trust, activation rejection, skipped activation, and runtime fault paths;
- event bus, `Dobby.Navigation`-owned navigation mapping, `PluginPageRenderRegistry` resolution, Shell-owned active page presentation state, and rendering through `MainWindow.xaml` `PluginPageHost` using official contracts only;
- plugin-owned background-operation snapshots flow through `IBackgroundOperationReporter` and `ShellBackgroundOperationCoordinator` into shell status/progress rendering without a custom channel or direct shell mutation.

Architecture tests:
- plugin projects must remain on Contracts/SDK-style boundaries;
- plugin package code must not drag in Infrastructure or concrete persistence references, including installed-state/cache durable adapters;
- `Dobby.PluginFramework` must not reference `Dobby.Audit` or `Dobby.Diagnostics`; host-owned coordination remains the audit/diagnostics emitter;
- plugin package validation rejects forbidden direct assembly references, reflection/dynamic-load host implementation access, and other-plugin references required by AC-015;
- `Dobby.Security` assembly resource inspection proves `DobbyPluginTrustProfile=Production` embeds only `Dobby.Security.Trust.vendor-master-public-key.pem`, `DobbyPluginTrustProfile=DevQa` embeds only `Dobby.Security.Trust.dev-qa-public-key.pem`, provider access uses only the selected internal resource, production has no active dev/QA trust anchor, and all included trust public keys match recorded source-path/hash approval evidence;
- Shell UI static scan and rendered-shell coverage prove `MainWindow.xaml` declares `PluginPageHost`, approved plugin page models render through `PluginPageRenderRegistry`, built-in content remains available, and plugin WPF types/view identities never enter the host surface;
- host/plugin split must preserve existing repo boundaries;
- namespace placement evidence must prove proposed Sprint 8 public/plugin-visible or skeleton-critical host-internal file placements are covered by the existing narrow, owner-specific `docs/08_CODING_STANDARDS.md` authority rows before creation, or by a new amendment only when a placement is not already covered; PluginFramework operation-target files must use contribution builder/validator placement and must not own authorization seeding or enforcement;
- architecture dependency checks must prove neither `Dobby.PluginFramework -> Dobby.Navigation` nor `Dobby.Navigation -> Dobby.PluginFramework` is introduced;
- no new forbidden package governance drift should appear.
- `toolchain-pin-gate.md` must contain a `dobby.sprint8.toolchainPins.v1` human/test-readable representation whose package ID, version, source, and manifest-validation-only use match the approved current pin;
- `Sprint8ReleaseEvidenceTests` must assert the named `architecture-baseline.md` `## Sprint 8 Implementation Evidence` section and required boundary/verification markers.
- Sprint 8 package-validation fixtures must be dedicated valid/invalid fixtures unless the sample manifest is explicitly brought into schema alignment; the current sample manifest must not be used as the canonical signed-package fixture.
- Dedicated `.dobbyplugin` package fixtures are generated during tests into temp/output paths by `PluginPackageFixtureBuilder`; if implementation switches to committed binary fixtures, their exact paths and narrow `.gitignore` exceptions become required test/setup evidence.

Evidence target:
- Sprint 7 closed with 558 tests and no failures in the recorded baseline; Sprint 8 should add tests rather than eroding that baseline.
- Sprint 8 release evidence is appended to `artifacts/release-evidence/skeleton-local/architecture-baseline.md` under `## Sprint 8 Implementation Evidence`; `tests/Dobby.IntegrationTests/Architecture/Sprint8ReleaseEvidenceTests.cs` asserts that section and the exact markers `SPRINT8-PACKAGE-GOVERNANCE`, `SPRINT8-TRUST-RESOURCE-PACKAGING`, `SPRINT8-COMPATIBILITY-DEPENDENCY`, `SPRINT8-SESSION-CANCELLATION`, `SPRINT8-ALC-ISOLATION`, `SPRINT8-PAGE-RENDER-HOSTING`, `SPRINT8-BACKGROUND-OPERATION`, and `SPRINT8-BOUNDARY-VERIFICATION`.

## 15. Out Of Scope

- licensing runtime enforcement, import UX, and runtime mode enforcement as the primary owner;
- sample plugin polish and sample admin tooling;
- complete Plugins Manager list/details/install/enable/disable/uninstall/status/fault workflows, permissions, confirmations, and pending-deletion/restart UX owned by Sprint 10; Sprint 8 implements only runtime lifetime/disposal primitives and consumes disabled state;
- treating the current sample manifest as the canonical signed-package fixture unless it is explicitly brought into schema alignment;
- plugin marketplace, update, repair, downgrade, restore, or online sync;
- process isolation or unload as a P1 promise;
- new core error taxonomy, new audit event names, or schema widening not already approved;
- pre-login plugin discovery or activation;
- any plugin-facing host persistence exposure;
- any fake or placeholder trust key material;
- any hidden dependency on Infrastructure or host implementation types from plugins.

## 16. Definition Of Done

This sprint is done when all of the following are true:
- signed local `.dobbyplugin` packages validate offline and reject tampered, unsigned, untrusted, duplicate/case-collision, symlink, or forbidden content;
- HD-S8-003 storage evidence confirms approved first-consumer migration `0002_postlogin_bootstrap`, `PostLoginPluginPersistenceMigrations`, Host-owned state/cache adapters, `PluginInstallStagingArea`, durable state transaction boundary, and recovery/cleanup invariant tests before installed-state/cache persistence is implemented; if evidence changes that baseline, sections 7, 8, 14, 16, and 18 are updated first;
- verified packages enter authoritative durable `InstalledPluginState`, and the metadata cache is updated as a durable derived read model that can be rebuilt/repaired and is not the trust, enablement, install, or package-validity truth source;
- failed validation, trust, copy, activation-prep, crash, kill, or power-loss interruption leaves no durable partial installed/runtime state, stale metadata-cache entry, or unrecoverable staged package/state artifact; the next post-login scan cleans up or returns to the last consistent state;
- plugin activation happens only after login through the authenticated `App.xaml.cs` transition and does not block shell first paint;
- lock, logout, expiry, invalidation, or session disposal cooperatively cancels in-flight activation at step boundaries, creates no persistent plugin status transition or lifecycle audit, and allows normal retry on the next login;
- stable SemVer compatibility is deterministic, pre-release plugin versions fail manifest validation, and host version below `minHostVersion` produces `plugin.activation.rejected` with the exact `host-version-below-min` reason;
- package fingerprints are based on canonical signed content and are independent of raw zip/container bytes, compression level, timestamps, and entry ordering metadata;
- `IPluginModule.RegisterServices` is bridged before `OnActivated` without exposing host `IServiceProvider`, root DI, or cross-plugin private services;
- plugin packages or activation attempts that use forbidden host implementation, Infrastructure, other-plugin references, reflection, or dynamic load are rejected before activation;
- plugin-owned singleton services are disposed when the plugin lifetime ends;
- dependency ordering is deterministic and reasoned, including `dependency-activation-failed` cascading to dependents while independent plugins continue;
- plugin failures stay isolated and non-disclosing;
- each activated plugin uses its own non-collectible ALC with host-pinned shared assemblies, plugin-private dependency isolation, and plugin-native dependency resolution;
- manifest validation fails closed on ADR-0036 procedural rules before registry seeding or activation, including plugin-owned `operationTargets` namespace validation, target-label requirements, duplicate target/id regression coverage, extension id uniqueness, `parentId` existence, and parent-cycle rejection;
- validated manifest `operationTargets` become PluginFramework-built operation-target contributions before activation only after procedural manifest validation succeeds, and host-owned coordination seeds them into the `Dobby.Security` authorization registry without moving authorization policy/enforcement into PluginFramework;
- required plugin install/trust/activation audit events and redacted diagnostics markers are emitted by host-owned coordination from typed, non-disclosing PluginFramework outcomes and tested for install attempts, verified installs, rejected installs, signature failures, trust failures, skipped activation, activation rejection, and runtime faults; `Dobby.PluginFramework` has no Audit/Diagnostics references;
- plugin navigation is normalized/composed by `Dobby.Navigation`; approved `IPluginPageModel` values resolve only through `Dobby.Shell/PluginPages/PluginPageRenderRegistry.cs`; and the resulting Shell-owned presentation state renders through `Dobby.Host/MainWindow.xaml` `PluginPageHost` without plugin-provided WPF objects or a forbidden project dependency;
- plugin-owned background-operation progress reaches shell status/progress rendering only through the official contracts and host coordinator;
- no repo boundary rule is broken;
- HD-S8-004 namespace authority evidence cites the narrow, owner-specific `docs/08_CODING_STANDARDS.md` rows for every new public/plugin-visible or skeleton-critical host-internal Sprint 8 namespace/file placement, or proves relocation into a covered placement; PluginFramework operation-target files use contribution builder/validator naming and do not own authorization seeding or enforcement;
- canonical production/dev-QA trust source PEMs are linked and embedded only by `Dobby.Security.csproj` under the named internal logical resources; `DobbyPluginTrustProfile=Production` and `DobbyPluginTrustProfile=DevQa` each embed only their approved anchor; provider access has no Host-resource/runtime-filesystem fallback; and production output has no active dev/QA trust anchor;
- approved public trust PEM source inputs are either tracked through narrow `.gitignore` exceptions for the named canonical paths or injected into those paths at build time with evidence proving the resulting `Dobby.Security` embedded-resource identity;
- `toolchain-pin-gate.md` contains the passing `dobby.sprint8.toolchainPins.v1` human/test-readable representation with no `JsonSchema.Net` package ID/version/source/use drift;
- `architecture-baseline.md` contains the asserted `## Sprint 8 Implementation Evidence` markers and recorded verification outcomes;
- the package and trust gates are explicitly satisfied or still recorded as blockers.

## 17. Reviewer Focus Checklist

- Are package and trust gates called out clearly enough that no one can miss them?
- Does the plan keep plugin trust in `Dobby.Security` rather than scattering it through the host?
- Does `Dobby.Security.csproj` link the canonical Host/Trust source PEMs under the named internal logical resources, with `DobbyPluginTrustProfile=Production` and `DobbyPluginTrustProfile=DevQa` each embedding only its approved anchor and no runtime filesystem/Host-resource provider fallback?
- Does the activation path remain post-login only through `App.xaml.cs` and the authenticated runtime, without blocking shell first paint?
- Does session-owned cancellation stop activation on lock, logout, expiry, invalidation, and disposal without persistent status/audit and allow retry on the next login?
- Do compatibility tests prove exact pre-release rejection and `host-version-below-min` activation-rejection behavior?
- Do dependency tests prove `dependency-activation-failed` cascading while independent plugins continue?
- Do ALC tests prove distinct non-collectible contexts, host-shared pinning, plugin-private dependency isolation, and native resolution?
- Do trust-resource project-file/package rules prove production cannot include an active dev/QA trust anchor?
- Does the chosen trust resource strategy account for the current `*.pem` ignore rule through narrow public-path exceptions or build-time injection evidence?
- Do package/runtime tests prove AC-015 forbidden host implementation, Infrastructure, other-plugin references, reflection, and dynamic-load attempts are rejected?
- Do package tests prove AC-018/ADR-0045 duplicate/case-collision rejection, symlink rejection, and raw-container-metadata-independent fingerprinting?
- Do manifest tests prove ADR-0036 procedural validation fails closed for target namespace ownership, labels, duplicate target/id regressions, extension id uniqueness, missing parents, and cycles?
- Do audit/diagnostics tests prove host-owned coordination maps typed PluginFramework outcomes to required install, trust, activation, skipped, rejected, and runtime-fault events without raw secrets or exception text, while architecture tests keep `Dobby.PluginFramework` free of Audit/Diagnostics references?
- Are the files-to-create and files-to-modify lists consistent with the current codebase shape?
- Are any new package references explicitly gated?
- Is installed-state/cache ownership explicit, does HD-S8-003 evidence record schema/table or backing abstraction plus migration ownership, and do tests prove staging, durable transaction, metadata-cache rebuild/repair, and post-login recovery/cleanup through the approved backing strategy?
- Does HD-S8-004 namespace authority cite the narrow, owner-specific `docs/08_CODING_STANDARDS.md` rows for proposed new public/plugin-visible or skeleton-critical host-internal placements, with PluginFramework operation-target files limited to contribution builder/validator ownership?
- Is the ADR-0006 service registration bridge explicit, and do tests prove plugin services, extension points, lifetimes, and disposal work without `IServiceProvider` exposure?
- Does navigation normalization stay in `Dobby.Navigation` without a forbidden `Dobby.PluginFramework -> Dobby.Navigation` dependency?
- Does `PluginPageRenderRegistry` remain the only approved model-to-host-view mapping path, and does `MainWindow.xaml` `PluginPageHost` bind only to Shell-owned presentation state while rejecting plugin WPF/view identities?
- Does the plan keep plugin communication on public contracts and the event bus only?
- Does plugin background-operation reporting use only the official reporter/coordinator path and reach shell progress rendering?
- Does `toolchain-pin-gate.md` carry the Sprint 8 test-readable package-use evidence and does `PackageGovernanceTests` prove no drift?
- Does `Sprint8ReleaseEvidenceTests` name and assert the Sprint 8 section in `architecture-baseline.md`?
- Is the current sample manifest kept out of canonical signed-package fixture usage unless it is brought into schema alignment?
- Are dedicated `.dobbyplugin` package fixtures generated under test temp/output paths, or are committed fixture paths paired with narrow `.gitignore` exceptions?
- Are the out-of-scope items clearly excluded?

## 18. Final Execution Checklist

With the recorded authority decisions in force, stop only at the named implementation evidence gates:
Preflight evidence before item 1:
- record HD-S8-003 storage implementation evidence before installed-state/cache persistence work: approved first-consumer migration `0002_postlogin_bootstrap`, `PostLoginPluginPersistenceMigrations`, Host-owned state/cache adapters, `PluginInstallStagingArea`, durable state transaction boundary, cache rebuild/repair behavior, and post-login recovery/cleanup invariant tests; if evidence changes that baseline, patch sections 7, 8, 14, 16, and 18 first;
- cite and verify HD-S8-004 namespace authority before creating proposed new public/plugin-visible or skeleton-critical host-internal namespace placements: narrow, owner-specific `docs/08_CODING_STANDARDS.md` rows or relocation into covered placements; keep PluginFramework operation-target files on contribution builder/validator naming only.

1. add the `dobby.sprint8.toolchainPins.v1` human/test-readable `JsonSchema.Net` evidence representation and parity/no-drift test;
2. write the unit tests for package, trust, install-state/cache, compatibility, DI bridge, dependency typed-outcome behavior, ALC isolation, ADR-0036 procedural manifest validation, `PluginPageRenderRegistry`, and host-owned audit/diagnostics mapping;
3. implement the package reader and validator;
4. add AC-018/ADR-0045 package validation for duplicate/case-collision entries, symlink entries, and raw-container-metadata-independent package fingerprints;
5. add fail-closed ADR-0036 procedural manifest validation before registry seeding or activation;
6. implement exact ADR-0037 compatibility outcomes;
7. link approved canonical `src/Dobby.Host/Trust/*.pem` source inputs into `Dobby.Security.csproj` as `Dobby.Security.Trust.vendor-master-public-key.pem` and `Dobby.Security.Trust.dev-qa-public-key.pem`; make `PluginTrustPublicKeyProvider` resolve only the resource selected by `DobbyPluginTrustProfile`; record logical-name/path/hash/approval and profile inclusion/exclusion evidence; and, if committing public PEM source files, add narrow `.gitignore` exceptions before relying on them;
8. implement install-state and metadata-cache ownership through the HD-S8-003 approved backing strategy, using staging, durable state transaction, derived cache projection, and post-login recovery/cleanup invariants rather than a simple cross-filesystem/database atomicity claim;
9. implement the ADR-0006 service registration bridge and plugin-owned singleton disposal primitives;
10. add AC-015 host-boundary rejection for forbidden references, reflection, and dynamic-load attempts;
11. add login-gated activation through `App.xaml.cs`, bind it to session-end cancellation/retry semantics, and preserve shell first paint;
12. add dependency-activation-failed cascading and per-plugin ALC isolation;
13. build and validate plugin operation-target contributions in PluginFramework only after procedural manifest validation succeeds, then have host-owned coordination seed them into the `Dobby.Security` authorization registry;
14. after HD-S8-004 namespace authority is recorded, add `Dobby.Navigation`-owned navigation mapping, the approved Shell plugin-page render registry placement, Shell-owned active page presentation state, `Dobby.Host/MainWindow.xaml` `PluginPageHost`, and official event/page/background-operation hosting;
15. map typed, non-disclosing PluginFramework outcomes to host-owned required plugin install/trust/activation audit events and redacted diagnostics markers, keep `Dobby.PluginFramework` free of Audit/Diagnostics references, and leave session-end cancellation unaudited;
16. use dedicated valid/invalid package fixtures generated by `PluginPackageFixtureBuilder` into temp/output paths unless the sample manifest is explicitly brought into schema alignment; if committed `.dobbyplugin` fixtures are later chosen, list their paths and add narrow `.gitignore` exceptions first;
17. add integration tests for install, install-state/cache, compatibility, DI registration, activation, session-end cancellation/retry, dependency activation-failure cascading, ALC isolation, navigation/event/page/background-operation hosting through the Shell registry and concrete MainWindow surface, `Dobby.Security` trust-resource packaging/profile isolation, AC-015 boundary rejection, host-owned audit/diagnostics emission from typed PluginFramework outcomes, and fault isolation;
18. run the package-governance, architecture, boundary, and release-evidence tests;
19. append and verify the named `architecture-baseline.md` Sprint 8 evidence section; stop if any gate or boundary drifts.

## 19. Coverage And Confidence

Coverage view:
- package validation: covered by sections 5, 6, 7, 10, 11, 12, and 14;
- procedural manifest validation: covered by sections 2, 5, 6, 10, 12, 13, 14, 16, 17, and 18;
- install state and metadata cache: covered by sections 3, 5, 6, 7, 12, 13, 14, 16, and 18, with HD-S8-003 evidence required for schema/table or backing abstraction, migration ownership, staging, durable transaction, derived-cache rebuild/repair, and post-login recovery/cleanup invariants;
- trust verification and `Dobby.Security`-owned trust-resource packaging/profile isolation: covered by sections 1, 5, 6, 7, 8, 10, 11, 12, 13, 14, 16, 17, and 18;
- audit and diagnostics: covered by sections 2, 5, 6, 10, 12, 13, 14, 16, 17, and 18;
- compatibility and dependency ordering: covered by sections 4, 5, 6, 7, 10, 12, and 14;
- session-end activation cancellation and retry: covered by sections 3, 4, 5, 6, 8, 10, 12, 13, 14, 16, 17, and 18;
- per-plugin ALC isolation: covered by sections 3, 5, 6, 7, 12, 13, 14, 16, 17, and 18;
- plugin DI registration bridge: covered by sections 3, 5, 6, 7, 8, 12, 14, 16, and 18;
- activation runtime and login gating: covered by sections 1, 3, 4, 5, 6, 8, 12, 14, 16, and 18;
- background-operation reporting: covered by sections 1, 2, 3, 5, 6, 7, 8, 12, 13, 14, 16, 17, and 18;
- navigation ownership, Shell render registry, concrete MainWindow page-host surface, and Sprint 10 lifecycle boundary: covered by sections 1, 2, 5, 6, 7, 8, 12, 13, 14, 15, 16, 17, and 18;
- namespace/file placement authority: covered by sections 1, 7, 8, 12, 13, 14, 16, 17, and 18, with HD-S8-004 narrow owner-specific `docs/08_CODING_STANDARDS.md` rows required to be cited before exact new placements are created;
- Sprint 8 package-use evidence and no-drift verification: covered by sections 2, 8, 11, 12, 13, 14, 16, 17, and 18;
- plugin boundary and host isolation: covered by sections 5, 6, 7, 8, 9, 10, 12, 14, 15, 16, and 17;
- source traceability for ADR/spec-backed behavior: covered by sections 2, 6, 10, 12, 14, and 19;
- release evidence and verification: covered by sections 2, 14, 18, and 19.
- sample fixture boundary and generated `.dobbyplugin` fixture strategy: covered by sections 3, 5, 7, 8, 9, 13, 14, 15, 17, and 18.
- trust resource ignore-rule materialization: covered by sections 2, 5, 7, 8, 12, 13, 14, 16, 17, and 18.

Implementation assumption count: 11.

Confidence statement:
- high confidence in the architecture boundary map and ADR/spec source traceability for the named sources;
- high confidence in the required test shape after adding explicit compatibility, dependency-failure cascade, session-cancellation, ALC-isolation, trust-profile isolation, page-render-hosting, background-operation, package-governance, install-state/cache recovery, namespace-authority, and release-evidence coverage;
- medium confidence on the final package-validation implementation shape until the approved Sprint 8 `JsonSchema.Net` use has a passing `dobby.sprint8.toolchainPins.v1` evidence representation;
- medium confidence on installed-state/cache durable persistence until implementation records and verifies the HD-S8-003 schema/table or backing abstraction, migration ownership, staging, durable transaction, cache rebuild/repair, and post-login recovery/cleanup evidence;
- medium confidence on exact namespace/file placements until HD-S8-004 coding-standards authority rows are cited and verified during implementation;
- low confidence on trust-resource wiring until the approved public key bytes are supplied and the committed-public-PEM or injected-resource strategy is verified.
