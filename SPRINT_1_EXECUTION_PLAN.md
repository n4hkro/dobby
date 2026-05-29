# 1. Sprint Summary

- Sprint number and title: Sprint 1 - Toolchain and Architecture Baseline.
- Main goal: verify and harden the repository skeleton, build conventions, package-version governance, encoding expectations, and architecture dependency guardrails before any feature sprint crosses package or architecture decision gates.
- Expected deliverable: static architecture and toolchain baseline checks, skeleton-local release evidence, and explicit blocker records for any package pin, project boundary, encoding, or documentation/code conflict that cannot be resolved without a human decision.
- Why this sprint exists: later P1 implementation depends on stable project ownership, approved package pins, no accidental Branch Shared or P2 runtime surface, and enforceable boundaries between Core, Contracts, SDK, PluginFramework, Infrastructure, Host, Admin, and UI projects.
- Sprint readiness: partially blocked.
  - Implementation of read-only/static guardrails and evidence is ready.
  - Full package-bearing progress is blocked until a human-approved pin list is supplied or an explicit stop condition is recorded.
  - Some current code conflicts with documentation and must be recorded or resolved only with the documented ownership decisions.

## Planning Coverage and Confidence

- Coverage confidence is high for repo-local static guardrail and evidence work because the plan reads the root files, canonical Sprint 1 docs, ADRs, schemas, source files, test files, project files, and existing skeleton-local artifacts listed in Section 2.
- Scope confidence is high that Sprint 1 remains a baseline and guardrail sprint because feature implementation, Branch Shared runtime work, package additions, and later SDK/plugin/security remediation stay out of scope unless explicitly authorized.
- Closure confidence is conditional for package-bearing work, pin-approval automation, and the Infrastructure/Application print-boundary conflict until the human decisions listed in Section 12 are supplied or recorded as stop conditions.

# 2. Sources Read

Scope note: Sprint-1-required canonical inputs are the root `README.md`, current `global.json`, current `Directory.Packages.props`, docs `01`, `02`, `03`, `05`, `06`, `08`, `09`, doc `04` only for the initial `Dobby.Core/ErrorCodes` exact-equality baseline registry check, which also prevents unapproved Branch Shared identifiers, and the human-approved package pin list or documented stop condition. Other docs, specs, schemas, ADRs, code, and tests listed below were read as background or verification context only; they do not expand Sprint 1 beyond the master sprint plan or move full Result-policy implementation into Sprint 1.

## Repo-root files

- `README.md`: establishes the skeleton status, non-git bootstrap state, canonical initial commands, and documentation-first workflow.
- `SPRINT_IMPLEMENTATION_PLAN.md`: source of Sprint 1 title, scope, dependencies, acceptance notes, verification, and exit criteria.
- `.editorconfig`: defines repository text conventions, including UTF-8 charset, CRLF, final newlines, and analyzer/editor behavior.
- `.gitignore`: confirms generated build, test, package, publish, and artifact outputs that must remain out of source.
- `global.json`: pins .NET SDK `10.0.202` with `latestPatch` roll-forward; part of the Sprint 1 toolchain gate.
- `Directory.Build.props`: central compiler, analyzer, nullable, language version, warning, and deterministic build settings.
- `Directory.Packages.props`: central package management file and current package-version pin source.
- `Dobby.sln`: solution shape and project membership source for solution readiness checks.

## Product and architecture documentation

- `docs/01_PRODUCT_SCOPE.md`: defines initial P1 scope, offline-first host boundaries, Branch Shared documentation-only addendum, and excluded runtime areas.
- `docs/02_ARCHITECTURE_MAP.md`: defines canonical project ownership, dependency table, phase taxonomy, initial skeleton exclusions, engineering conventions, and route/message/plugin boundaries.
- `docs/03_TECH_STACK_AND_VERSIONS.md`: defines .NET/WPF stack, target framework split, central package management, security floors, and package approval gate.
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md`: defines `Result` versus exception policy and canonical error-code categories.
- `docs/05_FUNCTIONAL_REQUIREMENTS.md`: defines P1, P1-if-time, P2, and Branch Shared functional boundaries.
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md`: defines offline, privacy, security, performance, plugin isolation, accessibility, and UTF-8/no-BOM requirements.
- `docs/07_ACCEPTANCE_CRITERIA.md`: defines stage-tagged acceptance criteria and evidence expectations, including encoding and dependency scan criteria.
- `docs/08_CODING_STANDARDS.md`: defines naming, namespace, DI, MVVM, SQL, result/exception, encoding, infrastructure, and package-management rules.
- `docs/09_BUILD_RUN_AND_RELEASE.md`: defines required physical layout, initial P1 exclusions, canonical build/test commands, release evidence, package pin governance, and forbidden initial projects.

## Specs

- `docs/specs/README.md`: spec ownership index and warning that specs do not authorize code beyond accepted ADRs and phase scope.
- `docs/specs/session-lifecycle.md`: login-first lifecycle, post-login shell transition, and pre-login closed-world boundaries.
- `docs/specs/shell-ui.md`: shell/navigation/menu/tab expectations relevant to project and localization ownership.
- `docs/specs/user-management.md`: user/admin scope and local-first auth boundaries.
- `docs/specs/plugins-manager.md`: P1 plugin-manager and package-validation scope; no marketplace, repair, update, or dependency resolver.
- `docs/specs/sample-plugin.md`: sample plugin manifest, extension point, SDK, and packaging expectations.
- `docs/specs/license-import.md`: offline license import/activation scope and license file boundaries.
- `docs/specs/audit-and-diagnostics.md`: audit, diagnostics, export, and redaction expectations.
- `docs/specs/storage-and-data-protection.md`: provider-neutral storage baseline and explicit block on production DB-at-rest provider packages, entities, migrations, and storage abstraction naming without a human table.
- `docs/specs/admin-tool.md`: Admin CLI command/folder expectations and blocked option naming decisions.
- `docs/specs/postgres-deployment-ops.md`: Branch Shared Tier-B documentation-only PostgreSQL scope that must not enter Sprint 1 runtime.
- `docs/specs/backup-restore-migration.md`: later backup/restore scope, useful for out-of-scope boundaries.
- `docs/specs/data-retention.md`: later retention scope, useful for out-of-scope boundaries.
- `docs/specs/design-system-assets.md`: design-system ownership and asset expectations, not a Sprint 1 implementation target.
- `docs/specs/device-lifecycle.md`: device lifecycle scope, later functional boundary.
- `docs/specs/onboarding-and-provisioning.md`: onboarding scope, later functional boundary.
- `docs/specs/settings-and-about.md`: settings/about scope, later shell scope.
- `docs/specs/shared-data-tools.md`: shared data tooling scope, later or Branch Shared boundary.
- `docs/specs/work-sessions.md`: work-session lifecycle scope already partly represented in code and tests.

## Schemas

- `docs/schemas/dobby-plugin-manifest.schema.json`: canonical plugin manifest schema and required field list.
- `docs/schemas/dobby-plugin-license.schema.json`: canonical detached plugin license schema.
- `docs/schemas/branchshared.schema.json`: Branch Shared profile schema; confirms documentation-only status for initial skeleton.

## ADRs

- `docs/adr/README.md`: ADR index, status lifecycle, numbering gaps, governance rules, and resolved decision index.
- `docs/adr/ADR-0000-template.md`: ADR format only; no implementation scope.
- `docs/adr/ADR-0001-contracts-dependency-boundary.md`: Contracts dependency boundary and narrow Core allowance.
- `docs/adr/ADR-0003-plugin-storage-boundary-and-access-model.md`: plugin storage boundary and access model.
- `docs/adr/ADR-0004-capability-enforcement-model.md`: capability enforcement model.
- `docs/adr/ADR-0005-ui-ordering-model.md`: UI ordering model.
- `docs/adr/ADR-0006-plugin-di-registration.md`: plugin DI registration.
- `docs/adr/ADR-0007-plugin-load-context-strategy.md`: plugin load context strategy.
- `docs/adr/ADR-0008-deferred-plugin-activation.md`: deferred plugin activation.
- `docs/adr/ADR-0009-plugin-dependency-graph.md`: plugin dependency graph.
- `docs/adr/ADR-0010-plugin-communication-policy.md`: plugin communication policy and folded event visibility model.
- `docs/adr/ADR-0011-plugin-sandbox-enforcement-principle.md`: plugin sandbox enforcement principle.
- `docs/adr/ADR-0012-plugin-capability-boundary.md`: plugin capability boundary.
- `docs/adr/ADR-0013-routing-and-deep-link-model.md`: routing and deep-link model.
- `docs/adr/ADR-0015-background-operation-reporting.md`: background operation reporting.
- `docs/adr/ADR-0016-logging-strategy.md`: logging strategy.
- `docs/adr/ADR-0019-audit-vs-diagnostics-boundary.md`: audit versus diagnostics boundary.
- `docs/adr/ADR-0021-sdk-surface-area.md`: SDK project boundary and canonical `PluginBase` shape.
- `docs/adr/ADR-0023-admin-host-boundary.md`: Admin-host boundary.
- `docs/adr/ADR-0026-license-lifecycle.md`: superseded license lifecycle ADR.
- `docs/adr/ADR-0027-configuration-model.md`: configuration model.
- `docs/adr/ADR-0028-shell-composition-ordering-render.md`: shell composition, ordering, and rendering.
- `docs/adr/ADR-0029-correlation-id-ownership.md`: correlation ID ownership.
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md`: host persistence and plugin storage boundary.
- `docs/adr/ADR-0031-session-lifecycle-and-refresh.md`: session lifecycle and refresh.
- `docs/adr/ADR-0032-plugin-signature-trust-model.md`: plugin signature trust model.
- `docs/adr/ADR-0033-signing-key-ownership.md`: signing key ownership.
- `docs/adr/ADR-0035-tenant-customer-context-scope.md`: superseded tenant/customer context scope.
- `docs/adr/ADR-0036-plugin-manifest-schema.md`: plugin manifest schema.
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md`: plugin compatibility and versioning.
- `docs/adr/ADR-0038-local-first-authentication.md`: local-first authentication.
- `docs/adr/ADR-0039-owner-password-recovery.md`: owner password recovery via vendor-signed device-bound artifact.
- `docs/adr/ADR-0040-internal-host-adapters-boundary.md`: internal host adapter boundary for `Dobby.Infrastructure`.
- `docs/adr/ADR-0041-login-first-startup-policy.md`: login-first startup policy.
- `docs/adr/ADR-0042-release-gate-hardware-baseline.md`: release-gate hardware baseline.
- `docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`: initial UI culture and fallback chain.
- `docs/adr/ADR-0044-localization-resource-organization.md`: localization resource organization.
- `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md`: plugin package and license artifact boundary.
- `docs/adr/ADR-0046-ui-and-code-terminology-mapping.md`: UI and code terminology mapping.
- `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`: host installer and portable artifact policy.
- `docs/adr/ADR-0048-operation-based-authorization.md`: operation-based authorization and canonical verb taxonomy.
- `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`: hierarchical navigation contribution model.
- `docs/adr/ADR-0050-in-memory-work-sessions.md`: in-memory work sessions.
- `docs/adr/ADR-0051-license-lifecycle-v2.md`: license lifecycle v2.
- `docs/adr/ADR-0052-tenant-customer-context-v2.md`: tenant/customer context v2.
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md`: capability surface runtime mode.
- `docs/adr/ADR-0054-db-at-rest-protection-provider.md`: DB-at-rest protection provider.
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md`: protected storage scope and key wrapping.
- `docs/adr/ADR-0056-deployment-modes-and-storage-providers.md`: deployment modes and storage providers.
- `docs/adr/ADR-0057-mode-aware-authentication-boundary.md`: mode-aware authentication boundary.

ADR numbering gaps are intentional per `docs/adr/README.md`; that README is the gap/governance index, not the source for enumerating every ADR file above.

## Source code areas

- `src/Dobby.Core`: `Result`, `ErrorCode`, correlation IDs, host version parsing, and Core project metadata.
- `src/Dobby.Contracts`: background operation, event/messaging, licensing, ordering, plugin framework, plugin page, plugin storage, and policy contracts.
- `src/Dobby.SDK`: plugin developer SDK base types and storage helpers.
- `src/Dobby.PluginFramework`: manifest model, manifest validator, dependency sorter, plugin exception boundary, and project references.
- `src/Dobby.Security`: PBKDF2 password hashing, operation key parsing, authorization types, and security project metadata.
- `src/Dobby.Licensing`: plugin license runtime mapping and licensing project metadata.
- `src/Dobby.Configuration`: host options and validation.
- `src/Dobby.Localization`: host string catalog and Azerbaijani resources.
- `src/Dobby.Navigation`: route registry and UI ordering comparer.
- `src/Dobby.Messaging`: in-memory event bus.
- `src/Dobby.Application`: background operations and print target classification abstraction.
- `src/Dobby.Diagnostics`: diagnostic redaction.
- `src/Dobby.Audit`: in-memory audit trail.
- `src/Dobby.UI`: design tokens, shared UI resources, and project metadata.
- `src/Dobby.Shell`: login/shell placeholders, WPF view, shell view model, work-session registry, and project metadata.
- `src/Dobby.Infrastructure`: Windows print target classifier and project metadata.
- `src/Dobby.Admin`: Admin command host and project metadata.
- `src/Dobby.Host`: WPF app host, bootstrapper, startup plan, resources, app settings, and project metadata.
- `samples/Dobby.Sample.Plugin`: sample plugin project and sample plugin manifest.

## Tests

- `tests/Dobby.UnitTests/Core/ResultTests.cs`: Core result behavior.
- `tests/Dobby.UnitTests/Contracts/PluginFrameworkContractTests.cs`: plugin storage contracts and SDK base behavior.
- `tests/Dobby.UnitTests/Contracts/PluginStorageContractTests.cs`: plugin storage contract behavior.
- `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs`: manifest validator, dependency sort, and plugin exception boundary.
- `tests/Dobby.UnitTests/Security/SecurityTests.cs`: operation-key parsing and PBKDF2 behavior.
- `tests/Dobby.UnitTests/Configuration/ConfigurationTests.cs`: security floor validation.
- `tests/Dobby.UnitTests/Diagnostics/DiagnosticsAndAuditTests.cs`: diagnostic redaction and audit trail behavior.
- `tests/Dobby.UnitTests/Licensing/LicensingTests.cs`: license runtime mapper behavior.
- `tests/Dobby.UnitTests/Messaging/InMemoryEventBusTests.cs`: in-memory event bus behavior.
- `tests/Dobby.UnitTests/Navigation/NavigationTests.cs`: ordering and route fallback behavior.
- `tests/Dobby.UnitTests/Shell/WorkSessionRegistryTests.cs`: work-session registry behavior.
- `tests/Dobby.IntegrationTests/ProjectBoundaryTests.cs`: current broad skeleton boundary tests.
- `tests/Dobby.SmokeTests/ShellSmokeTests.cs`: shell navigation label smoke test and co-located Admin command option rejection smoke test.
- Test project files under `tests/*/*.csproj`: validate test package references, target frameworks, and WPF usage.

## Project, build, tool, installer, and artifact files

- `src/*/*.csproj`: current project reference graph, target frameworks, WPF flags, and package references.
- `tests/*/*.csproj`: current test target frameworks and package references.
- `samples/Dobby.Sample.Plugin/Dobby.Sample.Plugin.csproj`: sample plugin project boundaries.
- `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json`: sample manifest shape and schema alignment.
- `src/Dobby.Host/appsettings.json`: host runtime configuration baseline.
- `src/Dobby.Host/App.xaml`: WPF app resource merge and startup baseline.
- `src/Dobby.Host/MainWindow.xaml`: current host window placeholder.
- `src/Dobby.Host/Startup/`: host bootstrapper and startup plan.
- `src/Dobby.Host/Trust/`: host trust placeholder documentation.
- `tools/sign-plugin-package/README.md`: confirms the dev/QA sample package signing wrapper placeholder and command-name boundary.
- `tools/create-plugin-license/README.md`: confirms the detached sample license creation wrapper placeholder and command-name boundary.
- `tools/create-recovery-response/README.md`: confirms the Owner recovery response wrapper placeholder and command-name boundary.
- `installer/README.md`: confirms installer folder placeholder status.
- `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md`: existing release evidence for auth KDF selection.

# 3. Sprint Scope From Master Sprint Plan

Sprint 1 in `SPRINT_IMPLEMENTATION_PLAN.md` is named `Toolchain and Architecture Baseline`.

The sprint goal is to verify the existing solution structure, build conventions, and architecture guardrails without crossing package-version decision gates.

The assigned task groups are:

- Documentation Baseline:
  - Map documentation into P1-core, P1-if-time, P2/future, Branch Shared/Tier-B, and initial-skeleton exclusions.
  - Record that Branch Shared materials are documentation-only unless their accepted ADRs explicitly authorize runtime work.
- Solution Shape:
  - Verify projects and build files against `docs/09_BUILD_RUN_AND_RELEASE.md`.
  - Verify ownership and references against `docs/02_ARCHITECTURE_MAP.md`.
  - Confirm optional future or deferred projects are absent from the initial skeleton.
- Toolchain Gate:
  - Map `global.json`, `Directory.Packages.props`, and project package references to the human-approved pin list.
  - Stop before adding any package or version not already approved.
- Architecture Checks:
  - Define checks for plugin reference boundaries.
  - Define checks for SDK limits.
  - Define checks for Contracts dependencies.
  - Define checks for UTF-8/no-BOM text encoding.
  - Define checks for forbidden Dobby namespace patterns and forbidden initial project patterns.

Dependencies from earlier sprints:

- There are no earlier implementation sprints.
- The sprint depends on the current documentation baseline, especially `README.md`, `global.json`, `Directory.Packages.props`, and docs `01`, `02`, `03`, `05`, `06`, `08`, and `09`.
- The sprint depends on a human-approved package pin list. No separate approval record was found in the repository.

Later sprints depending on Sprint 1:

- Sprint 2 depends directly on Sprint 1 for Contracts, Core, SDK, and static guardrail readiness.
- Sprint 3 through Sprint 10 depend transitively on Sprint 1 boundaries and package/version governance.
- Sprint 11 depends on Sprint 1 evidence as part of release and packaging verification.

Sprint 1 owns no primary user-facing acceptance criteria. It supplies guardrails for later acceptance criteria and must make deferred or blocked scope visible.

Sprint 1 contributes skeleton-time static dependency scan guardrails for AC-043 by proving plugin projects reference only `Dobby.Contracts` and `Dobby.SDK`. Sprint 8 remains the primary AC-043 owner, and Sprint 11 remains responsible for release-evidence consolidation.

# 4. Current Codebase State

## Existing modules, projects, and packages

- The documented initial solution shape exists under `src/`, `tests/`, `samples/`, `tools/`, `installer/`, and `artifacts/`.
- `src/Dobby.Update`, `src/Dobby.BranchSetup`, and `src/Dobby.Infrastructure.Postgres` are absent, which aligns with initial P1 exclusions.
- `tools/verify-plugin-package` is absent. `docs/09_BUILD_RUN_AND_RELEASE.md` describes it as optional later tooling, so this is not a Sprint 1 blocker.
- The current solution uses central package management through `Directory.Packages.props`.
- The current SDK pin is .NET `10.0.202` in `global.json`; the installed local SDK was planner-observed by `dotnet --info`, and the implementation agent must re-verify it.
- No separate human-approved package pin list was found. This blocks full package-governance closure.

## Existing interfaces, classes, and services relevant to Sprint 1

- `src/Dobby.Core/Result.cs` implements `Result`, `Result<T>`, and `ErrorCode`.
- `src/Dobby.Core/CorrelationId.cs` implements correlation ID primitives.
- `src/Dobby.Core/Versioning/HostVersion.cs` implements host-version parsing.
- `src/Dobby.Contracts` contains background operation, event/messaging, licensing, ordering, plugin framework, plugin page, plugin storage, and policy contracts.
- `src/Dobby.SDK/PluginFramework/PluginBase.cs` and related SDK storage helpers exist.
- `src/Dobby.PluginFramework/Manifest` contains a plugin manifest model and validator.
- `src/Dobby.Security` contains operation-key parsing and PBKDF2 password hashing.
- `src/Dobby.Configuration` contains `DobbyOptions` and security-floor validation.
- `src/Dobby.Application` contains background operations and print target classification abstraction.
- `src/Dobby.Infrastructure` contains the Windows print target classifier implementation.
- `src/Dobby.Admin` contains the Admin command host.
- `src/Dobby.Host` contains the WPF app host, bootstrapper, startup plan, app settings, and shell placeholder window.
- `src/Dobby.Shell` contains the shell route host, shell view model, and work-session registry.

## Existing tests

- Unit tests already cover Core result/version behavior, plugin contracts, plugin framework basics, security, configuration, diagnostics, audit, licensing, messaging, navigation, and work sessions.
- Integration tests already cover some project-boundary checks, central package management basics, deferred project absence, sample plugin references, and KDF evidence presence.
- Smoke tests already cover shell navigation labels and Admin command option rejection.
- Sprint 1 should split and strengthen the broad integration boundary checks into explicit architecture, package governance, encoding, plugin, SDK, and Contracts guardrails.

## Existing configuration

- `Directory.Build.props` already enforces nullable, implicit usings, language version `14.0`, warning-as-error, latest analysis level, code style in build, deterministic builds, and CI build flags.
- `Directory.Packages.props` already enables central package management and contains exact package versions.
- `.editorconfig` requests UTF-8, CRLF, final newline, and trim trailing whitespace.
- `.gitignore` excludes expected build/test/publish outputs.
- `global.json` pins the SDK.

## Existing gaps

- There is no repository-local approved package pin list separate from `Directory.Packages.props`.
- Architecture boundary checks are present but not yet comprehensive enough for all Sprint 1 requirements.
- Encoding checks are not comprehensive and current solution-file encoding conflicts with the documented no-BOM rule. A planner-observed byte-level read of `Dobby.sln` currently reports leading bytes `239 187 191`, the UTF-8 BOM signature; the implementation agent must re-verify and record byte evidence in the `Dobby.sln Encoding Evidence` subsection of `artifacts/release-evidence/skeleton-local/architecture-baseline.md` before removing the BOM if still present, recording absence-only evidence if no BOM is found, or recording an approved exception. Because the no-BOM rule already applies to repo-owned text files, `Dobby.sln` BOM removal is the default Sprint 1 encoding baseline fix when implementation-time byte verification still finds the BOM unless a human explicitly approves a solution-file exception.
- Plugin manifest code and sample manifest do not fully match the canonical schema and ADR decisions.
- SDK `PluginBase` behavior conflicts with the canonical ADR text.
- Operation-key verbs do not fully match the documented taxonomy and schema.
- Infrastructure currently references Application, which conflicts with the architecture dependency table and ADR-0040. This remains an unresolved blocker unless a human-authorized ADR or architecture-map resolution changes the print-adapter boundary. `Dobby.Infrastructure` also currently lacks the allowed `Dobby.Contracts` reference from the architecture dependency table; record that absence only as observed current graph evidence, not as a mismatch or required fix. `Dobby.SDK` currently lacks the allowed `Dobby.Localization` reference from the architecture dependency table and ADR-0021; record that absence only as observed current graph evidence, not as a mismatch, completeness proof, or required Sprint 1 fix.

## Mismatches between code and documentation

- `Dobby.Infrastructure` references `Dobby.Application`, but `docs/02_ARCHITECTURE_MAP.md` and ADR-0040 limit Infrastructure dependencies to Core and Contracts. ADR-0053 and the architecture map place `IPrintTargetClassifier`, `PrintTargetDescriptor`, and `PrintTargetClass` under `Dobby.Application.Printing` while keeping the physical print/OS adapter implementation under `Dobby.Infrastructure.Printing`, creating an ADR/architecture-map conflict that needs a human-authorized amendment or ownership decision before code movement.
- `src/Dobby.Application/Printing/PrintTargetClassifier.cs` defines `PrintTargetDescriptor` and `PrintTargetClass` with a shape that differs from ADR-0053. This is recorded as deferred drift and must not be reshaped in Sprint 1 unless the human explicitly expands the sprint from baseline guardrails to print contract remediation.
- `src/Dobby.SDK/PluginFramework/PluginBase.cs` defines `RegisterServices` as `virtual`, and an existing unit test expects the current default behavior. ADR-0021 says `RegisterServices` is abstract and mandatory.
- `src/Dobby.Security/Authorization/OperationKey.cs` lacks `configure` and `grant` verbs, while ADR-0048 and `docs/schemas/dobby-plugin-manifest.schema.json` include them.
- `src/Dobby.PluginFramework/Manifest/PluginManifest.cs` does not model all canonical manifest fields required by `docs/schemas/dobby-plugin-manifest.schema.json` and ADR-0036, including `vendorId`, `entryAssembly`, `displayName`, `description`, `files`, `signature`, and schema-required `capabilities[].name`.
- `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json` does not conform to the canonical schema and sample-plugin spec. It is missing required fields `vendorId`, `entryAssembly`, `displayName`, `description`, `files`, and schema-required `capabilities[].name` from `docs/schemas/dobby-plugin-manifest.schema.json`, and does not include the expected top-toolbar and sidebar extension-point examples.
- `Dobby.sln` currently has a planner-observed UTF-8 BOM (`239 187 191` / `EF BB BF`). Documentation and coding standards require UTF-8 without BOM for repository text files unless an explicit exception is approved, and implementation must re-verify the leading bytes before the baseline removal, absence-only evidence path, or approved exception. Unless a human explicitly approves a solution-file exception, remove the BOM as an encoding-only baseline fix only if implementation-time byte verification still finds it; if no BOM is found, do not modify `Dobby.sln` and record absence-only byte evidence in the `Dobby.sln Encoding Evidence` subsection of `artifacts/release-evidence/skeleton-local/architecture-baseline.md`.

# 5. Documentation and ADR Alignment

## Documentation mapping

- Product scope: Sprint 1 supports the P1 initial skeleton by keeping Branch Shared, P2, and future features outside runtime implementation.
- Architecture map: Sprint 1 directly enforces project ownership, dependency direction, and forbidden initial projects.
- Tech stack and versions: Sprint 1 verifies the SDK and package pin state and records stop conditions for unapproved versions.
- Result/error policy: Sprint 1 confirms Core result primitives exist and that unapproved Branch Shared `ErrorCode` identifiers remain deferred through the documented exact-equality baseline registry check.
- Functional and non-functional requirements: Sprint 1 supports offline-first, no telemetry, plugin isolation, UTF-8/no-BOM, and phase boundary requirements through static checks.
- Coding standards: Sprint 1 enforces naming, namespace, project reference, package management, and encoding guardrails.
- Build/run/release documentation: Sprint 1 verifies solution shape, commands, artifacts, and release evidence locations.
- Schemas and specs: Sprint 1 uses schemas/specs as alignment inputs and records current drift without implementing later feature behavior.

## ADR classification

| ADR | Sprint 1 classification | Reason |
| --- | --- | --- |
| ADR-0000 | No action | Template only. |
| ADR-0001 | No action | Drives Contracts dependency checks. |
| ADR-0003 | No action | Plugin storage boundary remains later storage/plugin work; no Sprint 1 ADR update. |
| ADR-0004 | No action | Capability enforcement remains later authorization/plugin work; no Sprint 1 ADR update. |
| ADR-0005 | No action | UI ordering remains later shell/plugin work; no Sprint 1 ADR update. |
| ADR-0006 | No action | Plugin DI registration remains later plugin runtime work; no Sprint 1 ADR update. |
| ADR-0007 | No action | Plugin load context remains later plugin runtime work; no Sprint 1 ADR update. |
| ADR-0008 | No action | Deferred activation informs login/plugin boundary checks; no Sprint 1 ADR update. |
| ADR-0009 | No action | Plugin dependency graph remains later plugin runtime work; no Sprint 1 ADR update. |
| ADR-0010 | No action | Plugin communication and folded event visibility model inform boundaries; no Sprint 1 ADR update. |
| ADR-0011 | No action | Sandbox principle remains later runtime enforcement work; no Sprint 1 ADR update. |
| ADR-0012 | No action | Capability boundary informs plugin/security scope; no Sprint 1 ADR update. |
| ADR-0013 | No action | Routing and deep-link model informs Contracts/navigation boundaries; no Sprint 1 ADR update. |
| ADR-0015 | No action | Background operation reporting remains later Application/plugin work; no Sprint 1 ADR update. |
| ADR-0016 | No action | Logging strategy remains later diagnostics work; no Sprint 1 ADR update. |
| ADR-0019 | No action | Audit versus diagnostics boundary informs scope; no Sprint 1 ADR update. |
| ADR-0021 | No action | ADR-0021 is explicit; current `PluginBase.RegisterServices` `virtual` behavior and the existing unit-test expectation are deferred SDK drift for the later SDK implementation sprint unless a human explicitly requests ADR amendment or Sprint 1 scope expansion. |
| ADR-0023 | No action | Admin-host boundary informs project checks; no Sprint 1 ADR update. |
| ADR-0026 | No action | Superseded by ADR-0051; do not use as implementation basis. |
| ADR-0027 | No action | Configuration model informs existing config boundaries; no Sprint 1 ADR update. |
| ADR-0028 | No action | Shell composition remains later shell work; no Sprint 1 ADR update. |
| ADR-0029 | No action | Correlation ID ownership is already represented in Core; no Sprint 1 ADR update. |
| ADR-0030 | No Sprint 1 action | Host persistence and plugin storage boundary is accepted; storage abstraction materialization remains outside Sprint 1 and blocked by docs/spec human decisions. |
| ADR-0031 | No action | Session lifecycle remains later auth/session work; no Sprint 1 ADR update. |
| ADR-0032 | No action | Plugin signature trust model remains later plugin package work; no Sprint 1 ADR update. |
| ADR-0033 | No action | Signing key ownership remains later trust/tooling work; no Sprint 1 ADR update. |
| ADR-0035 | No action | Superseded by ADR-0052; do not use as implementation basis. |
| ADR-0036 | No action | ADR and schema are explicit; current manifest code/sample drift belongs to a later implementation sprint unless a human chooses to relax the canonical schema. |
| ADR-0037 | No action | Plugin compatibility/versioning remains later plugin runtime work; no Sprint 1 ADR update. |
| ADR-0038 | No action | Existing KDF evidence exists; Sprint 1 should only preserve evidence and checks. |
| ADR-0039 | No action | Owner password recovery remains later auth/Admin work; no Sprint 1 ADR update. |
| ADR-0040 | Human decision required | Current Infrastructure to Application reference conflicts with the dependency table; ADR-0053 and the architecture map also place print classifier abstractions in Application while Infrastructure owns the physical adapter, so resolution requires human-authorized ADR/architecture-map action or ownership change. |
| ADR-0041 | No action | Login-first boundary remains relevant. |
| ADR-0042 | No action | Release-gate hardware baseline remains release verification context; no Sprint 1 ADR update. |
| ADR-0043 | No action | Culture/localization baseline remains relevant. |
| ADR-0044 | No action | String resource governance remains relevant. |
| ADR-0045 | No action | Plugin package/license artifact boundary remains later plugin package work; it is not the package pin governance source. |
| ADR-0046 | No action | UI/code terminology mapping remains later UI/localization context; no Sprint 1 ADR update. |
| ADR-0047 | No action | Host installer and portable artifact policy remain later release work; no Sprint 1 ADR update. |
| ADR-0048 | No action | ADR verb taxonomy is explicit; current code drift for `configure` and `grant` belongs to a later implementation sprint unless a human chooses to relax the canonical taxonomy. |
| ADR-0049 | No action | Hierarchical navigation contribution model remains later shell/plugin work; no Sprint 1 ADR update. |
| ADR-0050 | No action | In-memory work sessions are already represented by code/tests; no Sprint 1 ADR update. |
| ADR-0051 | No action | License lifecycle v2 remains later licensing work; no Sprint 1 ADR update. |
| ADR-0052 | No action | Tenant/customer context v2 remains later licensing/deployment context; no Sprint 1 ADR update. |
| ADR-0053 | Human decision required for documentation reconciliation; no Sprint 1 code action | Capability surface runtime mode remains later runtime-mode work, but its print classifier placement conflicts with ADR-0040 and the architecture map dependency table until a human-authorized ADR/architecture-map action or ownership change resolves the boundary. |
| ADR-0054 | No action | Confirms storage provider materialization is outside Sprint 1 unless separately approved. |
| ADR-0055 | No action | Protected storage scope remains later storage/security work; no Sprint 1 ADR update. |
| ADR-0056 | No action | Deployment modes and storage providers confirm Branch Shared runtime is outside Sprint 1. |
| ADR-0057 | No action | Mode-aware authentication boundary confirms Branch Shared auth/setup work is outside Sprint 1. |

New ADR or explicit documentation amendment may be needed only if a human chooses to change the architecture dependency table, resolve the ADR-0040/ADR-0053 print-classifier boundary conflict, accept a solution-file BOM exception, or intentionally relax the plugin manifest, operation verb taxonomy, or SDK decisions.

No ADR should be updated or closed during Sprint 1 unless the human explicitly requests ADR work after implementation evidence is reviewed.

# 6. Files to Create

All new `tests/Dobby.IntegrationTests/Architecture/*.cs` files stay inside the existing `tests/Dobby.IntegrationTests` project. They inherit the current integration-test target framework and `UseWPF` settings; Sprint 1 does not create a new architecture test project or modify test project TFM/tooling flags.

These architecture tests must remain static guardrails based on BCL file, text, and XML scanning. They must not add new package references or new `ProjectReference` entries to production projects. Because they are intentionally co-located in the existing integration test project, a compile failure in the existing referenced project graph may still prevent the static guardrail signal from running; Sprint 1 accepts that coupling rather than creating a new test project.

## `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`

- Purpose: enforce the project reference graph against `docs/02_ARCHITECTURE_MAP.md`, ADR dependency boundaries, Dobby-owned namespace guardrails, and forbidden initial skeleton surfaces.
- Why it belongs in this sprint: Sprint 1 explicitly owns architecture guardrail checks.
- Documentation basis: `docs/02_ARCHITECTURE_MAP.md`, `docs/04_RESULT_VS_EXCEPTION_POLICY.md`, `docs/08_CODING_STANDARDS.md` including its canonical namespace placement table, `docs/09_BUILD_RUN_AND_RELEASE.md`, ADR-0001, ADR-0040.
- Main types/classes/functions expected: integration test class for source project reference scanning, forbidden project checks, Dobby namespace-pattern scanning, and initial `Dobby.Core` error-code registry scanning.
- Tests required: the test file is the verification artifact; current project references must be a subset of the `docs/02_ARCHITECTURE_MAP.md` Cədvəl 2 allowed references, not an exact-list equality check. If the current `Dobby.Infrastructure` to `Dobby.Application` reference remains unresolved, the test may temporarily allow only that exact edge through a single named unresolved blocker ID `SPRINT1-INFRA-APP-PRINT-BOUNDARY`, tied to the `artifacts/release-evidence/skeleton-local/architecture-baseline.md` evidence entry. This is not a human-approved exception, must not be encoded as a normal Sprint 1 failing assertion, remains subject to the human print-boundary decision, must carry a Sprint 2 re-confirmation/sunset gate, and any additional unauthorized reference must fail. The test allowance must be exact-edge-only and its failure message must name the blocker ID and evidence path so renewal, removal, or expansion cannot be silent. It should also verify forbidden initial projects remain absent, fail with a clear project/path/reference message, and cite Cədvəl 2 by project-name row, not only a general ADR reference. It must verify the public static `ErrorCode` field names in `src/Dobby.Core/Result.cs` by exact equality against the documented `Dobby.Core/ErrorCodes` baseline registry in `docs/04_RESULT_VS_EXCEPTION_POLICY.md` lines 209-237; no separate `ErrorCodes` namespace or file exists yet. Any new `ErrorCode` field, Branch Shared or otherwise, fails this equality check until a future approved registry update changes the documented baseline. It must also scan declared Dobby-owned namespaces for dumping-ground namespace segments `Common`, `Utils`, `Helpers`, and `Extensions` per `docs/08_CODING_STANDARDS.md`; scope this to Dobby-owned namespace declarations and folder ownership, not arbitrary text, third-party namespaces, package names, canonical namespace-table placements such as `Dobby.Core.Versioning`, or `using Microsoft.Extensions.*`.
- Dependencies: BCL XML/file/text APIs and xUnit only; no new packages.

## `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`

- Purpose: enforce central package management, absence of inline package versions, deterministic package scope/stance guardrails, absence of unapproved new package references, and the approved-pin stop condition.
- Why it belongs in this sprint: Sprint 1 owns the toolchain gate and package approval boundary.
- Documentation basis: `docs/03_TECH_STACK_AND_VERSIONS.md` P1 Toolchain / Package Pin Gate and `docs/09_BUILD_RUN_AND_RELEASE.md` root file governance.
- Main types/classes/functions expected: integration test class for `Directory.Packages.props`, project package references, deterministic package scope/stance rules from `docs/03_TECH_STACK_AND_VERSIONS.md`, and any human-approved test-readable pin approval or stop-condition evidence format.
- Tests required: tests must not silently accept current pins as approved, and must not make a committed red suite the normal Sprint 1 outcome. They always enforce central package management, absence of inline package versions, Tier 1 test-only package isolation to `tests/**` projects, absence of the Tier 0 BCL/runtime-only package names `System.Text.Json`, `System.IO.Compression`, `System.IO.Compression.ZipFile`, and `System.Threading.Channels` in `.csproj` `PackageReference Include` or `Directory.Packages.props` `PackageVersion Include` elements, and absence of Tier R rejected/deferred packages unless an explicit human-approved opt-in record exists. Tier R means `Commercial-license MediatR line (LuckyPennySoftware)`, `EPPlus v5+`, `FluentAssertions v8+`, `Syncfusion`, `DevExpress`, `Telerik`, `Moq`, `AutoMapper commercial line`, encrypted `SQLitePCLRaw.bundle_*` packages, `Microsoft.Extensions.Resilience`, `Microsoft.Extensions.Http`, and `Microsoft.Extensions.Http.Resilience`. Tier 1 test-only means the rows for `xunit`, `xunit.runner.visualstudio`, `Shouldly`, `NSubstitute`, `Verify.Xunit`, `Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.NET.Test.Sdk`, and `coverlet.collector`; production Tier 1 rows are not restricted to `tests/**`. They may enforce approved-pin or stop-condition evidence only after the human approves the on-disk representation that the test reads.
- Dependencies: BCL XML/file APIs and xUnit only; no new packages.

## `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs`

- Purpose: scan repository text files that are in scope for UTF-8/no-BOM governance and fail on BOM or invalid text encoding unless explicitly excluded.
- Why it belongs in this sprint: Sprint 1 architecture checks include UTF-8/no-BOM rules.
- Documentation basis: the `docs/08_CODING_STANDARDS.md` "Review checklist additions" UTF-8 without BOM rule is the primary basis for broad repo-owned text-file scope; `.editorconfig` supports the UTF-8 charset/editor behavior only. `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` and AC-047 in `docs/07_ACCEPTANCE_CRITERIA.md` support their narrower host-owned and docs/source/resource subsets.
- Main types/classes/functions expected: integration test class with an explicit doc-backed repo-owned text-file allowlist that separates extension entries from exact filename entries, plus generated/binary-output exclusions. `docs/schemas/**/*.json` is in scope; text files under `docs/_perf/` are not blanket-excluded if their extension is otherwise in scope.
- Tests required: the scan should initially expose the currently observed `Dobby.sln` BOM conflict until that file is converted by the default Sprint 1 encoding baseline fix or a human-approved exception is recorded; the implementation agent must re-verify the leading bytes before taking either path. If no explicit exception is approved and a BOM is still present, remove the BOM and record the pre/post evidence path as the `Dobby.sln Encoding Evidence` subsection of `artifacts/release-evidence/skeleton-local/architecture-baseline.md`. If no BOM is found at implementation time, do not modify `Dobby.sln`; record absence-only byte evidence in that same subsection.
- Dependencies: BCL file APIs and xUnit only; no new packages.

## `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`

- Purpose: enforce plugin, SDK, Contracts, and sample-plugin project reference limits.
- Why it belongs in this sprint: Sprint 1 requires plugin reference boundary, SDK limit, and Contracts dependency checks.
- Documentation basis: `docs/02_ARCHITECTURE_MAP.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`, ADR-0001, ADR-0021, ADR-0036, ADR-0048.
- Main types/classes/functions expected: integration test class for Contracts, SDK, PluginFramework, and sample plugin reference constraints.
- Tests required: tests should enforce per-project allowed reference lists: Contracts project references stay limited to `Dobby.Core`; ADR-0001 type-level narrow Core surface enforcement is deferred to Sprint 2, which owns "Contracts reference only allowed Core/BCL surfaces" and the static dependency check for Contracts and SDK references in `SPRINT_IMPLEMENTATION_PLAN.md`. SDK project references are checked as a subset of Core, Contracts, and Localization per ADR-0021, not exact equality; PluginFramework is compared against the full `docs/02_ARCHITECTURE_MAP.md` Cədvəl 2 allowed set of Core, Contracts, Security, Licensing, Configuration, and Messaging; the migrated sample plugin assertion remains exact-equality and must require exactly `Dobby.Contracts` and `Dobby.SDK` project references. This sample-plugin reference assertion is Sprint 1's concrete AC-043 contribution artifact; Sprint 8 remains the primary AC-043 owner. WPF, Host, Infrastructure, Security, or implementation leakage into SDK/Contracts/sample plugin must be explicit failures.
- Dependencies: BCL XML/file APIs and xUnit only; no new packages.

## `tests/Dobby.IntegrationTests/Architecture/ReleaseEvidenceTests.cs`

- Purpose: ensure the two new Sprint 1 evidence files are not silently skipped.
- Why it belongs in this sprint: Sprint 1 exit criteria require architecture and toolchain evidence, and this minimal check keeps the evidence files from being omitted while avoiding brittle content assertions.
- Documentation basis: `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`, and the Implementation Determinism Addendum in Section 12.
- Main types/classes/functions expected: integration test class with file existence checks and one required canonical heading check per evidence file.
- Tests required: `artifacts/release-evidence/skeleton-local/architecture-baseline.md` must exist and contain `## Static Guardrail Status`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` must exist and contain `## Package Pin Approval Status`. The tests must not assert detailed evidence content, command output, blocker wording, or complete heading order.
- Dependencies: BCL file/text APIs and xUnit only; no new packages.

## `artifacts/release-evidence/skeleton-local/architecture-baseline.md`

- Purpose: record Sprint 1 architecture baseline evidence, including current project map, forbidden project absence, known doc/code conflicts, and deferred scope boundaries.
- Why it belongs in this sprint: Sprint 1 exit criteria require skeleton verification and recorded stop conditions.
- Documentation basis: `docs/09_BUILD_RUN_AND_RELEASE.md`, `docs/07_ACCEPTANCE_CRITERIA.md`, `SPRINT_IMPLEMENTATION_PLAN.md`.
- Main types/classes/functions expected: not applicable; Markdown evidence only.
- Required content: record the Documentation Baseline phase-classification map for P1-core, P1-if-time, P2/future, Branch Shared/Tier-B, and initial-skeleton exclusions; record the current project map, forbidden project absence, static-check coverage, static guardrail run status, and known documentation/code conflicts. The required headings are `## Sources`, `## Static Guardrail Status`, `## Known Blockers`, `## Dobby.sln Encoding Evidence`, `## Deferred Drift`, and `## Sprint 2 Reconfirmation`. Static guardrail run status must say whether the focused static guardrail checks completed, failed, or were blocked by upstream compile errors in the existing integration-test project graph. If the current `Dobby.Infrastructure` to `Dobby.Application` reference is not fixed by a human-approved ownership decision, record it as temporary blocker `SPRINT1-INFRA-APP-PRINT-BOUNDARY`, an evidence-bound unresolved exact-edge blocker, not as an approved exception, with the three-option Sprint 2 re-confirmation/sunset statement required by the Section 12 Implementation Determinism Addendum. Record architecture, encoding, and documentation/code blockers only in this file's `## Known Blockers` section. Record the currently absent-but-allowed `Dobby.Infrastructure` to `Dobby.Contracts` reference only as observed current graph evidence, not as a mismatch or required fix. Record the currently absent-but-allowed `Dobby.SDK` to `Dobby.Localization` reference only as observed current graph evidence, not as a mismatch, SDK completeness proof, or required Sprint 1 fix. Record `Dobby.sln` byte-level evidence in the required `## Dobby.sln Encoding Evidence` section of this file: pre/post bytes for the default BOM removal if a BOM is present, absence-only leading-byte evidence if no BOM is found, or the captured BOM plus approved exception if a human grants a solution-file exception.
- Tests required: `ReleaseEvidenceTests.cs` verifies existence and the required `## Static Guardrail Status` heading only. Manual release-evidence review verifies the file matches the implemented checks and headings without turning detailed Markdown wording into brittle test assertions.
- Dependencies: no code dependencies.

## `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`

- Purpose: record SDK/package pin review results, approval-source status, and any blocked package/version decisions.
- Why it belongs in this sprint: Sprint 1 cannot pass the toolchain gate without either approved pins or an explicit stop condition.
- Documentation basis: `docs/03_TECH_STACK_AND_VERSIONS.md` P1 Toolchain / Package Pin Gate and `docs/09_BUILD_RUN_AND_RELEASE.md` root file governance.
- Main types/classes/functions expected: not applicable; Markdown evidence only unless a human later approves a test-readable section or companion artifact.
- Required content: record whether an approved pin list exists, the source path if it exists, the minimum pin-list fields from `docs/03_TECH_STACK_AND_VERSIONS.md`, and the explicit stop condition when no approved source is available. The required headings are `## Sources`, `## Package Pin Approval Status`, and `## Known Blockers`. This file's `## Known Blockers` section owns only package/toolchain blockers and must include the cross-reference sentence `Architecture blockers are tracked in architecture-baseline.md`; architecture, encoding, and documentation/code blockers are not duplicated here.
- Tests required: `ReleaseEvidenceTests.cs` verifies existence and the required `## Package Pin Approval Status` heading only. Package governance tests verify the pin state or stop condition only if a human-approved test-readable representation exists. Until then, manual release-evidence review verifies this Markdown file records the approval state without treating missing approval as a committed failing test.
- Dependencies: no code dependencies.

# 7. Files to Modify

## `tests/Dobby.IntegrationTests/ProjectBoundaryTests.cs`

- Current role: broad integration boundary test covering startup plan, package centralization, deferred projects, sample plugin references, and KDF evidence.
- Required change: split ownership deterministically so each assertion has one owner after Sprint 1:
  - `Startup_plan_allows_only_closed_pre_login_work`: keep in `ProjectBoundaryTests.cs`.
  - `Csproj_files_use_central_package_versions_only`: move to `PackageGovernanceTests.cs` and remove the duplicate from `ProjectBoundaryTests.cs`.
  - `Deferred_branch_shared_runtime_projects_are_absent`: move to `ArchitectureDependencyBoundaryTests.cs` and remove the duplicate from `ProjectBoundaryTests.cs`.
  - `Sample_plugin_references_only_contracts_and_sdk`: move to `PluginAndSdkBoundaryTests.cs` and remove the duplicate from `ProjectBoundaryTests.cs`.
  - `Skeleton_kdf_evidence_is_present`: keep in `ProjectBoundaryTests.cs`.
- Guardrail: do not weaken the `Skeleton_kdf_evidence_is_present` evidence path (`artifacts/release-evidence/skeleton-local/auth-kdf-selection.md`) or its `PBKDF2-HMAC-SHA256` assertion during this split unless a later authorized AC-051-owning sprint changes the evidence contract.
- Reason: Sprint 1 needs explicit, maintainable checks for each guardrail category.
- Documentation basis: `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/02_ARCHITECTURE_MAP.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`.
- Risks: duplicate assertions can create noisy failures; removing existing coverage can weaken current guardrails.
- Tests required: all implemented integration tests must still pass; any Sprint 1 blocker must be represented as scoped-out or evidence-recorded work rather than as a normal committed failing assertion.

## `Dobby.sln`

- Current role: solution membership and build entry point.
- Required change: byte-verify the leading bytes. If the UTF-8 BOM is present, remove it as an encoding-only Sprint 1 baseline fix unless a human explicitly approves a documented solution-file exception. If no BOM is found, do not modify `Dobby.sln`. Record the pre-fix byte evidence and the post-fix absence of the BOM, the absence-only byte evidence, or the approved exception evidence in the `Dobby.sln Encoding Evidence` subsection of `artifacts/release-evidence/skeleton-local/architecture-baseline.md`.
- Reason: current file encoding conflicts with repository UTF-8/no-BOM policy.
- Documentation basis: the `docs/08_CODING_STANDARDS.md` "Review checklist additions" UTF-8 without BOM rule is primary for repository text-file encoding. `.editorconfig` supports the UTF-8 charset/editor behavior only. `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` and AC-047 provide narrower supporting context but do not independently define solution-file scope.
- Risks: Visual Studio may rewrite solution files; any exception must be explicit to prevent recurring false positives.
- Tests required: encoding boundary test.

## `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj`

- Current role: Infrastructure project metadata and project references.
- Required change: human-decision gated. Remove the `Dobby.Application` reference only if the print-classifier ownership decision keeps ADR-0040 and the architecture dependency table unchanged.
- Reason: current reference conflicts with the documented Infrastructure boundary.
- Documentation basis: `docs/02_ARCHITECTURE_MAP.md`, ADR-0040, `docs/08_CODING_STANDARDS.md`.
- Risks: removing the reference requires moving or redefining the print classifier abstraction; doing so without a human decision may violate the coding standards print namespace guidance.
- Tests required: architecture dependency boundary test and existing unit/build checks.

## `src/Dobby.Application/Printing/PrintTargetClassifier.cs`

- Current role: host-internal print target classifier abstraction and classifier implementation helper.
- Required change: human-decision gated. Adjust ownership only if the Infrastructure/Application dependency conflict is resolved by moving or reclassifying the abstraction.
- Reason: the current abstraction location drives the Infrastructure to Application reference.
- Documentation basis: `docs/08_CODING_STANDARDS.md`, ADR-0040, ADR-0053.
- Risks: moving the abstraction may affect namespace contracts and later print/file output implementation.
- Tests required: architecture dependency boundary tests and any existing Application tests that use the classifier.

## `src/Dobby.Infrastructure/Printing/WindowsPrintTargetClassifier.cs`

- Current role: physical Windows print target classifier adapter.
- Required change: human-decision gated. Update references only after the print abstraction ownership is clarified.
- Reason: it currently depends on the Application abstraction.
- Documentation basis: `docs/08_CODING_STANDARDS.md`, ADR-0040, ADR-0053.
- Risks: changing it prematurely may create undocumented adapter contracts.
- Tests required: architecture dependency boundary tests and build verification.

## `Directory.Packages.props`

- Current role: central package-version pin source.
- Required change: no change unless a human-approved pin list says an existing pin is wrong.
- Reason: Sprint 1 must stop before adding or changing unapproved package versions.
- Documentation basis: `docs/03_TECH_STACK_AND_VERSIONS.md` P1 Toolchain / Package Pin Gate and `docs/09_BUILD_RUN_AND_RELEASE.md` root file governance.
- Risks: changing versions without approval violates the sprint gate.
- Tests required: package governance tests and full build/test after any approved version change.

## `global.json`

- Current role: .NET SDK pin source.
- Required change: no change unless a human-approved toolchain decision changes the SDK pin.
- Reason: SDK changes are package/toolchain gate decisions.
- Documentation basis: `docs/03_TECH_STACK_AND_VERSIONS.md` P1 Toolchain / Package Pin Gate and `docs/09_BUILD_RUN_AND_RELEASE.md` root file governance.
- Risks: changing SDK pin without approval breaks reproducibility and violates Sprint 1 scope.
- Tests required: `dotnet --info`, restore/build/test after any approved SDK change.

## Product-code files with known drift but not Sprint 1 implementation targets

These files should not be modified in Sprint 1 unless the human explicitly expands Sprint 1 scope:

- `src/Dobby.SDK/PluginFramework/PluginBase.cs`
- `tests/Dobby.UnitTests/Contracts/PluginFrameworkContractTests.cs`
- `src/Dobby.Security/Authorization/OperationKey.cs`
- `tests/Dobby.UnitTests/Security/SecurityTests.cs`
- `src/Dobby.PluginFramework/Manifest/PluginManifest.cs`
- `src/Dobby.PluginFramework/Manifest/PluginManifestValidator.cs`
- `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs`
- `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json`

Reason: each file reflects real documentation/code drift, but fixing those behaviors belongs to later SDK, plugin, security, or sample-plugin implementation sprints unless Sprint 1 is explicitly authorized to resolve documentation conflicts.

# 8. Code Work Plan

## Step 1 - Confirm immutable baseline inputs

- Goal: confirm the sprint is using the current docs, ADRs, schemas, project files, and source tree.
- Files affected: no code files; evidence files may record the source list.
- Expected behavior: implementation agent can identify the docs/code baseline used by static checks.
- Interfaces/classes/services involved: none.
- Validation/error-handling expectations: missing baseline docs or schemas in the source-list review should be recorded as evidence failures. Automated tests should fail for missing docs or schemas only when that specific test directly reads the missing file.
- Logging/diagnostics expectations: evidence/test failure messages must include the missing path and expected purpose.
- Dependency notes: no package additions.
- Completion criteria: baseline source list is recorded in Sprint 1 evidence.

## Step 2 - Add project reference architecture checks

- Goal: enforce the documented project dependency graph, forbidden initial projects, the documented `Dobby.Core/ErrorCodes` exact-equality baseline registry, and Dobby-owned namespace-pattern guardrails.
- Files affected: `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`; `tests/Dobby.IntegrationTests/ProjectBoundaryTests.cs` only to remove the moved deferred-project assertion.
- Expected behavior: tests assert that current project references are a subset of the architecture map's Cədvəl 2 allowed references, not exact-list equality, and separately verify forbidden initial projects remain absent. They also verify that public static `ErrorCode` field names in `src/Dobby.Core/Result.cs` match the documented `Dobby.Core/ErrorCodes` baseline registry by exact equality; any new field, Branch Shared or otherwise, fails until a future approved registry update changes the documented baseline. The tests fail on declared Dobby-owned namespace segments `Common`, `Utils`, `Helpers`, or `Extensions` used as dumping-ground namespaces while avoiding false positives from third-party namespace/package names and canonical namespace-table placements.
- Interfaces/classes/services involved: project files, the current `ErrorCode` declarations in `src/Dobby.Core/Result.cs`, and Dobby-owned source/test/sample text files that declare namespaces.
- Validation/error-handling expectations: failures should name the project, actual reference, allowed references, file path, namespace or error-code identifier, and documentation basis, including the relevant `docs/02_ARCHITECTURE_MAP.md` Cədvəl 2 project-name row, `docs/04_RESULT_VS_EXCEPTION_POLICY.md` baseline registry or Branch Shared category deferral, `docs/09_BUILD_RUN_AND_RELEASE.md` initial exclusion, or `docs/08_CODING_STANDARDS.md` namespace rule and canonical namespace placement table.
- Logging/diagnostics expectations: no runtime logging; deterministic test diagnostics only.
- Dependency notes: use BCL XML/project file parsing, BCL file/text APIs, and xUnit only.
- Completion criteria: forbidden initial projects, documented `Dobby.Core/ErrorCodes` exact-equality baseline registry drift, and forbidden Dobby namespace patterns are checked; current Infrastructure/Application conflict is either fixed by approved ownership changes or recorded as temporary blocker `SPRINT1-INFRA-APP-PRINT-BOUNDARY`, an evidence-bound named unresolved exact-edge blocker that does not count as a human-approved exception, remains subject to the human print-boundary decision, fails for any additional unauthorized edge, names `artifacts/release-evidence/skeleton-local/architecture-baseline.md` as its evidence path, and carries a Sprint 2 re-confirmation/sunset gate.

## Step 3 - Add package and toolchain governance checks

- Goal: enforce central package management, deterministic package scope/stance guardrails, and make missing approval evidence visible.
- Files affected: `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`; `tests/Dobby.IntegrationTests/ProjectBoundaryTests.cs` only to remove the moved central package version assertion.
- Expected behavior: tests confirm package versions live only in `Directory.Packages.props`, projects do not introduce inline versions, documented Tier 1 test-only packages do not appear in production runtime projects, documented Tier 0 BCL/runtime-only package names are not added as explicit package references or package-version pins, documented Tier R rejected/deferred packages are absent unless an explicit human-approved opt-in record exists, and any approval-source requirement is explicit. The Tier 0 non-reference package names checked in Sprint 1 are exactly `System.Text.Json`, `System.IO.Compression`, `System.IO.Compression.ZipFile`, and `System.Threading.Channels`; runtime/BCL type names such as `IHostedService`, `TimeProvider`, `Guid.CreateVersion7()`, and `Rfc2898DeriveBytes` are outside package-name test scope. Tier R means `Commercial-license MediatR line (LuckyPennySoftware)`, `EPPlus v5+`, `FluentAssertions v8+`, `Syncfusion`, `DevExpress`, `Telerik`, `Moq`, `AutoMapper commercial line`, encrypted `SQLitePCLRaw.bundle_*` packages, `Microsoft.Extensions.Resilience`, `Microsoft.Extensions.Http`, and `Microsoft.Extensions.Http.Resilience`. Tier 1 test-only is limited to the documented test package rows and does not restrict production Tier 1 packages to `tests/**`. Approved-pin or stop-condition verification is automated only if a human-approved test-readable representation exists; otherwise the automation gap itself is recorded as a human-decision blocker in `toolchain-pin-gate.md`.
- Interfaces/classes/services involved: project files, `Directory.Packages.props`, `global.json`.
- Validation/error-handling expectations: missing approval evidence should be recorded as a blocked Sprint 1 condition. Tests should fail only when an approved representation exists and neither approved pin evidence nor a valid recorded stop condition is present.
- Logging/diagnostics expectations: test failures must name the package, project, version when applicable, violated scope/stance rule, and missing approval source when applicable.
- Dependency notes: no new package references or version changes.
- Completion criteria: central package governance and deterministic package scope/stance guardrails are automated, the missing human-approved pin list is not hidden, and package-approval automation is either passing against an approved pin or stop-condition representation or explicitly marked blocked pending that human decision.

## Step 4 - Add encoding boundary checks

- Goal: enforce UTF-8/no-BOM policy for repository-owned text files.
- Files affected: `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs`; `Dobby.sln` as an encoding-only baseline fix unless a human explicitly approves a solution-file exception.
- Expected behavior: tests scan a conservative repo-owned text-file scope with extension entries such as `.cs`, `.csproj`, `.props`, `.targets`, `.sln`, `.json`, `.md`, `.xaml`, and `.resx`, plus exact filename entries such as `.editorconfig` and `.gitignore`, explicitly including `docs/schemas/**/*.json`. Extending beyond AC-047's docs/source/resource wording is backed primarily by the `docs/08_CODING_STANDARDS.md` "Review checklist additions" UTF-8 without BOM rule; `.editorconfig` `charset = utf-8` supports the UTF-8 charset expectation only. NFR-022 and AC-047 support narrower host-owned and docs/source/resource subsets but do not independently define broad repo-root build/solution file scope. No separate human decision is required for the broad repo-owned text scope or for removing the current `Dobby.sln` BOM if implementation-time byte verification still finds it; a human decision is required only to approve a narrower scope or explicit solution-file exception. If no BOM is found at implementation time, `Dobby.sln` is not modified and absence-only evidence is recorded. The scan excludes generated/build/test outputs such as `bin/`, `obj/`, `.git/`, `TestResults/`, package/publish outputs, and binary assets by extension, including `.jpg`, `.jpeg`, `.png`, `.ico`, `.ttf`, `.otf`, `.woff`, `.woff2`, `.nupkg`, `.dobbyplugin`, `.dobbylicense`, and `.dobbyrecovery`. Do not blanket-exclude text files under `docs/_perf/`. Encoding validation is strict no-BOM plus strict UTF-8 decode with invalid byte sequences throwing; `.resx` base64 string content is valid text, while raw invalid binary bytes fail.
- Interfaces/classes/services involved: file system only.
- Validation/error-handling expectations: failure messages should include the exact file path and encoding issue.
- Logging/diagnostics expectations: no runtime logging; deterministic test output only.
- Dependency notes: no package additions.
- Completion criteria: `Dobby.sln` BOM is removed as the default baseline fix if present, no file modification is performed if no BOM is found, or an explicit human-approved exception is documented and encoded in the test allowlist. The `Dobby.sln Encoding Evidence` subsection of `artifacts/release-evidence/skeleton-local/architecture-baseline.md` records byte-level verification of the leading bytes, including the byte-dump command/output or equivalent byte-level evidence, and captures the post-fix absence of the BOM, absence-only evidence, or approved exception.

## Step 5 - Add plugin, SDK, and Contracts boundary checks

- Goal: enforce SDK, Contracts, PluginFramework, and sample plugin reference boundaries.
- Files affected: `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`; `tests/Dobby.IntegrationTests/ProjectBoundaryTests.cs` only to remove the moved sample plugin reference assertion.
- Expected behavior: tests confirm Contracts project references only `Dobby.Core` and does not leak implementation/UI/WPF dependencies. Type-level ADR-0001 narrow Core surface enforcement is deferred to Sprint 2, which owns "Contracts reference only allowed Core/BCL surfaces" and the static dependency check for Contracts and SDK references in `SPRINT_IMPLEMENTATION_PLAN.md`. SDK project references are checked as a subset of the ADR-0021 allowed set of Core, Contracts, and Localization, not exact equality; PluginFramework follows the full Architecture Map Cədvəl 2 allowed set of Core, Contracts, Security, Licensing, Configuration, and Messaging, and the sample plugin project has exactly the `Dobby.Contracts` and `Dobby.SDK` references.
- Interfaces/classes/services involved: project files only.
- Validation/error-handling expectations: failures must identify the violating project reference and the allowed boundary.
- Logging/diagnostics expectations: no runtime logging; deterministic test diagnostics only.
- Dependency notes: no package additions.
- Completion criteria: plugin and SDK boundary tests exist and current allowed references are explicit.

## Step 6 - Record architecture evidence

- Goal: create human-readable release evidence for Sprint 1 baseline findings.
- Files affected: `artifacts/release-evidence/skeleton-local/architecture-baseline.md`.
- Expected behavior: evidence lists verified project shape, deferred projects absent, static check coverage, and known documentation/code conflicts.
- Interfaces/classes/services involved: none.
- Validation/error-handling expectations: evidence should not claim conflicts are resolved unless corresponding tests and code state prove it.
- Logging/diagnostics expectations: not applicable.
- Dependency notes: no code dependencies.
- Completion criteria: evidence file exists and references the same blockers as the failing or gated checks.

## Step 7 - Make only approved baseline fixes

- Goal: address low-risk baseline drift only when it does not implement product features or cross human decision gates.
- Files affected: likely `Dobby.sln`; possibly project files only if a human resolves the Infrastructure/Application conflict.
- Expected behavior: changes are limited to encoding, test organization, or approved boundary alignment.
- Precondition: no edits to `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj`, `src/Dobby.Application/Printing/PrintTargetClassifier.cs`, or `src/Dobby.Infrastructure/Printing/WindowsPrintTargetClassifier.cs` may occur unless the human print-boundary ownership decision is recorded; otherwise the conflict stays a logged blocker only.
- Interfaces/classes/services involved: project metadata and print classifier ownership only if approved.
- Validation/error-handling expectations: any unresolved conflict remains recorded as a blocker.
- Logging/diagnostics expectations: not applicable.
- Dependency notes: no new packages, no storage providers, no Branch Shared runtime.
- Completion criteria: all non-blocked Sprint 1 checks pass; blocked checks are tied to explicit human decisions.

## Step 8 - Run verification and update evidence

- Goal: prove Sprint 1 guardrails and evidence are complete.
- Files affected: evidence Markdown files only if command results or blocker status must be recorded.
- Expected behavior: build/test/static checks run in the implementation sprint and produce reproducible results.
- Interfaces/classes/services involved: full solution and test projects.
- Validation/error-handling expectations: failed checks must be categorized as implementation failure or documented blocker.
- Logging/diagnostics expectations: command outputs should be summarized in evidence, not committed as noisy logs unless documentation requires it.
- Dependency notes: build/test commands may generate ignored `bin`, `obj`, and test output directories.
- Completion criteria: verification commands complete, or blocked commands have documented reasons and owners.

# 9. Test Plan

## Unit tests

- No new unit tests are required solely for Sprint 1 unless an approved baseline code change affects an existing unit-tested type.
- If the print classifier ownership is changed after human approval, update or add unit tests in the relevant Application or Infrastructure test area to preserve current behavior.
- Expected result: existing unit tests continue to pass after any approved baseline-only edits.

## Integration tests

### `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`

- Verifies: current source project references are subsets of the documented architecture dependency table, forbidden initial projects remain absent, public static `ErrorCode` field names in `src/Dobby.Core/Result.cs` match the documented `Dobby.Core/ErrorCodes` baseline registry in `docs/04_RESULT_VS_EXCEPTION_POLICY.md` lines 209-237 by exact equality, and declared Dobby-owned namespaces do not use `Common`, `Utils`, `Helpers`, or `Extensions` dumping-ground segments.
- Why it belongs in this sprint: Sprint 1 owns architecture boundary checks.
- Expected result: passes only when each project reference is allowed, a documented human-approved exception exists, or the current `Dobby.Infrastructure` to `Dobby.Application` edge is present as the sole temporary blocker `SPRINT1-INFRA-APP-PRINT-BOUNDARY`, tied to `artifacts/release-evidence/skeleton-local/architecture-baseline.md` with a Sprint 2 re-confirmation/sunset gate. The blocker path must not allow any new unauthorized reference, must not be described as an approved exception, and remains subject to the human print-boundary decision.

### `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`

- Verifies: package versions are centralized, project files do not carry inline versions, `global.json` is present, deterministic package scope/stance rules are enforced, and package pins have approval evidence or a recorded stop condition exists.
- Why it belongs in this sprint: Sprint 1 owns the toolchain gate.
- Expected result: central package-management checks pass; Tier 1 test-only packages stay in `tests/**` projects while production Tier 1 packages are not subject to that `tests/**` restriction; the Tier 0 BCL/runtime-only package names `System.Text.Json`, `System.IO.Compression`, `System.IO.Compression.ZipFile`, and `System.Threading.Channels` are absent from `.csproj` `PackageReference Include` and `Directory.Packages.props` `PackageVersion Include` elements; Tier R rejected/deferred packages are absent unless a human-approved opt-in record exists. Tier R means `Commercial-license MediatR line (LuckyPennySoftware)`, `EPPlus v5+`, `FluentAssertions v8+`, `Syncfusion`, `DevExpress`, `Telerik`, `Moq`, `AutoMapper commercial line`, encrypted `SQLitePCLRaw.bundle_*` packages, `Microsoft.Extensions.Resilience`, `Microsoft.Extensions.Http`, and `Microsoft.Extensions.Http.Resilience`. Pin-approval checks pass when a human-approved test-readable record contains either approved pin evidence or a recorded stop condition; if that representation is not yet approved, the pin-approval assertion is not introduced as a committed failing test and the automation gap is recorded in evidence.

### `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs`

- Verifies: repository-owned text files are UTF-8 without BOM and do not include invalid text encodings.
- Why it belongs in this sprint: Sprint 1 owns encoding guardrails.
- Expected result: passes after `Dobby.sln` has no BOM because either no BOM is found at implementation-time byte verification or a present BOM is removed by the default baseline fix, or after a human-approved exception is represented in the test. The evidence path is explicit rather than a silent implementation choice. Each scanned file must avoid the `EF BB BF` BOM signature and strict-decode as UTF-8 without invalid byte sequences. The test scope explicitly includes in-scope text schemas and excludes generated outputs plus binary assets by extension, without blanket-excluding `docs/_perf/` text.

### `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`

- Verifies: Contracts, SDK, PluginFramework, and sample plugin project references stay within their per-project documented boundaries: Contracts project references only `Dobby.Core`, with ADR-0001 type-level Core allow-list enforcement explicitly deferred to Sprint 2 ownership of "Contracts reference only allowed Core/BCL surfaces" in `SPRINT_IMPLEMENTATION_PLAN.md`; SDK references are a subset of Core, Contracts, and Localization per ADR-0021, PluginFramework follows the full Architecture Map Cədvəl 2 allowed set of Core, Contracts, Security, Licensing, Configuration, and Messaging, and sample plugin references are exactly `Dobby.Contracts` and `Dobby.SDK`. The sample-plugin reference check is Sprint 1's AC-043 contribution artifact.
- Why it belongs in this sprint: Sprint 1 explicitly calls for plugin reference, SDK limit, and Contracts dependency checks.
- Expected result: passes when all project references remain within documented boundaries.

### `tests/Dobby.IntegrationTests/ProjectBoundaryTests.cs`

- Verifies: retained high-level project-boundary checks after focused checks are split out, specifically startup closed pre-login work and existing skeleton KDF evidence.
- Why it belongs in this sprint: existing coverage must not be lost.
- Expected result: package centralization, deferred project absence, and sample plugin reference assertions are owned only by their new focused test files.

### `tests/Dobby.IntegrationTests/Architecture/ReleaseEvidenceTests.cs`

- Verifies: `artifacts/release-evidence/skeleton-local/architecture-baseline.md` exists and contains `## Static Guardrail Status`, and `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` exists and contains `## Package Pin Approval Status`.
- Why it belongs in this sprint: evidence file creation is part of the Sprint 1 exit condition and should not be skippable.
- Expected result: passes when both evidence files exist with their required canonical headings; detailed evidence content remains manually reviewed and is not made brittle test-owned text.

## Contract tests

- No external contract-test project is required in Sprint 1.
- Boundary integration tests should serve as repository contract tests for project references, package governance, encoding, and forbidden project absence.

## Smoke tests

- Existing smoke tests should remain unchanged unless approved baseline changes affect shell or Admin startup.
- Expected result: `tests/Dobby.SmokeTests/ShellSmokeTests.cs` continues to pass for both shell navigation labels and the co-located Admin command-host smoke test.

## Build and compile checks

Run during implementation verification:

- `dotnet --info`
- `dotnet restore .\Dobby.sln`
- `dotnet build .\Dobby.sln -c Debug --no-restore`
- `dotnet test .\Dobby.sln -c Debug --no-build`
- `dotnet format .\Dobby.sln --verify-no-changes`

Expected result: commands pass after approved fixes. Human-decision blockers may leave scoped automation or implementation work incomplete, but they must not be represented as the normal committed state of a failing test suite.

## Manual verification

- Review `artifacts/release-evidence/skeleton-local/architecture-baseline.md`.
- Review `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`.
- Review the `Dobby.sln Encoding Evidence` subsection of `artifacts/release-evidence/skeleton-local/architecture-baseline.md` to confirm the pre/post byte evidence for default BOM removal, the absence-only byte evidence if no BOM was found, or the captured BOM plus explicit exception if a human-approved exception is granted.
- Confirm the public static `ErrorCode` field names in `src/Dobby.Core/Result.cs` match the documented `Dobby.Core/ErrorCodes` baseline registry in `docs/04_RESULT_VS_EXCEPTION_POLICY.md` lines 209-237 by exact equality.
- Confirm no Branch Shared runtime, PostgreSQL provider, update project, storage migrations, or package additions were introduced.
- Confirm all unresolved documentation/code conflicts are listed with owner/decision status.

# 10. Configuration, Build, and Tooling Changes

- Project files: no project references should change unless the Infrastructure/Application conflict is resolved by a human-approved ownership decision.
- Solution files: `Dobby.sln` receives an encoding-only modification only if implementation-time byte verification finds a BOM and no human explicitly approves a solution-file exception; if no BOM is found, no solution-file modification is performed and absence-only evidence is recorded.
- Package references: no new package references and no version changes are allowed without human approval.
- `Directory.Packages.props`: no change unless the human-approved pin list requires a correction.
- `global.json`: no change unless the human-approved toolchain decision requires a different SDK pin.
- Build scripts: no new build scripts are required.
- Runtime configuration: no `appsettings.json` or runtime behavior changes are required.
- Test configuration: no new test project or test package is required; use existing xUnit integration test infrastructure under `tests/Dobby.IntegrationTests`, inheriting its current target framework and `UseWPF` settings.
- CI configuration: no CI files were found and none are required by Sprint 1.
- Local verification commands: use the documented `dotnet` commands from `docs/09_BUILD_RUN_AND_RELEASE.md` after implementation changes.

If no human-approved pin list is provided, Sprint 1 must record that package governance is blocked rather than modifying package versions or inventing an approval source. Automated pin-approval checks require a human-approved test-readable pin-list or stop-condition representation; without that decision, the plan records the automation gap instead of adding an intentionally failing test.

# 11. Implementation Order

1. Preparation
   - Re-read `SPRINT_IMPLEMENTATION_PLAN.md`, docs `02`, `03`, `04` scoped to the ErrorCode baseline registry and Branch Shared addendum, `07` scoped to AC-043 and AC-047, `08`, `09`, ADR-0001, ADR-0021, ADR-0036, ADR-0040, ADR-0048, ADR-0053, ADR-0054, ADR-0056, and ADR-0057.
   - Confirm whether a human-approved pin list exists outside the current repository inspection.
   - Confirm whether a human-approved test-readable pin-list or stop-condition representation exists for package governance checks.
   - Byte-verify the current `Dobby.sln` leading bytes; if a BOM is present, remove it as the default encoding baseline fix unless a human explicitly approves a solution-file exception; if no BOM is present, do not modify the file and record absence-only evidence.
   - Confirm whether the Infrastructure/Application print-classifier conflict should be fixed or recorded as a temporary, evidence-bound unresolved blocker with a Sprint 2 re-confirmation/sunset gate.
2. File creation
   - Add focused architecture, package governance, encoding, and plugin/SDK boundary integration tests.
   - Add Sprint 1 evidence files under `artifacts/release-evidence/skeleton-local/`.
3. Core implementation
   - Implement only static checks and evidence generation/recording.
   - Do not implement product features.
4. Integration
   - Apply the `ProjectBoundaryTests.cs` assertion ownership mapping so each moved assertion has exactly one focused test owner.
   - Apply only approved baseline fixes, such as the default `Dobby.sln` encoding cleanup when a BOM is present or approved project-reference correction.
   - Do not edit `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj`, `src/Dobby.Application/Printing/PrintTargetClassifier.cs`, or `src/Dobby.Infrastructure/Printing/WindowsPrintTargetClassifier.cs` unless the human print-boundary ownership decision is recorded; otherwise keep the conflict as a temporary, evidence-bound unresolved blocker with a Sprint 2 re-confirmation/sunset gate.
5. Tests
   - Run the focused integration tests first.
   - Run all unit, integration, and smoke tests through the solution.
6. Verification
   - Run documented restore/build/test/format checks.
   - Update evidence with pass/fail/blocker status.
7. Cleanup
   - Confirm no unapproved package/version/project additions occurred.
   - Confirm no generated `bin`, `obj`, test result, or publish output is committed.
   - Confirm unresolved decisions are listed and not silently bypassed.

# 12. Risks, Blockers, and Human Decisions

## Blockers

- No repository-local human-approved package pin list was found.
- Current `Directory.Packages.props` pins cannot be treated as approved solely because they exist.
- Package-bearing changes must stop until approval evidence is supplied.
- Current `Dobby.Infrastructure` to `Dobby.Application` reference conflicts with the documented dependency table, ADR-0040, and ADR-0053 print-classifier placement; it remains an unresolved blocker unless a human-authorized ADR/architecture-map resolution or ownership change is recorded. Any coded test allowance for this edge is temporary, evidence-bound, must fail for any additional unauthorized edge, and must not be treated as an approved architecture exception.
- Current planner-observed `Dobby.sln` BOM conflicts with UTF-8/no-BOM rules and must be removed as an encoding-only baseline fix if implementation-time byte verification still finds it, unless a human explicitly approves a solution-file exception. If no BOM is found, no file modification is performed and absence-only evidence is recorded.

## Unresolved documentation questions

- Should Visual Studio `.sln` files receive an explicit documented BOM exception despite the repo-owned text no-BOM rule?
- Should the print target classifier abstraction remain in Application as ADR-0053 and the architecture map state, should ADR-0040 or the architecture map allow the Infrastructure to Application edge, or should the abstraction move through a human-authorized ADR/architecture-map amendment?
- Where should the human-approved package pin list or recorded stop condition live, and what test-readable format should Sprint 1 checks read?

## Risky assumptions

- Treating `Directory.Packages.props` as the approval list would violate the documented approval gate.
- Fixing Infrastructure/Application references without a human decision may create a new undocumented print abstraction or silently amend ADR-0040, ADR-0053, and the architecture map.
- Broad encoding scans can accidentally include binary or generated files unless the scan scope is conservative and documented.
- Making plugin manifest, operation verb, or SDK behavior fixes in Sprint 1 could pull later feature-sprint scope into a guardrail sprint.

## Human decisions required

- Provide or identify the approved SDK/package pin list or explicitly record the package-governance stop condition.
- Approve the on-disk, test-readable representation for an approved pin list or recorded package-governance stop condition before automated pin-approval checks can be considered complete.
- Decide only if an explicit `Dobby.sln` BOM exception is requested; otherwise the documented no-BOM rule applies, and the file is fixed as baseline encoding work only if implementation-time byte verification still finds a BOM.
- Decide how to resolve the Infrastructure/Application print-adapter boundary conflict: amend ADR-0040, amend ADR-0053, amend the architecture map, or relocate/reclassify the abstraction.
- Decide at Sprint 2 start whether the `Dobby.Infrastructure` to `Dobby.Application` unresolved blocker is renewed by human confirmation or removed.
- Decide whether any ADR update is authorized after implementation evidence is reviewed.

## Implementation Determinism Addendum

These rules close the remaining interpretation surface for single-pass Sprint 1 execution. They do not change the Sprint 1 scope or turn deferred work into implementation work.

### D-023.1 - Evidence existence guardrails

- `tests/Dobby.IntegrationTests/Architecture/ReleaseEvidenceTests.cs` verifies only that `artifacts/release-evidence/skeleton-local/architecture-baseline.md` exists and contains `## Static Guardrail Status`, and that `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` exists and contains `## Package Pin Approval Status`.
- The test must not assert detailed evidence body content, command output, blocker prose, or complete heading order.

### D-023.2 - Evidence heading mapping

- `artifacts/release-evidence/skeleton-local/architecture-baseline.md` must contain these headings: `## Sources`, `## Static Guardrail Status`, `## Known Blockers`, `## Dobby.sln Encoding Evidence`, `## Deferred Drift`, and `## Sprint 2 Reconfirmation`.
- `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` must contain these headings: `## Sources`, `## Package Pin Approval Status`, and `## Known Blockers`.
- `architecture-baseline.md` owns architecture, encoding, and documentation/code blockers, including `SPRINT1-INFRA-APP-PRINT-BOUNDARY`.
- `toolchain-pin-gate.md` owns only package/toolchain blockers and includes the cross-reference sentence `Architecture blockers are tracked in architecture-baseline.md`.

### D-023.3 - Sprint 2 reconfirmation contract

- The `## Sprint 2 Reconfirmation` section records the exact sunset contract for `SPRINT1-INFRA-APP-PRINT-BOUNDARY`: at Sprint 2 start, the architecture-baseline pass must either (a) renew the blocker by human confirmation, (b) remove the named architecture-test exception and let the `Dobby.Infrastructure` to `Dobby.Application` edge fail, or (c) record the human-authorized ADR-0040 or architecture-map amendment that changes the boundary.

### D-023.4 - Encoding scan strictness

- Encoding tests use two checks for every in-scope text file: the first three bytes are not `EF BB BF`, and the full byte payload strict-decodes as UTF-8 with invalid byte sequences throwing.
- `.resx` base64 string content is valid UTF-8 text; raw invalid binary bytes fail.
- Allowlist: `.cs`, `.csproj`, `.props`, `.targets`, `.sln`, `.json`, `.md`, `.xaml`, `.resx`, `.editorconfig`, and `.gitignore`.
- Exclude: `bin/`, `obj/`, `.git/`, `TestResults/`, package/publish outputs, and binary assets by extension such as `.jpg`, `.jpeg`, `.png`, `.ico`, `.ttf`, `.otf`, `.woff`, `.woff2`, `.nupkg`, `.dobbyplugin`, `.dobbylicense`, and `.dobbyrecovery`.
- Do not blanket-exclude text files under `docs/_perf/`.

### D-023.5 - ErrorCode exact-equality test

- Architecture tests read public static `ErrorCode` field names from `src/Dobby.Core/Result.cs` by reflection and compare them by exact equality to the `docs/04_RESULT_VS_EXCEPTION_POLICY.md` lines 209-237 baseline registry.
- The test fails if the code declares fewer or more names than the documented baseline. Branch Shared identifiers are not separately enumerated; any new `ErrorCode` field fails equality until a future approved registry update changes the baseline.

### D-023.6 - Tier 0 non-reference list

- Package governance tests check exactly these Tier 0 BCL/runtime-only package names for absence from `.csproj` `PackageReference Include` and `Directory.Packages.props` `PackageVersion Include` elements: `System.Text.Json`, `System.IO.Compression`, `System.IO.Compression.ZipFile`, and `System.Threading.Channels`.
- Runtime/BCL type names such as `IHostedService`, `TimeProvider`, `Guid.CreateVersion7()`, and `Rfc2898DeriveBytes` are not package names and are outside this test scope.

### D-023.7 - Inventory prose is non-authoritative

- Implementation-agent test fixtures must not treat inventory prose as the source of truth.
- For Contracts and other project area names, tests use the real `src/<Project>/*` folder/namespace state and the `docs/02_ARCHITECTURE_MAP.md` Cədvəl 2 dependency boundary. The plan's Section 2 and Section 4 prose is explanatory context, not authoritative test input.

# 13. Definition of Done

- Files created:
  - Focused architecture boundary integration tests.
  - Package governance integration tests.
  - Encoding boundary integration tests.
  - Plugin/SDK/Contracts boundary integration tests.
  - Release evidence existence integration tests.
  - Sprint 1 architecture evidence.
  - Sprint 1 toolchain pin-gate evidence.
- Files modified:
  - Existing broad project-boundary tests updated only as needed.
  - `Dobby.sln` encoding fixed by removing the BOM as baseline work if implementation-time byte verification finds one unless a human explicitly approves a solution-file exception; if no BOM is found, `Dobby.sln` is not modified and absence-only evidence is recorded.
  - Project references changed only after human decision.
  - No feature code modified unless explicitly authorized as baseline-only conflict resolution.
- Test files created or assertions moved/added:
  - Existing `ProjectBoundaryTests.cs` assertions follow the ownership map in Sections 7 and 8 so moved assertions have one focused owner and are not counted as duplicate net-new coverage.
  - Architecture dependency tests.
  - Forbidden Dobby namespace-pattern tests.
  - `ErrorCode` exact-equality baseline registry tests, which implicitly fail on Branch Shared identifiers or any other unapproved new field.
  - Package governance tests, including central package management, inline-version bans, Tier 1 test-only package isolation without restricting production Tier 1 packages to `tests/**`, Tier 0 BCL/runtime-only non-reference checks for `System.Text.Json`, `System.IO.Compression`, `System.IO.Compression.ZipFile`, and `System.Threading.Channels`, and Tier R rejected/deferred package absence checks for `Commercial-license MediatR line (LuckyPennySoftware)`, `EPPlus v5+`, `FluentAssertions v8+`, `Syncfusion`, `DevExpress`, `Telerik`, `Moq`, `AutoMapper commercial line`, encrypted `SQLitePCLRaw.bundle_*` packages, `Microsoft.Extensions.Resilience`, `Microsoft.Extensions.Http`, and `Microsoft.Extensions.Http.Resilience`.
  - Encoding tests, including strict no-BOM and strict UTF-8 decode checks for in-scope repository text files.
  - Plugin/SDK/Contracts boundary tests, including the AC-043 Sprint 1 sample-plugin reference contribution.
  - Release evidence existence tests that check only the required canonical headings `## Static Guardrail Status` and `## Package Pin Approval Status`.
- Tests passing:
  - Unit tests pass.
  - Integration tests that are implemented for Sprint 1 pass.
  - Package pin-approval automation is either passing against a human-approved test-readable pin or stop-condition representation, or is documented as blocked pending that representation instead of being committed as a failing test.
  - Smoke tests pass.
  - Build and format checks pass; any blocked command has an exact cause and is not caused by intentionally committed failing tests.
- Documentation/ADR follow-up identified:
  - Missing approved pin list recorded.
  - ADR-0001 type-level Contracts/Core surface enforcement recorded as Sprint 2-owned deferred guardrail for "Contracts reference only allowed Core/BCL surfaces."
  - Infrastructure/Application conflict recorded as temporary blocker `SPRINT1-INFRA-APP-PRINT-BOUNDARY`, an evidence-bound unresolved blocker with a Sprint 2 re-confirmation/sunset gate, not as an approved exception.
  - SDK's absent-but-allowed `Dobby.Localization` reference recorded as observed current-graph evidence only, not as SDK completeness proof or a required Sprint 1 fix.
  - `PluginBase` ADR/code conflict recorded as deferred SDK drift.
  - Manifest schema/code/sample conflict recorded as deferred plugin/package drift, including the schema-required `capabilities[].name` field.
  - Operation verb conflict recorded as deferred authorization/plugin-manifest drift.
  - `Dobby.sln` encoding evidence recorded with implementation-time byte-level leading-byte evidence in the `Dobby.sln Encoding Evidence` subsection of `artifacts/release-evidence/skeleton-local/architecture-baseline.md`, including pre/post bytes for the baseline BOM removal, absence-only byte evidence if no BOM is found, or captured BOM plus explicit approval for an exception.
  - Evidence files use the heading ownership and blocker split defined by the Section 12 Implementation Determinism Addendum.
- No out-of-scope work done:
  - No Branch Shared runtime.
  - Current `ErrorCode` declarations in `src/Dobby.Core/Result.cs` match the documented `Dobby.Core/ErrorCodes` baseline registry in `docs/04_RESULT_VS_EXCEPTION_POLICY.md` by exact equality, preventing Branch Shared or any other unapproved new identifiers before a future approved registry update.
  - No PostgreSQL provider package or project.
  - No `Dobby.Update`.
  - No storage entities, migrations, repositories, or provider packages.
  - No plugin manager UI/runtime feature implementation.
  - No license import feature implementation.
  - No user/session feature implementation.
- Sprint exit condition satisfied:
  - Existing skeleton is verified against architecture/build boundaries.
  - Forbidden initial projects, documented `Dobby.Core/ErrorCodes` exact-equality baseline registry drift, and forbidden Dobby namespace patterns are guarded.
  - Any unapproved package/version need is recorded as a stop condition.
  - Later sprint agents can rely on explicit guardrails instead of re-planning boundaries.

# 14. Out of Scope

- Sprint 2 work: expanding Core, Contracts, SDK behavior beyond static boundary checks.
- Sprint 3 work: diagnostics, audit, logging, or durable evidence implementation beyond Sprint 1 Markdown evidence.
- Sprint 4 work: storage abstractions, protected storage implementation, database provider selection, entities, migrations, or repositories.
- Sprint 5 work: startup login screen, session runtime, and login-first shell transitions.
- Sprint 6 work: local auth, users, roles, settings UI, and password/session feature behavior.
- Sprint 7 work: shell UI, navigation UI, localization UI, and design-system feature implementation.
- Sprint 8 work: plugin runtime loading, plugin activation, async operation execution, or plugin policy enforcement.
- Sprint 9 work: license import, activation, offline license enforcement, or license UI.
- Sprint 10 work: plugin manager UI, sample plugin feature completion, Admin command implementation beyond existing placeholders.
- Sprint 11 work: installer packaging, release packaging, final smoke evidence, or deployment scripts.
- Branch Shared runtime work, including Branch Setup, PostgreSQL deployment code, Npgsql package references, Branch Shared admin/migration tools, or shared-data runtime.
- `Dobby.Update` creation or update mechanism implementation.
- Adding, upgrading, or changing package versions without human approval.
- Closing, updating, or creating ADRs without explicit human authorization.
- Changing plugin manifest, SDK, operation verb, or sample plugin behavior unless the human explicitly expands Sprint 1 from baseline guardrails to conflict remediation.

# 15. Final Execution Checklist

Before starting implementation:

- Reconfirm Sprint 1 scope in `SPRINT_IMPLEMENTATION_PLAN.md`.
- Check whether a human-approved package pin list is available.
- Check whether a human-approved test-readable pin-list or stop-condition representation is available before adding automated pin-approval assertions.
- Byte-verify whether `.sln` BOM is present; if present, remove it as the default encoding baseline fix unless a human explicitly approves a solution-file exception; if absent, do not modify the file and record absence-only evidence.
- Check whether the Infrastructure/Application print boundary has a human decision.
- Confirm no feature-sprint work has been added to Sprint 1.

During implementation:

- Add focused static integration tests with clear failure messages.
- Create Sprint 1 evidence files under `artifacts/release-evidence/skeleton-local/`.
- Do not add intentionally failing package pin-approval tests before a human-approved test-readable pin-list or stop-condition representation exists.
- Avoid new packages and package version changes.
- Avoid Branch Shared runtime, storage provider, update, installer, plugin manager, licensing, auth, and shell feature work.
- Record documentation/code conflicts instead of silently resolving them outside scope.

Before finishing:

- Run the documented restore/build/test/format checks.
- Confirm all implemented new and existing tests pass; list exact human-decision blockers for any scoped-out automation or implementation work.
- Confirm no forbidden projects, forbidden Dobby namespace patterns, or package references were added.
- Confirm evidence names the package pin status and architecture baseline status.
- Confirm ADR follow-up is identified but no ADR was changed unless explicitly authorized.
- Confirm Sprint 1 exit condition is satisfied or marked partially blocked with exact blockers.
