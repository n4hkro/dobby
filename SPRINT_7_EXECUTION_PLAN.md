# Sprint 7 Execution Plan — Shell, Navigation, Theme, Localization, and Work Sessions

## 1. Sprint Summary
- Sprint number and title: Sprint 7 — Shell, Navigation, Theme, Localization, and Work Sessions (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7).
- Main goal: build the authenticated WPF shell and user-facing host experience required for P1 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Goal).
- Expected deliverable: authenticated post-login shell with `TopToolbar`, `Sidebar`, `Third Bar`, `Main Content`, bottom status/progress, minimized work strip, built-in `Home`/`Plugins`/`Settings` routes, theme/localization foundations, explicit in-memory work-session lifecycle, rendered Settings/About access, and an authenticated diagnostics-export action surface (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Scope; `docs/specs/shell-ui.md`; `docs/specs/work-sessions.md`).
- Why this sprint exists: Sprint 6 delivered Settings/User Management source behavior and explicitly left rendered Settings and snapshot/manual evidence to Sprint 7 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6 / Acceptance and exit expectations; `artifacts/release-evidence/skeleton-local/architecture-baseline.md` §Sprint 6 Implementation Evidence). Sprint 7 also provides shell evidence used by AC-008 release consolidation and hosts later Sprint 8 plugin navigation (`SPRINT_IMPLEMENTATION_PLAN.md` §Cross-Sprint Dependency Notes).
- Readiness: READY FOR IMPLEMENTATION. Required predecessor evidence is present for current planning: Sprint 6 implementation evidence is recorded as implemented and verified with 487 tests and zero failures (`artifacts/release-evidence/skeleton-local/architecture-baseline.md` §Sprint 6 Implementation Evidence); Sprint 6 decision gates `D-20` and `D-21` are recorded in `artifacts/release-evidence/skeleton-local/decision-log.md`; Sprint 7 package approval `HD-S7-001` / `Q1` is closed in `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26` with primary evidence in `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md#sprint-7-package-approval---hd-s7-001--q1`. `Directory.Packages.props` remains current repository state, not generic approval evidence by itself.
- Strategy:
  1. Preserve scope boundaries: consume Sprint 5 auth/session and Sprint 6 Settings/authorization sources; do not implement Sprint 8 plugin activation, Sprint 9 licensing, or Sprint 10 plugin manager/admin artifact work (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Dependencies; §Sprint 8; §Sprint 9; §Sprint 10).
  2. Write tests first for route ordering/fallback, rendered shell structure, theme preference wiring, localization coverage/missing-key diagnostics, work-session lifecycle, diagnostics export action, Azerbaijani diacritics, and release-evidence closure (`docs/07_ACCEPTANCE_CRITERIA.md`; `tests/Dobby.UnitTests/Navigation/NavigationTests.cs`; `tests/Dobby.UnitTests/Shell/WorkSessionRegistryTests.cs`).
  3. Extend existing shell/navigation/localization/theme code rather than replacing established project boundaries (`src/Dobby.Shell/ShellViewModel.cs`; `src/Dobby.Navigation/RouteRegistry.cs`; `src/Dobby.Localization/HostStringCatalog.cs`; `src/Dobby.UI/Themes/DesignTokens.xaml`).
  4. Wire the existing post-login WPF window to the authenticated shell while keeping OS/WPF adapters out of ViewModels (`src/Dobby.Host/MainWindow.xaml`; `src/Dobby.Host/App.xaml.cs`; `docs/08_CODING_STANDARDS.md`).
  5. Close with build/test/format, snapshot/manual evidence, encoding scans, and release-evidence appends (`docs/09_BUILD_RUN_AND_RELEASE.md`; `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs`).
- Dependency summary: consumes Sprints 2-6 shared primitives/auth/config/audit/storage/settings; unblocks Sprint 8 plugin route/page hosting and Sprint 11 AC-008 smoke consolidation (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Dependencies; §Cross-Sprint Dependency Notes).

## 2. Sources Read

Planning audit count: 126 inspected source entries. Inspection depth labels are `deep`, `targeted`, and `boundary/context`.

### Master sprint plan
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `SPRINT_IMPLEMENTATION_PLAN.md` | deep | Master authority for Sprint 7 scope, dependencies, AC ownership, out-of-scope boundaries, and later-sprint sequencing. |
| `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 | deep | Direct source for title, goal, scope, task groups, dependencies, verification, risks, and exit criteria. |
| `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8 | boundary/context | Protects plugin package trust, install, activation runtime, and manifest seeding as later work. |
| `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9 | boundary/context | Protects licensing/runtime-mode enforcement and Sprint 1 print-boundary closure as later work. |
| `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 10 | boundary/context | Protects Plugins Manager, sample plugin, and Admin artifact tools as later work. |
| `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 11 | boundary/context | Defines release-gate and AC-008 consolidation dependency on Sprint 7 evidence. |

### Previous sprint execution plans
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `SPRINT_1_EXECUTION_PLAN.md` | targeted | Established baseline plan conventions and Sprint 1 print-boundary blocker context. |
| `SPRINT_2_EXECUTION_PLAN.md` | targeted | Confirmed shared API/navigation vocabulary continuity and renewed print-boundary blocker context. |
| `SPRINT_3_EXECUTION_PLAN.md` | targeted | Confirmed configuration, diagnostics, audit, and diagnostics export package ownership consumed by Sprint 7 AC-033 contribution. |
| `SPRINT_4_EXECUTION_PLAN.md` | targeted | Confirmed protected storage and `HostConfiguration` persistence context consumed by settings/theme preferences. |
| `SPRINT_5_EXECUTION_PLAN.md` | deep | Confirmed auth/session/recovery predecessor expectations and production recovery evidence boundary. |
| `SPRINT_6_EXECUTION_PLAN.md` | deep | Immediate predecessor; established execution-plan format, D-20/D-21 gates, Settings/About source behavior, and rendered Settings carry-over to Sprint 7. |

### Later sprint references (boundary/context only)
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8 | boundary/context | Later plugin trust/activation and plugin `operationTargets` seeding are out of Sprint 7. |
| `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9 | boundary/context | Later licensing/runtime mode and print-boundary closure are out of Sprint 7. |
| `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 10 | boundary/context | Later Plugins Manager/sample plugin/Admin recovery-response tooling are out of Sprint 7. |
| `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 11 | boundary/context | Sprint 7 must leave release-gate consolidation to Sprint 11 while producing named evidence hooks. |

No later `SPRINT_*_EXECUTION_PLAN.md` file was present in the repository root at planning time; later context therefore comes only from `SPRINT_IMPLEMENTATION_PLAN.md`.

### Repo-root files
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `README.md` | targeted | Confirms local command conventions and canonical docs location. |
| `Dobby.sln` | targeted | Confirms source/test project membership and WPF test projects. |
| `.editorconfig` | targeted | Confirms UTF-8, newline, whitespace, indentation, and file-scoped namespace rules. |
| `global.json` | targeted | Confirms .NET SDK `10.0.202` pin. |
| `Directory.Build.props` | targeted | Confirms nullable, warnings-as-errors, analyzer, deterministic, and code-style build settings. |
| `Directory.Packages.props` | targeted | Confirms central package pins including WPF-UI, localization, MVVM, xUnit, Shouldly, NSubstitute, Verify.Xunit, and test SDK. |

### Product / architecture / requirements docs
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `docs/01_PRODUCT_SCOPE.md` | deep | Product authority for login-after shell, navigation bars, work sessions, localization/theme, no telemetry, and plugin boundary. |
| `docs/02_ARCHITECTURE_MAP.md` | deep | Project/namespace ownership, allowed dependencies, Shell/UI/Localization/Navigation conventions, future-scope exclusions. |
| `docs/03_TECH_STACK_AND_VERSIONS.md` | targeted | Stack pins, WPF `net10.0-windows`, WPF-UI, `.resx` localization, test stack, and package-governance context. |
| `docs/04_RESULT_VS_EXCEPTION_POLICY.md` | deep | Result/exception discipline, ErrorCode baseline, UI boundary non-disclosure, cancellation mapping. |
| `docs/05_FUNCTIONAL_REQUIREMENTS.md` | targeted | FR-005, FR-010, FR-013, FR-015, FR-019, FR-023, FR-024, FR-027, and FR-029 drove work plan and DoD. |
| `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` | targeted | NFR-010, NFR-012, NFR-013, NFR-019, NFR-020, NFR-021, NFR-022, and NFR-023 drove verification and boundaries. |
| `docs/07_ACCEPTANCE_CRITERIA.md` | deep | AC-003, AC-016, AC-017, AC-019, AC-023, AC-027, AC-035, AC-040, AC-048, and AC-033 mapping. |
| `docs/08_CODING_STANDARDS.md` | targeted | ViewModel adapter boundary, namespace rules, test naming, encoding, and zero-warning constraints. |
| `docs/09_BUILD_RUN_AND_RELEASE.md` | targeted | Build/test/format/release-evidence closure command sequence and evidence conventions. |

### ADRs
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `docs/adr/ADR-0005-ui-ordering-model.md` | deep | Ordering key semantics, fixed built-ins, user override boundary. |
| `docs/adr/ADR-0013-routing-and-deep-link-model.md` | deep | Route identity/fallback ownership and post-login Home start. |
| `docs/adr/ADR-0015-background-operation-reporting.md` | deep | Background operation contract, status/progress rendering, no ViewModel ad-hoc threading. |
| `docs/adr/ADR-0019-audit-vs-diagnostics-boundary.md` | deep | AC-033 diagnostics export and localization missing-key diagnostics vs audit boundary. |
| `docs/adr/ADR-0028-shell-composition-ordering-render.md` | deep | Shell composition surfaces, profile menu, notifications, and render ordering. |
| `docs/adr/ADR-0031-session-lifecycle-and-refresh.md` | targeted | Logout/lock/session behavior consumed by work sessions and profile menu actions. |
| `docs/adr/ADR-0036-plugin-manifest-schema.md` | boundary/context | Plugin `extensionPoints` and `operationTargets` shape; validation and activation remain Sprint 8. |
| `docs/adr/ADR-0041-login-first-startup-policy.md` | targeted | Pre-login heavy-work restrictions and `startup.shell-first-paint` milestone. |
| `docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md` | deep | Default `az-AZ`, fallback chain, missing-key diagnostics, no runtime language switch. |
| `docs/adr/ADR-0044-localization-resource-organization.md` | deep | Host localization resource ownership in `Dobby.Localization`; plugin localization stays plugin-owned. |
| `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md` | boundary/context | Plugin package/license install and activation boundaries are not Sprint 7 implementation. |
| `docs/adr/ADR-0046-ui-and-code-terminology-mapping.md` | deep | User-facing Azerbaijani terminology and target terms `TopToolbar`, `Sidebar`, `Third Bar`, plugin page region. |
| `docs/adr/ADR-0048-operation-based-authorization.md` | targeted | Operation authorization and non-disclosing deny semantics consumed by Settings and Plugins/License placeholders. |
| `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md` | deep | Navigation descriptor hierarchy, target vocabulary, plugin page contract boundary, fallback behavior. |
| `docs/adr/ADR-0050-in-memory-work-sessions.md` | deep | Work-session process lifetime, explicit minimize, warning choices, close/cancel, restart boundary. |

### Specs
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `docs/specs/shell-ui.md` | deep | Canonical shell surfaces, layout rules, route ordering, theme/localization, profile menu, status/progress, and verification. |
| `docs/specs/work-sessions.md` | deep | Work-session lifecycle, warning choices, minimize/restore, restart, logout/lock/plugin lifecycle behavior. |
| `docs/specs/design-system-assets.md` | deep | Theme tokens, light/dark design-system expectations, WPF-UI inventory, spacing/radius/typography/icon constraints. |
| `docs/assets/ui_design_light_theme.jpg` | boundary/context | Non-normative visual reference cited by shell/design-system specs; used only for composition and light-theme color-family context, not measurements, labels, plugin/license defaults, or implementation scope. |
| `docs/specs/settings-and-about.md` | deep | Rendered Settings category tree, About privacy fields, TopToolbar/Appearance shared preference, license/About boundary. |
| `docs/specs/audit-and-diagnostics.md` | deep | Exact `diagnostics.export.created` audit event, `localization.missing-key`, no telemetry, redaction/export requirements. |
| `docs/specs/session-lifecycle.md` | targeted | Logout, lock, password-change, work-session interaction, session audit events. |
| `docs/specs/plugins-manager.md` | boundary/context | Plugins Manager behavior, install/runtime/activation work, and plugin status surfaces are later-sprint scope. |
| `docs/specs/license-import.md` | boundary/context | `Plugins > License` canonical placement; Settings/About must not become license import/status surfaces. |

### Schemas
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `docs/schemas/dobby-plugin-manifest.schema.json` | targeted | Machine-readable plugin `extensionPoints` closed target vocabulary and route-key/resource-key shapes for boundary protection only. |

### Source code
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `src/Dobby.Core/Result.cs` | deep | Existing `Result<T>` and closed `ErrorCode` registry; no new Core error codes planned. |
| `src/Dobby.Audit/AuditEventName.cs` | deep | Existing `diagnostics.export.created` event; no new audit event names planned. |
| `src/Dobby.Contracts/BackgroundOperations/BackgroundOperationContracts.cs` | deep | Existing background operation snapshot/status/progress contract to render in shell. |
| `src/Dobby.Contracts/PluginPages/PluginPageContracts.cs` | deep | Plugin page boundary has no WPF types; Sprint 7 must not widen it. |
| `src/Dobby.Contracts/Policies/PolicyContracts.cs` | targeted | Existing typed policy payload and non-disclosing policy decision shape. |
| `src/Dobby.Configuration/SettingsPreferences.cs` | deep | Existing settings preference aggregate includes appearance/theme and security/session. |
| `src/Dobby.Configuration/ISettingsPreferenceSource.cs` | deep | Existing Settings preference source contract has parameterless `Load`/`Save`; Sprint 7 shell preferences therefore require an explicitly authenticated user-scoped source instance or equivalent scope value before persistence is safe. |
| `src/Dobby.Configuration/AppThemePreference.cs` | deep | Existing light/dark enum; no new theme closed-set values planned. |
| `src/Dobby.Configuration/AppearancePreference.cs` | deep | Existing Appearance preference shape consumed by Settings and TopToolbar theme switch. |
| `src/Dobby.Application/BackgroundOperationReporter.cs` | targeted | Current host reporter adapter to feed shell status/progress. |
| `src/Dobby.Diagnostics/DiagnosticsExportPackageBuilder.cs` | deep | Existing local support package builder consumed by shell action. |
| `src/Dobby.Diagnostics/DiagnosticsExportPackage.cs` | targeted | Existing export result shape. |
| `src/Dobby.Diagnostics/DiagnosticRedactor.cs` | targeted | Existing redaction boundary for diagnostics export. |
| `src/Dobby.Diagnostics/LocalizationMissingKeyEvent.cs` | targeted | Existing diagnostics-only event record for missing localization keys. |
| `src/Dobby.Diagnostics/StartupMilestoneNames.cs` | targeted | Existing `startup.shell-first-paint` milestone constant. |
| `src/Dobby.Diagnostics/StartupMilestone.cs` | targeted | Existing startup milestone shape. |
| `src/Dobby.Navigation/RouteRegistry.cs` | deep | Current built-in route constants and fallback implementation. |
| `src/Dobby.Navigation/DefaultUiOrderingComparer.cs` | deep | Current ordering comparer for group/weight/plugin/declaration tiebreak. |
| `src/Dobby.Shell/ShellViewModel.cs` | deep | Current minimal top-navigation ViewModel to extend. |
| `src/Dobby.Shell/ShellRouteHost.cs` | deep | Current route activation/fallback wrapper. |
| `src/Dobby.Shell/WorkSessions/WorkSessionRegistry.cs` | deep | Current minimal work-session registry to extend. |
| `src/Dobby.UI/ThemeTokens.cs` | deep | Current token key surface; Sprint 7 must expand without one-off hardcoding. |
| `src/Dobby.UI/Themes/DesignTokens.xaml` | deep | Current radius/spacing/motion token dictionary. |
| `src/Dobby.UI/Themes/LightTheme.xaml` | deep | Current light brush dictionary. |
| `src/Dobby.UI/Themes/DarkTheme.xaml` | deep | Current dark brush dictionary; only partial token coverage exists. |
| `src/Dobby.Localization/HostResourceKeys.cs` | deep | Existing host localization key constants; Sprint 7 must add keys centrally. |
| `src/Dobby.Localization/HostStringCatalog.cs` | deep | Current `az-AZ` string catalog and missing-key placeholder behavior. |
| `src/Dobby.Localization/Resources/Host.az-AZ.resx` | deep | Current host resource file only covers nav keys. |
| `src/Dobby.Host/App.xaml` | deep | Current app merges design tokens and light theme only. |
| `src/Dobby.Host/App.xaml.cs` | deep | Current login-to-`MainWindow` wiring. |
| `src/Dobby.Host/MainWindow.xaml` | deep | Current placeholder post-login UI with minimum 1024x680 window. |
| `src/Dobby.Host/MainWindow.xaml.cs` | deep | Current post-login session checkpoint, password change, and manual lock code-behind. |
| `src/Dobby.Host/Login/HostLoginRuntime.cs` | deep | Current runtime aggregate includes Sprint 6 settings/authorization sources but no shell runtime model. |
| `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs` | deep | Current composition point for post-login services, settings sources, audit, diagnostics, and background operations. |
| `src/Dobby.Host/Settings/HostConfigurationSettingsPreferenceSource.cs` | deep | Existing `HostConfiguration`-backed theme and auto-lock preference persistence currently uses global `settings.*` keys; Sprint 7 must add authenticated-user scoping for shell view preference keys without schema widening. |
| `src/Dobby.Host/Settings/SettingsNavigationModel.cs` | deep | Existing Sprint 6 Settings category model and User Management permission gate. |
| `src/Dobby.Host/Settings/AppearanceSettingsModel.cs` | deep | Existing theme setting model sharing `ISettingsPreferenceSource`. |
| `src/Dobby.Host/Settings/AccountSettingsModel.cs` | targeted | Existing password-change and saved-login shortcuts; no lock/logout duplicate in Settings. |
| `src/Dobby.Host/Settings/SecuritySessionSettingsModel.cs` | targeted | Existing auto-lock settings source behavior. |
| `src/Dobby.Host/Settings/AboutSettingsModel.cs` | targeted | Existing About model excludes license import/status. |
| `src/Dobby.Host/Settings/AboutInfoProvider.cs` | targeted | Existing About fields and managed-user neutral Owner display behavior. |
| `src/Dobby.Host/Settings/AboutInfo.cs` | targeted | Existing About data shape. |
| `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs` | targeted | Existing `HostConfiguration`, `AuditLog`, session, permissions, and user table shapes; no schema edit planned. |
| `src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs` | targeted | Existing non-disclosing authorization policy. |
| `src/Dobby.Security/Authorization/BuiltInOperationTargets.cs` | targeted | Existing built-in `plugins.license`, `plugins.management`, and `users.management` targets. |
| `src/Dobby.Security/Authorization/OperationTargetRegistry.cs` | targeted | Existing registry and plugin-target registration boundary. |
| `src/Dobby.Security/Authorization/OperationKey.cs` | targeted | Existing operation key parser semantics. |
| `src/Dobby.Messaging/UiDispatch/IUiDispatcher.cs` | targeted | Existing UI dispatcher abstraction for WPF-thread boundary. |
| `src/Dobby.Host/Dobby.Host.csproj` | targeted | Existing Host package/project references and WPF executable TFM. |
| `src/Dobby.Shell/Dobby.Shell.csproj` | targeted | Existing Shell project refs; currently no Security/Diagnostics project ref. |
| `src/Dobby.UI/Dobby.UI.csproj` | targeted | Existing UI package refs and TFM. |
| `src/Dobby.Localization/Dobby.Localization.csproj` | targeted | Existing localization package and Core-only project reference. |
| `src/Dobby.Navigation/Dobby.Navigation.csproj` | targeted | Existing Navigation project refs. |
| `src/Dobby.Diagnostics/Dobby.Diagnostics.csproj` | targeted | Existing Diagnostics project refs. |

### Tests
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `tests/Dobby.UnitTests/Dobby.UnitTests.csproj` | targeted | Existing WPF-enabled unit test project and references. |
| `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj` | targeted | Existing WPF-enabled integration test project, `Verify.Xunit`, and Host refs. |
| `tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj` | targeted | Existing WPF-enabled smoke project and Shell refs. |
| `tests/Dobby.SmokeTests/ShellSmokeTests.cs` | deep | Current shell smoke only verifies minimal top navigation. |
| `tests/Dobby.UnitTests/Navigation/NavigationTests.cs` | deep | Current route fallback and built-in route tests. |
| `tests/Dobby.UnitTests/Shell/WorkSessionRegistryTests.cs` | deep | Current minimal work-session explicit minimize/restore test. |
| `tests/Dobby.UnitTests/Localization/SettingsLocalizationStringCatalogTests.cs` | targeted | Current localization coverage for Sprint 6 Settings keys. |
| `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs` | deep | Current Sprint 6 settings/About source behavior baseline. |
| `tests/Dobby.UnitTests/Host/AboutInfoProviderTests.cs` | deep | Current About provider field/privacy baseline; rendered AC-035 evidence must prove the provider field set appears in Settings/About UI without license import/status exposure. |
| `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs` | deep | Confirms existing preferences persist through `HostConfiguration` without new schema. |
| `tests/Dobby.IntegrationTests/Host/HostLoginRuntimeFactoryTests.cs` | deep | Current Host runtime composition and background operation reporting tests. |
| `tests/Dobby.IntegrationTests/Security/OperationAuthorizationIntegrationTests.cs` | targeted | Existing non-disclosing authorization and permission-denied audit behavior. |
| `tests/Dobby.IntegrationTests/Diagnostics/DiagnosticsExportBoundaryTests.cs` | deep | Existing diagnostics export redaction/no-upload boundary. |
| `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` | deep | Enforces architecture map, ErrorCode baseline, namespace, and print-boundary rules. |
| `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs` | deep | Enforces central package management and package approval boundaries. |
| `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs` | deep | Enforces UTF-8 no-BOM for repo text files. |
| `tests/Dobby.IntegrationTests/Architecture/ReleaseEvidenceTests.cs` | targeted | Existing release-evidence artifact presence tests. |
| `tests/Dobby.IntegrationTests/Architecture/Sprint6ReleaseEvidenceTests.cs` | deep | Existing prior-sprint release-evidence closure pattern. |
| `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs` | targeted | Existing Contracts/SDK/PluginFramework boundary guard. |
| `tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs` | deep | Enforces plugin-facing Contracts no-WPF/no-extra-Core surface. |

### Project / config / build files
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `global.json` | targeted | SDK pin. |
| `Directory.Packages.props` | targeted | Central package pins and no-new-pin boundary. |
| `Directory.Build.props` | targeted | Zero-warning and deterministic build constraints. |
| `.editorconfig` | targeted | Encoding and formatting constraints. |
| `Dobby.sln` | targeted | Project membership for build/test closure. |
| `README.md` | targeted | Local command references. |

### Release / evidence artifacts
| Source | Depth | Why it matters / how it affected the plan |
|---|---:|---|
| `artifacts/release-evidence/skeleton-local/architecture-baseline.md` | deep | Current implementation/evidence baseline, Sprint 6 closure, 487-test baseline, and no-new-pins/no-new-schema record. |
| `artifacts/release-evidence/skeleton-local/decision-log.md` | deep | Closed Sprint 6 decisions `D-20` and `D-21`; closed Sprint 7 package approval `HD-S7-001` / `Q1`. |
| `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` | targeted | Approved package pin evidence, Sprint 7 package approval evidence record, and package-governance baseline. |
| `artifacts/release-evidence/skeleton-local/storage-materialization.md` | targeted | Host storage materialization and schema-widening gate context. |

## 3. Sprint Scope From Master Sprint Plan
- Master-plan goal quote: “Build the authenticated WPF shell and user-facing host experience required for P1.” (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Goal).
- Master-plan task groups:
  - Shell Composition: WPF/WPF-UI shell layout, documented shell surfaces, profile menu, notifications, feedback states, minimum window sizing, status/progress areas, and authenticated user/Owner diagnostics export action entry point (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Scope; §Task Groups).
  - Navigation: stable route identities, Home fallback, `TopToolbar`/`Sidebar`/`Third Bar` hierarchy, group/weight/tiebreak ordering, fixed built-ins, and user override rules (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Task Groups; `docs/specs/shell-ui.md` §Navigation Model).
  - Theme and Design System: light/dark themes, single `TopToolbar` light/dark switch, high-contrast/reduced-motion, spacing/radius/typography/icon rules (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Task Groups; `docs/specs/design-system-assets.md`).
  - Localization: centralized strings, default `az-AZ`, fallback chain, optional `en-US`/`tr-TR` skeletons, missing-key diagnostics, Azerbaijani diacritic handling, no runtime language switching (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Task Groups; `docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`).
  - Work Sessions: explicit minimize/restore, no silent auto-minimize, navigation warning choices, controlled close/cancel, restart boundary, logout/lock/plugin lifecycle behavior, minimized item metadata (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Task Groups; `docs/specs/work-sessions.md`).
- Acceptance / exit expectations and AC ownership:
  - Primary: `AC-003`, `AC-016`, `AC-017`, `AC-019`, `AC-023`, `AC-027`, `AC-035`, `AC-040`, `AC-048` (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Acceptance and exit expectations).
  - AC-035 primary: rendered Settings tree, TopToolbar/Appearance theme interaction, snapshot evidence, manual evidence; Sprint 6 contributes source behavior (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Acceptance and exit expectations; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6 / Acceptance and exit expectations).
  - AC-033 contribution: authenticated shell action surface for explicit diagnostics export trigger; Sprint 3 owns diagnostics package, redaction, audit, and no-telemetry boundary (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Acceptance and exit expectations; `docs/specs/audit-and-diagnostics.md`).
  - `AC-011`, `AC-012`, and `AC-013` are P1-if-time/non-blocking and do not block P1-core (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Acceptance and exit expectations).
- Dependencies: Sprints 2-6, plus `docs/adr/ADR-0005-ui-ordering-model.md`, `docs/adr/ADR-0013-routing-and-deep-link-model.md`, `docs/adr/ADR-0015-background-operation-reporting.md`, `docs/adr/ADR-0028-shell-composition-ordering-render.md`, `docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`, `docs/adr/ADR-0044-localization-resource-organization.md`, `docs/adr/ADR-0046-ui-and-code-terminology-mapping.md`, `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`, `docs/adr/ADR-0050-in-memory-work-sessions.md`, `docs/specs/design-system-assets.md`, `docs/specs/shell-ui.md`, and `docs/specs/work-sessions.md` (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Dependencies).
- Later sprints depending on this sprint: Sprint 8 depends on shell route/page hosting and post-login activation rules; Sprint 11 consolidates AC-008 smoke using Sprint 5, Sprint 7, and Sprint 8 evidence (`SPRINT_IMPLEMENTATION_PLAN.md` §Cross-Sprint Dependency Notes; §Sprint 11 / Verification / evidence).
- Master-plan explicit out-of-scope items relevant here:
  - Work sessions are not restored after restart (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Acceptance and exit expectations).
  - Plugin page contracts do not expose WPF types (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Acceptance and exit expectations; `tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs`).
  - Runtime language switching is out of P1 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Acceptance and exit expectations; `docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`).
  - Richer Home/favorites polish and expanded shared UI controls are P1-if-time/later, not Sprint 7 required closure (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Risks / Blockers; `docs/02_ARCHITECTURE_MAP.md` Future Scope).

## 4. Prior Sprint Continuity Check
| Prior sprint | Verified present in current repo | Missing / carry-over visible to Sprint 7 | Readiness effect |
|---|---|---|---|
| Sprint 1 | Baseline project/test architecture exists in `Dobby.sln`; package governance and architecture tests exist in `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs` and `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`. | Temporary `SPRINT1-INFRA-APP-PRINT-BOUNDARY` remains a later Sprint 9 closure item, not Sprint 7 work (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`). | No Sprint 7 blocker. |
| Sprint 2 | Shared result, contracts, ordering, background operation, plugin page, and policy primitives exist in `src/Dobby.Core/Result.cs`, `src/Dobby.Contracts/BackgroundOperations/BackgroundOperationContracts.cs`, `src/Dobby.Contracts/PluginPages/PluginPageContracts.cs`, and `src/Dobby.Contracts/Policies/PolicyContracts.cs`. | Plugin runtime/page activation remains later; Contracts must not gain WPF types (`docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`; `tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs`). | No Sprint 7 blocker. |
| Sprint 3 | Diagnostics export builder/redaction and audit event taxonomy exist in `src/Dobby.Diagnostics/DiagnosticsExportPackageBuilder.cs`, `src/Dobby.Diagnostics/DiagnosticRedactor.cs`, `src/Dobby.Audit/AuditEventName.cs`, and `tests/Dobby.IntegrationTests/Diagnostics/DiagnosticsExportBoundaryTests.cs`. | Sprint 7 owns only authenticated shell trigger surface for export; package/redaction/no-telemetry remain Sprint 3-owned boundaries (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Acceptance and exit expectations). | No Sprint 7 blocker. |
| Sprint 4 | Protected storage schema and `HostConfiguration` table exist in `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; settings preferences persist through `HostConfiguration` without new schema in `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`. | Required Sprint 7 shell UI preferences must use existing `HostConfiguration` key/value backing; schema widening for shell preference state is not pre-approved and is a Human decision required gate (`artifacts/release-evidence/skeleton-local/storage-materialization.md`; `SPRINT_6_EXECUTION_PLAN.md` §7a A5/A6). | Ready with Assumption A3 and schema stop gate. |
| Sprint 5 | Auth/session/post-login runtime exists in `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`, `src/Dobby.Host/MainWindow.xaml.cs`, and session lifecycle tests/evidence (`SPRINT_5_EXECUTION_PLAN.md`; `artifacts/release-evidence/skeleton-local/architecture-baseline.md`). | Admin-generated production recovery evidence remains Sprint 10/11; not Sprint 7 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 5 / Acceptance and exit expectations; §Sprint 10; §Sprint 11). | No Sprint 7 blocker. |
| Sprint 6 | Operation authorization, Settings source models, About source models, and preference source exist in `src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs`, `src/Dobby.Host/Settings/SettingsNavigationModel.cs`, `src/Dobby.Host/Settings/AppearanceSettingsModel.cs`, `src/Dobby.Host/Settings/AboutInfoProvider.cs`, and `src/Dobby.Host/Settings/HostConfigurationSettingsPreferenceSource.cs`. Sprint 6 evidence records implemented/verified status with 487 tests (`artifacts/release-evidence/skeleton-local/architecture-baseline.md` §Sprint 6 Implementation Evidence). | Rendered Settings tree, TopToolbar/Appearance theme interaction, snapshot evidence, and manual evidence are explicitly Sprint 7 work (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6 / Acceptance and exit expectations; §Sprint 7 / Acceptance and exit expectations). | Ready; Sprint 7 consumes source behavior rather than redoing Sprint 6. |

Carry-over blockers, decisions, and risks:
- `D-20` built-in User Management target id and `D-21` Settings namespace/preference authority are closed and recorded in `artifacts/release-evidence/skeleton-local/decision-log.md`; Sprint 7 must use them as constraints.
- No open predecessor decision blocks Sprint 7 planning. Conditional gates remain for new package pins, schema widening, new Core `ErrorCode` values, new audit events, new plugin-facing contracts, or scope movement across sprints (`Directory.Packages.props`; `docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`).

Readiness conclusion: predecessor outputs required for planning are present in current code/tests/evidence, and implementation may start using the closed gates and implementation assumptions in §7a/§13.

## 5. Current Codebase State
| Area | Current files / surfaces | Sprint 7 implication |
|---|---|---|
| Authenticated window | `src/Dobby.Host/App.xaml.cs` opens `LoginWindow`, then `src/Dobby.Host/MainWindow.xaml`; `src/Dobby.Host/MainWindow.xaml` is a placeholder account panel with `MinWidth=1024` and `MinHeight=680`. | Sprint 7 must replace the post-login placeholder with shell surfaces while preserving session checkpoint/password/lock behavior from `src/Dobby.Host/MainWindow.xaml.cs` and `docs/specs/session-lifecycle.md`. |
| Shell project | `src/Dobby.Shell/ShellViewModel.cs`, `src/Dobby.Shell/ShellRouteHost.cs`, `src/Dobby.Shell/WorkSessions/WorkSessionRegistry.cs`, and `src/Dobby.Shell/Dobby.Shell.csproj`. | Shell composition state, navigation model, work-session lifecycle, and status/progress models should extend this project unless an assumption is rejected (§7a A1/A2/A6). |
| Navigation project | `src/Dobby.Navigation/RouteRegistry.cs` has built-ins `Dobby.Shell.Navigation.Home`, `Dobby.Shell.Navigation.Plugins`, `Dobby.Shell.Navigation.Settings`; `src/Dobby.Navigation/DefaultUiOrderingComparer.cs` implements group/weight/plugin/declaration ordering. | Sprint 7 must add hierarchy/user override/fallback coverage without changing route identities on agent authority (§7a A2). |
| Localization project | `src/Dobby.Localization/HostResourceKeys.cs`, `src/Dobby.Localization/HostStringCatalog.cs`, and `src/Dobby.Localization/Resources/Host.az-AZ.resx` exist. | Sprint 7 must centralize visible shell strings, consolidate the current catalog/resource-file split behind the Dobby-owned `IStringLocalizer<T>` adapter path, expand `az-AZ` `.resx` coverage, and add missing-key diagnostics behavior without runtime language switching (`docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`; `docs/03_TECH_STACK_AND_VERSIONS.md`). |
| Theme/UI project | `src/Dobby.UI/ThemeTokens.cs` and theme dictionaries under `src/Dobby.UI/Themes/` exist, but token coverage is minimal. | Sprint 7 must expand tokens and switch light/dark dictionaries using existing WPF-UI/CommunityToolkit pins only (`docs/specs/design-system-assets.md`; `Directory.Packages.props`). |
| Settings source behavior | Sprint 6 source models exist under `src/Dobby.Host/Settings/` and are covered by `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs` and `tests/Dobby.UnitTests/Host/AboutInfoProviderTests.cs`. | Sprint 7 must render these surfaces and add AC-035 snapshot/manual evidence, not re-author the source policy decisions. |
| Preferences | `src/Dobby.Configuration/SettingsPreferences.cs` currently includes `AppearancePreference` and `SecuritySessionPreference`; `src/Dobby.Configuration/ISettingsPreferenceSource.cs` exposes parameterless `Load`/`Save`; Host persistence backs theme and auto-lock keys through global `settings.*` keys in `src/Dobby.Host/Settings/HostConfigurationSettingsPreferenceSource.cs`. | Sprint 7 must add typed shell view preference state and authenticated-user-scoped HostConfiguration-backed key/value persistence for AC-016/AC-023 shell preferences while stopping before schema widening, Host-owned durable preference storage, or global shell preference keys that can collide across users. |
| Diagnostics export | `src/Dobby.Diagnostics/DiagnosticsExportPackageBuilder.cs` builds local zip packages and `src/Dobby.Audit/AuditEventName.cs` contains `DiagnosticsExportCreated`. | Sprint 7 must add authenticated shell action wiring, not rework export package semantics (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / AC-033 contribution). |
| Background operations | `src/Dobby.Contracts/BackgroundOperations/BackgroundOperationContracts.cs` defines statuses and snapshots; `src/Dobby.Application/BackgroundOperationReporter.cs` adapts progress. | Sprint 7 shell bottom status/progress should consume this existing contract and preserve `ADR-0015` no-ad-hoc-threading rules. |
| Plugin page boundary | `src/Dobby.Contracts/PluginPages/PluginPageContracts.cs` has model/descriptor contracts without WPF types; `docs/schemas/dobby-plugin-manifest.schema.json` has extension-point target vocabulary. | Sprint 7 may prepare host shell regions but must not activate plugins, validate packages, or expose WPF through Contracts. |

Mismatches and risks the target sprint must address or avoid:
- `src/Dobby.Host/MainWindow.xaml` currently contains hardcoded visible strings and no shell surfaces; Sprint 7 must localize visible strings and render the required shell (`docs/specs/shell-ui.md`; `docs/07_ACCEPTANCE_CRITERIA.md` AC-003/AC-017).
- `src/Dobby.Navigation/RouteRegistry.cs` current route key constants differ from ADR-0013 example route names such as `host/home`; current code/tests use the existing constants, so renaming is a routed-state migration decision and must not happen without authority (§7a A2; `tests/Dobby.UnitTests/Navigation/NavigationTests.cs`; `docs/adr/ADR-0013-routing-and-deep-link-model.md`).
- `src/Dobby.Localization/HostStringCatalog.cs` currently holds in-memory `az-AZ` strings, returns `[[key]]` for missing keys, does not yet consume `IStringLocalizer<T>` / `.resx` as the runtime authority, and does not yet emit `localization.missing-key`; Sprint 7 must consolidate the catalog with `Host.az-AZ.resx` through the Dobby adapter and align missing-key behavior with diagnostics without audit event invention (`docs/specs/audit-and-diagnostics.md`; `src/Dobby.Diagnostics/LocalizationMissingKeyEvent.cs`; `docs/03_TECH_STACK_AND_VERSIONS.md`).
- `src/Dobby.UI/Themes/DarkTheme.xaml` implements only a subset of the Solarized Dark token mapping documented in `docs/specs/design-system-assets.md`; Sprint 7 must expand tokens rather than hardcode per-screen colors.

## 6. Documentation and ADR Alignment
- Documents fully aligned for target-sprint planning with no planning-time edits:
  - `docs/specs/shell-ui.md` aligns with shell surfaces, minimum window size, navigation hierarchy, theme/localization, status/progress, and verification.
  - `docs/specs/work-sessions.md` aligns with in-memory process-lifetime work-session behavior, explicit minimize, close/cancel, logout/lock, and restart boundary.
  - `docs/specs/design-system-assets.md` aligns with theme token and WPF-UI implementation constraints; `docs/assets/ui_design_light_theme.jpg` is boundary/context only, with text specs remaining normative for measurements, tokens, labels, defaults, and scope.
  - `docs/specs/settings-and-about.md` aligns with rendered Settings tree, About privacy fields, and Settings/About license boundary.
  - `docs/specs/audit-and-diagnostics.md` aligns with diagnostics export action, no telemetry, redaction, and missing-key diagnostics.
- Documents requiring only implementation-time evidence appends:
  - `artifacts/release-evidence/skeleton-local/architecture-baseline.md` should receive Sprint 7 implementation evidence, UI snapshot/manual evidence, no-new-pins/no-new-schema/no-new-Contracts statements, and final build/test/format results.
  - `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` contains the closed `HD-S7-001` / `Q1` Sprint 7 package approval evidence; do not edit it during implementation unless a separate package/source/native asset decision is approved (`tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`).
- Documents that may need an update note after implementation: none planned for the current file plan. This plan keeps `ShellThemeCoordinator.cs` and `ShellDiagnosticsExportCoordinator.cs` in the existing flat `Dobby.Host` namespace; if implementation needs `Dobby.Host.Shell`, `Dobby.Host.Diagnostics`, or another new public/plugin-visible or skeleton-critical Host namespace placement, stop and record Human decision required for a `docs/08_CODING_STANDARDS.md` §5 table row or explicit docs amendment before creating it (`SPRINT_IMPLEMENTATION_PLAN.md` Core Rules; `docs/08_CODING_STANDARDS.md`).

ADR alignment classification:
| ADR | Classification | Evidence |
|---|---|---|
| `ADR-0005` | Aligned via existing comparer and planned hierarchy tests. | `src/Dobby.Navigation/DefaultUiOrderingComparer.cs`; `tests/Dobby.UnitTests/Navigation/NavigationTests.cs`. |
| `ADR-0013` | Aligned with route fallback; route-key spelling mismatch tracked as Assumption A2. | `src/Dobby.Navigation/RouteRegistry.cs`; `docs/adr/ADR-0013-routing-and-deep-link-model.md`. |
| `ADR-0015` | Aligned by consuming existing background operation contracts and avoiding ViewModel direct thread work. | `src/Dobby.Contracts/BackgroundOperations/BackgroundOperationContracts.cs`; `docs/adr/ADR-0015-background-operation-reporting.md`. |
| `ADR-0019` | Aligned: diagnostics export and localization missing-key remain diagnostics/audit-separated. | `docs/specs/audit-and-diagnostics.md`; `src/Dobby.Audit/AuditEventName.cs`. |
| `ADR-0028` | Aligned: shell surfaces/profile/notification/render ordering are Sprint 7 implementation targets. | `docs/adr/ADR-0028-shell-composition-ordering-render.md`; `docs/specs/shell-ui.md`. |
| `ADR-0031` | Aligned: logout/lock/session behavior consumed by work-session lifecycle. | `docs/specs/session-lifecycle.md`; `docs/specs/work-sessions.md`. |
| `ADR-0041` | Aligned: no pre-login plugin activation/heavy work; shell first paint after authentication. | `src/Dobby.Host/App.xaml.cs`; `src/Dobby.Diagnostics/StartupMilestoneNames.cs`. |
| `ADR-0043` | Aligned: default `az-AZ`, fallback diagnostics, no runtime language switch. | `src/Dobby.Localization/HostStringCatalog.cs`; `docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`. |
| `ADR-0044` | Aligned with host resources under `Dobby.Localization`; §8/§12 require catalog/`Host.az-AZ.resx` consolidation through the Dobby-owned `IStringLocalizer<T>` adapter path. | `src/Dobby.Localization/Dobby.Localization.csproj`; `docs/adr/ADR-0044-localization-resource-organization.md`; `docs/03_TECH_STACK_AND_VERSIONS.md`. |
| `ADR-0046` | Aligned: shell plan keeps canonical terms and Azerbaijani UI resource ownership. | `docs/adr/ADR-0046-ui-and-code-terminology-mapping.md`; `docs/specs/shell-ui.md`. |
| `ADR-0048` | No new action required beyond consuming existing authorization policy for protected rendered routes/actions. | `src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs`; `src/Dobby.Security/Authorization/BuiltInOperationTargets.cs`. |
| `ADR-0049` | Aligned: Sprint 7 owns hierarchical shell descriptors/regions but not plugin activation. | `src/Dobby.Contracts/PluginPages/PluginPageContracts.cs`; `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`. |
| `ADR-0050` | Aligned: work-session lifecycle implementation is a primary Sprint 7 target. | `src/Dobby.Shell/WorkSessions/WorkSessionRegistry.cs`; `docs/adr/ADR-0050-in-memory-work-sessions.md`. |

Specs and schemas alignment:
- `docs/schemas/dobby-plugin-manifest.schema.json` already defines `extensionPoints.target` closed vocabulary and route/resource key shapes; Sprint 7 must not edit the schema or implement package validation (`docs/adr/ADR-0036-plugin-manifest-schema.md`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8).
- No storage schema update is planned; existing `HostConfiguration` table is the mandatory durable backing for Sprint 7 shell UI preferences unless implementation proves the current authority insufficient and records Human decision required before schema/preference-authority changes (`src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`).

Net documentation footprint summary: no docs/ADRs/specs/schemas/master plan/predecessor plan edits are part of this planning artifact. Implementation-time release evidence appends are expected; source documentation edits require a separate authority decision, including any new Host namespace-placement authority not already covered by this plan.

## 7. Files to Create
Naming rule: every new file must map to a documented role in `SPRINT_IMPLEMENTATION_PLAN.md`, an ADR/spec/schema, current architecture map row, current code namespace, or an accepted implementation assumption in §7a. If implementation discovers an unavoidable new name not covered here, stop and record it as an Implementation assumption or Human decision required before creating the file.

### Production files
| Placement | Filename | Purpose | Source basis | Associated tests | Boundary / architecture restrictions |
|---|---|---|---|---|---|
| `src/Dobby.Shell/Layout/` | `ShellLayoutState.cs` | Host-owned shell state model for `TopToolbar`, `Sidebar`, `Third Bar`, main content, bottom status/progress, profile state, notification taxonomy state, screen feedback states, and minimized strip metadata. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Scope; `docs/specs/shell-ui.md`; Assumption A1. | `tests/Dobby.UnitTests/Shell/ShellLayoutStateTests.cs`; shell snapshot tests. | Must not reference Host internals, Diagnostics package builder, or Security services unless allowed by `docs/02_ARCHITECTURE_MAP.md`. |
| `src/Dobby.Navigation/` | `NavigationContribution.cs` | Navigation-owned contribution descriptor model for hierarchy targets, parent relationships, group/weight/declaration index, fixed built-in positions, and user override inputs. | `docs/02_ARCHITECTURE_MAP.md` Row 51; `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`; `docs/specs/shell-ui.md`; Assumption A2. | `tests/Dobby.UnitTests/Navigation/NavigationComposerTests.cs`. | Must stay descriptor/model-only; no WPF types, no Host services, no plugin activation/package validation. |
| `src/Dobby.Navigation/` | `NavigationComposer.cs` | Compose built-ins and future validated descriptor inputs into ordered `TopToolbar`/`Sidebar`/`Third Bar` navigation model with fallback to Home. | `docs/02_ARCHITECTURE_MAP.md` Row 51; `docs/adr/ADR-0005-ui-ordering-model.md`; `docs/adr/ADR-0013-routing-and-deep-link-model.md`; `src/Dobby.Navigation/DefaultUiOrderingComparer.cs`; Assumption A2. | `tests/Dobby.UnitTests/Navigation/NavigationComposerTests.cs`; `tests/Dobby.UnitTests/Navigation/NavigationTests.cs`. | `Dobby.Shell` renders/hosts the composed model; `Dobby.Navigation` owns descriptor normalization/composition. |
| `src/Dobby.Configuration/` | `ShellViewPreference.cs` | Typed per-user shell preference model for sidebar collapse state, Sidebar open node state, Sidebar/Third Bar ordering, TopToolbar plugin ordering/hidden state, window size/position, and last active module storage-without-startup-restore. | `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-010/FR-019; `docs/07_ACCEPTANCE_CRITERIA.md` AC-016/AC-023; `docs/02_ARCHITECTURE_MAP.md` Row 46; Assumption A3. | `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs`; `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`. | Must remain `Dobby.Configuration`-owned typed preference state; concrete `HostConfiguration` persistence remains Host-wired. |
| `src/Dobby.Configuration/` | `SettingsPreferenceScope.cs` | Typed authenticated user preference scope used to derive a stable non-PII per-user key namespace for Sprint 7 shell preference persistence. | `docs/specs/shell-ui.md` §Per-user shell state; `docs/02_ARCHITECTURE_MAP.md` Row 46; `docs/07_ACCEPTANCE_CRITERIA.md` AC-016/AC-023; Assumption A3. | `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs`; `tests/Dobby.IntegrationTests/Host/ShellPreferenceUserIsolationTests.cs`; `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`. | Scope type must not expose table access or PII-bearing raw identifiers; Host wires the authenticated user scope to existing `HostConfiguration` key/value rows without schema widening. |
| `src/Dobby.Host/` | `ShellThemeCoordinator.cs` | Host-owned coordination between the TopToolbar light/dark switch, existing `AppearanceSettingsModel`, `ISettingsPreferenceSource`, and WPF resource dictionary switching. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Theme and Design System; `src/Dobby.Configuration/AppThemePreference.cs`; `src/Dobby.Host/Settings/AppearanceSettingsModel.cs`; Assumption A3. | `tests/Dobby.UnitTests/Host/ShellThemePreferenceTests.cs`; `tests/Dobby.IntegrationTests/Host/ShellThemeAndSettingsIntegrationTests.cs`. | Must use the existing flat `Dobby.Host` namespace; must not create `Dobby.Host.Shell` without coding-standards namespace-placement authority; must not add `Dobby.Shell -> Dobby.Configuration`; Shell-facing state/commands stay configuration-neutral, and new theme values require Human decision required. |
| `src/Dobby.Shell/WorkSessions/` | `WorkSessionCloseCoordinator.cs` | Model navigation/logout/close warning choices, controlled close/cancel, minimized item metadata, and in-process restore state around existing registry. | `docs/specs/work-sessions.md`; `docs/adr/ADR-0050-in-memory-work-sessions.md`; Assumption A6. | `tests/Dobby.UnitTests/Shell/WorkSessionLifecycleTests.cs`; `tests/Dobby.IntegrationTests/Host/WorkSessionShellIntegrationTests.cs`. | Must keep sessions process-lifetime only; no restart restore persistence. |
| `src/Dobby.Host/` | `ShellDiagnosticsExportCoordinator.cs` | Host-mediated authenticated shell action that invokes existing diagnostics export builder and records audit event. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / AC-033 contribution; `docs/specs/audit-and-diagnostics.md`; `src/Dobby.Diagnostics/DiagnosticsExportPackageBuilder.cs`; Assumption A4. | `tests/Dobby.IntegrationTests/Host/ShellDiagnosticsExportActionTests.cs`; `tests/Dobby.IntegrationTests/Diagnostics/DiagnosticsExportBoundaryTests.cs`. | Must use the existing flat `Dobby.Host` namespace; must not create `Dobby.Host.Diagnostics` without coding-standards namespace-placement authority; must not add vendor upload/telemetry; file-picker/clipboard/OS actions stay Host adapters, not ViewModel direct APIs. |
| `src/Dobby.Shell/UiDispatch/` | `WpfUiDispatcher.cs` | WPF implementation of existing UI dispatcher abstraction wired through the documented Shell messaging UI bridge for shell/status updates. | `src/Dobby.Messaging/UiDispatch/IUiDispatcher.cs`; `docs/02_ARCHITECTURE_MAP.md` Row 55; `docs/03_TECH_STACK_AND_VERSIONS.md` UI dispatcher row; `docs/adr/ADR-0015-background-operation-reporting.md`; Assumption A7. | `tests/Dobby.UnitTests/Shell/WpfUiDispatcherBoundaryTests.cs` if implementation exposes testable behavior. | Shell-wired WPF adapter; do not add WPF dependency to Contracts/SDK or non-Windows projects. |

### Test files
| Placement | Filename | Purpose | Source basis | Boundary / architecture restrictions |
|---|---|---|---|---|
| `tests/Dobby.UnitTests/Shell/` | `ShellLayoutStateTests.cs` | Verify required shell surface model is present and stable before WPF rendering, including profile menu inventory metadata, Toast/Banner/Modal notification taxonomy, and loading/empty/success/error/confirmation feedback-state metadata. | `docs/specs/shell-ui.md`; `docs/07_ACCEPTANCE_CRITERIA.md` AC-003/AC-027. | Avoid brittle visual pixel claims; leave rendered snapshots to integration/smoke evidence. |
| `tests/Dobby.UnitTests/Navigation/` | `NavigationComposerTests.cs` | Verify Home first, Settings last, plugin/built-in ordering, hierarchy, user override reset, invalid route fallback, and `Dobby.Navigation` ownership of descriptor composition. | `docs/adr/ADR-0005-ui-ordering-model.md`; `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`; AC-023. | Do not require plugin activation or manifest validation. |
| `tests/Dobby.UnitTests/Host/` | `ShellThemePreferenceTests.cs` | Verify the Host-owned TopToolbar theme coordinator writes the same preference consumed by Settings Appearance without adding `Dobby.Shell -> Dobby.Configuration`. | `docs/specs/settings-and-about.md`; `src/Dobby.Host/Settings/AppearanceSettingsModel.cs`; AC-016/AC-035. | Must use existing `AppThemePreference`. |
| `tests/Dobby.UnitTests/Shell/` | `WorkSessionLifecycleTests.cs` | Verify explicit minimize/restore, warning choices, controlled close/cancel, lock/logout behavior, restart non-restore state, and progress/result/error/input/screen state retained across explicit minimize/restore. | `docs/specs/work-sessions.md`; AC-019. | Must not introduce durable work-session restore. |
| `tests/Dobby.UnitTests/Localization/` | `ShellLocalizationCoverageTests.cs` | Verify visible shell/profile/work-session/settings strings have centralized keys, `az-AZ` values, `HostResourceKeys` / `Host.az-AZ.resx` parity, and Dobby-owned `IStringLocalizer<T>` adapter lookup/fallback behavior. | `docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`; `docs/adr/ADR-0044-localization-resource-organization.md`; `docs/03_TECH_STACK_AND_VERSIONS.md`; AC-017. | Runtime language switching is not required. |
| `tests/Dobby.IntegrationTests/Host/` | `ShellPreferenceUserIsolationTests.cs` | Verify two authenticated user scopes can persist different shell view preferences through existing `HostConfiguration` rows without collision or fallback to global shell keys. | `docs/specs/shell-ui.md` §Per-user shell state; `docs/07_ACCEPTANCE_CRITERIA.md` AC-016/AC-023; Assumption A3. | No schema widening; do not require unrelated user-management UI work. |
| `tests/Dobby.IntegrationTests/Host/` | `ShellThemeAndSettingsIntegrationTests.cs` | Verify rendered TopToolbar switch and Settings Appearance write/read the same persisted preference through `HostConfiguration`. | `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`; AC-016/AC-035. | No schema widening unless approved. |
| `tests/Dobby.IntegrationTests/Host/` | `WorkSessionShellIntegrationTests.cs` | Verify navigation warning choices, lock preserves sessions, logout close/cancel cleanup, progress/result/error/input/screen state retained through shell restore, and plugin disable placeholder behavior without activation. | `docs/specs/work-sessions.md`; `docs/07_ACCEPTANCE_CRITERIA.md` AC-019/AC-003. | Must not require plugin activation/package validation or durable work-session restore. |
| `tests/Dobby.IntegrationTests/Host/` | `ShellDiagnosticsExportActionTests.cs` | Verify authenticated shell action invokes local diagnostics export, excludes secrets, does not upload, and audits `diagnostics.export.created`. | `docs/specs/audit-and-diagnostics.md`; `src/Dobby.Audit/AuditEventName.cs`; AC-033. | Must not create new audit event names. |
| `tests/Dobby.IntegrationTests/Host/` | `AzerbaijaniDiacriticsRoundTripTests.cs` | Verify Azerbaijani diacritics round-trip across UI-facing input/display, storage, and audit-supported payloads. | `docs/07_ACCEPTANCE_CRITERIA.md` AC-048; `tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs`. | Identifier comparisons remain ordinal/normalized; do not introduce culture-sensitive IDs. |
| `tests/Dobby.IntegrationTests/Shell/` | `ShellSnapshotTests.cs` | Verify shell snapshots at 1024x680 and 1920x1080 in light/dark modes, including a synthetic nested descriptor tree proving `TopToolbar`/`Sidebar`/`Third Bar` render hierarchy without plugin activation. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Verification / evidence; `Directory.Packages.props` `Verify.Xunit`; Assumption A5. | No new screenshot package without Human decision required. |
| `tests/Dobby.IntegrationTests/Architecture/` | `ShellUiStaticScanTests.cs` | Verify Sprint 7 shell/built-in WPF surfaces do not introduce hardcoded visible UI strings or shell-level hardcoded color/spacing/font values outside approved constants/resources. | `docs/07_ACCEPTANCE_CRITERIA.md` AC-016/AC-017; `docs/specs/shell-ui.md` §Verification. | Static scan must avoid false positives for resource keys, technical identifiers, tests, and documented constants. |
| `tests/Dobby.SmokeTests/` | `ShellKeyboardSmokeTests.cs` | Verify keyboard-only Home/Plugins/Settings route movement, Sidebar collapse, theme switch, required profile menu inventory, shortcuts, visible focus traversal/focus indicators, and shell route activation smoke. | `docs/specs/shell-ui.md`; AC-027/AC-040. | Smoke tests must avoid package install/activation. |
| `tests/Dobby.IntegrationTests/Architecture/` | `Sprint7ReleaseEvidenceTests.cs` | Verify Sprint 7 implementation evidence block, AC/evidence markers, exact package approval/no-drift markers, localization `.resx` parity evidence, and unchanged test-runner infrastructure markers are present after implementation. | `tests/Dobby.IntegrationTests/Architecture/Sprint6ReleaseEvidenceTests.cs`; `docs/09_BUILD_RUN_AND_RELEASE.md`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`. | Evidence test should check named Sprint 7 markers only, not rewrite evidence. |

## 7a. Implementation assumptions for new files

### Assumption A1 — WPF window remains in `Dobby.Host`, shell state/composition lives in `Dobby.Shell`
- Assumption: keep `src/Dobby.Host/MainWindow.xaml` as the executable post-login WPF window container, while moving shell state/composition models into `src/Dobby.Shell/`.
- Cited source: `src/Dobby.Host/App.xaml.cs` currently constructs `MainWindow`; `src/Dobby.Host/MainWindow.xaml`; `src/Dobby.Shell/ShellViewModel.cs`; `docs/02_ARCHITECTURE_MAP.md` Shell UI Baseline and project dependency rows.
- Risk if wrong: a reviewer may require WPF shell views to move into `Dobby.Shell`, causing namespace/project churn.
- Fallback: stop and request Human decision required for shell view placement before moving window classes or adding project references outside `docs/02_ARCHITECTURE_MAP.md`.

### Assumption A2 — Preserve current built-in route keys unless authority approves migration
- Assumption: keep current built-in route constants from `src/Dobby.Navigation/RouteRegistry.cs` (`Dobby.Shell.Navigation.Home`, `Dobby.Shell.Navigation.Plugins`, `Dobby.Shell.Navigation.Settings`) and treat ADR `host/home` examples as examples, not a Sprint 7 migration mandate.
- Ownership: `Dobby.Navigation` owns descriptor normalization/composition and route fallback model; `Dobby.Shell` owns route hosting/rendered visual state and must not become the owner for navigation composition.
- Cited source: `src/Dobby.Navigation/RouteRegistry.cs`; `tests/Dobby.UnitTests/Navigation/NavigationTests.cs`; `docs/02_ARCHITECTURE_MAP.md` Row 51; `docs/adr/ADR-0013-routing-and-deep-link-model.md`; `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`.
- Risk if wrong: persisted route/user override keys could drift from canonical route identity.
- Fallback: stop before renaming route keys, moving navigation composition ownership out of `Dobby.Navigation`, or writing migration logic; record Human decision required for route identity migration, composition ownership changes, or persisted-state handling.

### Assumption A3 — Existing settings preference authority must carry Sprint 7 shell preferences without schema widening
- Assumption: add typed shell view preference state under `Dobby.Configuration` and persist required AC-016/AC-023 shell preferences through the existing Host-wired `HostConfiguration` key/value backing with an authenticated user scope: sidebar collapse state, Sidebar open node state, Sidebar/Third Bar ordering, TopToolbar plugin ordering/hidden state, window size/position, and last active module storage-without-startup-restore.
- Required scoping strategy: introduce a `Dobby.Configuration` scope value such as `SettingsPreferenceScope` and make the post-login `ISettingsPreferenceSource` instance user-scoped before it is passed to shell/theme/settings coordinators. Because `ISettingsPreferenceSource.Load()` / `Save(...)` are currently parameterless, the implementation must either keep the interface unchanged with an already-scoped source instance or make an explicit interface/scope change in `Dobby.Configuration`; it must not let shell preferences use the current global `settings.*` namespace.
- Keying rule: `HostConfigurationSettingsPreferenceSource` must derive non-PII, stable, authenticated-user-scoped `ConfigKey` values for Sprint 7 shell preferences while preserving the existing `HostConfiguration` table shape. Two different authenticated users must be able to save different shell view preferences without one user's values overwriting, hiding, or resetting the other's values.
- Existing preference clarification: Sprint 7 must user-scope `ShellViewPreference` keys. The TopToolbar/Settings Appearance theme flow must use the same post-login preference source as the rendered shell; if implementation would silently change unrelated Sprint 6 security/session preference semantics, add explicit tests/evidence or stop for Human decision required before broadening that behavior.
- Host placement rule: `ShellThemeCoordinator.cs` belongs at `src/Dobby.Host/ShellThemeCoordinator.cs` with namespace `Dobby.Host`. Do not create `Dobby.Host.Shell` unless `docs/08_CODING_STANDARDS.md` §5 has a table row or explicit docs amendment before creation.
- Cited source: `src/Dobby.Configuration/ISettingsPreferenceSource.cs`; `src/Dobby.Configuration/SettingsPreferences.cs`; `src/Dobby.Host/Settings/HostConfigurationSettingsPreferenceSource.cs`; `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`; `docs/02_ARCHITECTURE_MAP.md` Row 46; `docs/07_ACCEPTANCE_CRITERIA.md` AC-016/AC-023; `artifacts/release-evidence/skeleton-local/decision-log.md` `D-21`.
- Architecture-map conflict note: Sprint 7 must follow the enforced `Dobby.Shell` allowed-reference row and `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`; any reverse-column "referenced by" inconsistency listing `Dobby.Shell` under `Dobby.Configuration` is not permission to add `Dobby.Shell -> Dobby.Configuration`.
- Risk if wrong: required shell preference persistence may remain global and collide across users, or implementation may need new typed preference authority, schema semantics, or a new architecture decision.
- Fallback: stop before editing `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`, adding tables/columns, creating a Host-owned durable preference store, bypassing `Dobby.Configuration`, using non-durable-only shell preference state, using PII-bearing raw user identifiers in `ConfigKey`, or persisting shell preferences without authenticated user scope; record Human decision required for schema/preference authority.

### Assumption A4 — Diagnostics export shell action is Host-mediated, not Shell-to-Diagnostics direct coupling
- Assumption: add the authenticated shell export trigger as Shell action state and Host coordinator wiring, so file system/output path/audit details stay Host/Diagnostics mediated.
- Host placement rule: `ShellDiagnosticsExportCoordinator.cs` belongs at `src/Dobby.Host/ShellDiagnosticsExportCoordinator.cs` with namespace `Dobby.Host`. Do not create `Dobby.Host.Diagnostics` unless `docs/08_CODING_STANDARDS.md` §5 has a table row or explicit docs amendment before creation.
- Cited source: `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / AC-033 contribution; `src/Dobby.Diagnostics/DiagnosticsExportPackageBuilder.cs`; `src/Dobby.Audit/AuditEventName.cs`; `docs/specs/audit-and-diagnostics.md`; `src/Dobby.Shell/Dobby.Shell.csproj`.
- Risk if wrong: adding `Dobby.Shell -> Dobby.Diagnostics` or direct OS file APIs in a ViewModel may violate architecture/coding standards.
- Fallback: keep Diagnostics export coordination in `Dobby.Host`; if a new project reference is necessary, validate against `docs/02_ARCHITECTURE_MAP.md` and add architecture test coverage or request Human decision required.

### Assumption A5 — Existing test stack is enough for UI snapshots
- Assumption: use existing WPF-enabled test projects and `Verify.Xunit` for snapshot evidence, plus manual evidence blocks for DPI/resolution checks.
- Cited source: `Directory.Packages.props`; `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Verification / evidence; `docs/specs/shell-ui.md` §Verification.
- Risk if wrong: screenshot automation may require a new package or native tool.
- Fallback: do not add package pins; record Human decision required for any new UI automation/screenshot package and keep manual evidence where automation is not feasible.

### Assumption A6 — Work-session lifecycle extends the existing `Dobby.Shell.WorkSessions` owner namespace
- Assumption: extend `src/Dobby.Shell/WorkSessions/WorkSessionRegistry.cs` with close/cancel/warning metadata and add related files under `src/Dobby.Shell/WorkSessions/`.
- Cited source: `src/Dobby.Shell/WorkSessions/WorkSessionRegistry.cs`; `docs/specs/work-sessions.md`; `docs/adr/ADR-0050-in-memory-work-sessions.md`.
- Risk if wrong: lifecycle orchestration may need Host session/logout services rather than Shell-only state.
- Fallback: keep shell-owned pure state in `Dobby.Shell` and Host session/logout adapters in `Dobby.Host`; request Human decision required if a new cross-project lifecycle contract is needed.

### Assumption A7 — WPF dispatcher implementation is Shell-wired
- Assumption: keep the `IUiDispatcher` abstraction in `Dobby.Messaging.UiDispatch` and implement/wire the WPF dispatcher under `src/Dobby.Shell/UiDispatch/`, matching the documented `Dobby.Messaging` / `Dobby.Shell` host-internal Messaging UI Bridge.
- Cited source: `src/Dobby.Messaging/UiDispatch/IUiDispatcher.cs`; `docs/02_ARCHITECTURE_MAP.md` Row 55; `docs/03_TECH_STACK_AND_VERSIONS.md` UI dispatcher row; `docs/08_CODING_STANDARDS.md`; `docs/adr/ADR-0015-background-operation-reporting.md`; `src/Dobby.Shell/Dobby.Shell.csproj`.
- Risk if wrong: implementation may need a Host-owned WPF adapter despite the current architecture wording.
- Fallback: stop before adding a WPF reference to non-WPF/plugin-facing projects or moving the WPF dispatcher to `Dobby.Host`; request Human decision required if Host-owned dispatcher placement is proposed.

## 8. Files to Modify
| File | Exact change scope | Reason | Boundary impact | Required test updates | Architecture-test consequences |
|---|---|---|---|---|---|
| `src/Dobby.Host/MainWindow.xaml` | Replace placeholder Account panel with authenticated shell layout: `TopToolbar`, `Sidebar`, `Third Bar`, `Main Content`, status/progress, notification/feedback states, minimized work strip, profile/menu shell entry points, and Settings rendering host. | Sprint 7 shell composition and AC-003/AC-035. | Host WPF surface may use WPF/WPF-UI; do not expose WPF to Contracts/SDK. | Shell snapshot tests, keyboard smoke, rendered Settings tests. | Architecture tests should remain green; no new forbidden project refs. |
| `src/Dobby.Host/MainWindow.xaml.cs` | Preserve session checkpoint/activity/manual lock/password behavior while wiring Shell view model, theme switching, work-session warnings, diagnostics export action, and first-paint milestone. | Existing post-login lifecycle currently lives here. | OS/file/dialog operations stay code-behind/Host coordinators, not ViewModels. | Host shell integration, diagnostics export action, work-session lifecycle, shell-first-paint evidence. | No direct dependency violation expected. |
| `src/Dobby.Host/App.xaml` | Support dynamic light/dark theme resource dictionary switching while preserving design tokens. | AC-016 and design-system theme requirements. | No per-screen hardcoded theme colors. | Theme integration and snapshots. | Encoding test covers XAML. |
| `src/Dobby.Host/App.xaml.cs` | Pass Shell/runtime dependencies to `MainWindow` and keep login-first/no-heavy-pre-login policy. | Shell needs authenticated runtime sources after login. | Must not start plugin activation or heavy work before login. | `HostLoginRuntimeFactoryTests`, shell first-paint integration. | No package/ref change expected. |
| `src/Dobby.Host/Login/HostLoginRuntime.cs` | Add shell/runtime coordination fields only if needed to carry an authenticated-user-scoped settings preference source/scope to `MainWindow` without constructor sprawl. | Current aggregate contains Settings/Auth sources but no shell runtime object or explicit shell preference scope. | New fields must be Host-owned or existing project types; do not expose raw PII-bearing identifiers to Shell. | `HostLoginRuntimeFactoryTests`; `ShellPreferenceUserIsolationTests`. | Public surface is internal app code, not Contracts. |
| `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs` | Compose shell view model/coordinators, Host-owned theme coordinator, background operation reporter, diagnostics export coordinator, authenticated-user-scoped settings/preference source, `IStringLocalizer<T>`-backed host localization adapter/catalog, and UI dispatcher after pre-login restrictions are satisfied. | Composition root for post-login services; current preference source is global unless an authenticated scope is applied. | Do not instantiate plugin runtime/package validation; preserve audit retention background operation; do not add `Dobby.Shell -> Dobby.Configuration`; do not reuse an unscoped/global preference source for shell view preferences. | `HostLoginRuntimeFactoryTests`, diagnostics export action integration, theme/preference integration, localization adapter tests, two-user shell preference isolation tests. | Package governance should remain green with Sprint 7 pin parity covered. |
| `src/Dobby.Shell/ShellViewModel.cs` | Expand from top navigation only to shell state/actions, consuming the `Dobby.Navigation` composed navigation model and configuration-neutral Host-provided commands/state for required surfaces, notification taxonomy, feedback states, and work-session restore state. | Current ViewModel is insufficient for Sprint 7 shell surfaces. | No direct Host/OS APIs, no Diagnostics package builder direct call, and no `Dobby.Configuration` dependency under Assumption A3/A4. | Shell layout/navigation/theme tests; existing smoke test update. | `Dobby.Shell.csproj` references must remain allowed by architecture map. |
| `src/Dobby.Shell/Dobby.Shell.csproj` | Add `Dobby.Messaging` project reference only if needed for the Shell-wired `WpfUiDispatcher` implementation to consume `Dobby.Messaging.UiDispatch.IUiDispatcher`. | Q8 resolves dispatcher adapter ownership to the documented `Dobby.Messaging` / `Dobby.Shell` bridge. | Allowed only for the Shell-wired dispatcher bridge; do not add `Dobby.Configuration`, Host, Infrastructure, Diagnostics, plugin activation, or Contracts/SDK WPF exposure; any architecture-map reverse-column inconsistency is not permission for `Dobby.Shell -> Dobby.Configuration`. | Architecture dependency tests and `WpfUiDispatcherBoundaryTests.cs` if implementation exposes behavior. | Any new project edge must stay documented by the enforced allowed-reference row and `ArchitectureDependencyBoundaryTests.cs`; do not weaken architecture tests. |
| `src/Dobby.Shell/ShellRouteHost.cs` | Add route fallback diagnostics state and route hosting around the `Dobby.Navigation` route/composition model. | ADR-0013/ADR-0049 fallback and navigation model. | Fallback diagnostic warning is not audit unless protected operation attempt occurs; `Dobby.Navigation` remains descriptor/composition owner. | Navigation composer tests, route fallback tests. | No Core ErrorCode addition. |
| `src/Dobby.Shell/WorkSessions/WorkSessionRegistry.cs` | Extend session state with metadata/progress/error/result/input/screen restore state, minimized display, and explicit close/cancel semantics. | AC-019 and ADR-0050. | Process-lifetime only; no persistence. | Work-session lifecycle tests. | No storage/schema change. |
| `src/Dobby.Navigation/RouteRegistry.cs` | Preserve current built-in route constants and integrate them with Navigation-owned descriptors/composer; do not rename route keys unless decision changes. | AC-023; ADR-0013/ADR-0049. | Route identity migration is gated by Assumption A2. | Navigation tests and `NavigationComposerTests`. | No Contracts widening. |
| `src/Dobby.Configuration/SettingsPreferences.cs` | Add typed `ShellViewPreference` aggregate member/defaults for required shell UI preferences, including Sidebar open node state. | AC-016/AC-023; architecture map Row 46; Assumption A3. | Durable preference ownership remains `Dobby.Configuration`; concrete persistence remains Host-wired through an authenticated user-scoped source/scope. | `SettingsPreferenceSurfaceTests`, `SettingsPreferencePersistenceTests`, `ShellPreferenceUserIsolationTests`, theme/navigation integration tests. | No schema change. |
| `src/Dobby.Configuration/ISettingsPreferenceSource.cs` | Keep `Load`/`Save` parameterless only if source instances are explicitly authenticated-user-scoped before use; otherwise introduce the minimal `Dobby.Configuration` scope parameter/type required by Assumption A3. | Current contract has no user parameter; AC-016/AC-023 require per-user shell preferences. | Do not add Infrastructure/Host references to `Dobby.Configuration`; do not expose persistence keys or raw user identifiers through the contract. | `SettingsPreferenceSurfaceTests`; `HostLoginRuntimeFactoryTests`; `ShellPreferenceUserIsolationTests`. | No schema change or project-edge change expected. |
| `src/Dobby.Host/Settings/HostConfigurationSettingsPreferenceSource.cs` | Persist/load required shell view preference keys, including Sidebar open node state, through existing `HostConfiguration` key/value rows using authenticated-user-scoped non-PII key namespaces; reject or avoid global shell preference keys. | AC-016/AC-023; `D-21`; Assumption A3; current global `settings.*` source behavior. | Must not create Host-owned durable preference storage or new tables/columns; must not store PII-bearing raw user identifiers in `ConfigKey`. | `SettingsPreferencePersistenceTests`; `ShellPreferenceUserIsolationTests`; shell preference integration tests. | No direct `Dobby.Configuration -> Dobby.Infrastructure` edge. |
| `src/Dobby.UI/ThemeTokens.cs` | Add documented token keys for full shell/theme design system. | `docs/specs/design-system-assets.md`. | Tokens stay shared; no one-off view-specific colors. | Theme token/static tests; snapshots. | No package impact. |
| `src/Dobby.UI/Themes/DesignTokens.xaml` | Add spacing/radius/typography/motion token resources. | Design-system asset spec. | Reduced-motion/high-contrast choices must be centralized. | Encoding/static scan and snapshots. | XAML encoding covered. |
| `src/Dobby.UI/Themes/LightTheme.xaml` | Expand light theme brush resources matching new token keys. | AC-016 and theme token coverage. | No layout changes per theme. | Snapshots and token tests. | XAML encoding covered. |
| `src/Dobby.UI/Themes/DarkTheme.xaml` | Expand dark theme brush resources to documented Solarized Dark mapping where required. | `docs/specs/design-system-assets.md`. | No per-screen dark theme hardcoding. | Dark snapshots and token tests. | XAML encoding covered. |
| `src/Dobby.Localization/HostResourceKeys.cs` | Add shell/profile/status/work-session/settings/rendered UI keys and keep key names in parity with host `.resx` resource names. | AC-017 and ADR-0044. | Do not use raw visible strings in WPF/code. | Localization coverage tests. | Public host key constants only; no plugin contract. |
| `src/Dobby.Localization/HostStringCatalog.cs` | Consolidate the current in-memory catalog into the Dobby-owned adapter path over `IStringLocalizer<T>` and `Host.az-AZ.resx`; preserve `az-AZ` default/fallback behavior and add missing-key diagnostics adapter behavior. | AC-017; ADR-0043; ADR-0044; `docs/03_TECH_STACK_AND_VERSIONS.md`; audit/diagnostics spec. | `localization.missing-key` diagnostics only, not audit; avoid direct diagnostics package coupling unless architecture permits. | Localization coverage, `.resx` parity, adapter/fallback, and missing-key tests. | Avoid adding `Dobby.Localization -> Dobby.Diagnostics` unless architecture permits. |
| `src/Dobby.Localization/Resources/Host.az-AZ.resx` | Add full matching host resources for all Sprint 7 shell/profile/status/work-session/settings/rendered UI keys used by `HostResourceKeys` / `HostStringCatalog`; this `.resx` parity is required, not optional. | `docs/03_TECH_STACK_AND_VERSIONS.md` localization row; ADR-0044; Sprint 5 deferred resource-file consolidation to Sprint 7. | UTF-8/no-BOM; no runtime language switching. | Localization key scans and `.resx` parity tests. | Encoding test covers `.resx`. |
| `src/Dobby.Host/Settings/AppearanceSettingsModel.cs` | Ensure Settings Appearance and TopToolbar switch share immediate apply semantics through the same post-login scoped preference source used by shell/theme coordination. | AC-016/AC-035. | Use existing `ISettingsPreferenceSource` only as an authenticated scoped instance for shell-facing flows; avoid silently changing unrelated Sprint 6 security/session semantics without tests/evidence. | Shell theme integration tests; `ShellPreferenceUserIsolationTests` if theme is scoped with shell preferences. | No schema change. |
| `src/Dobby.Host/Settings/SettingsNavigationModel.cs` | Support rendered Settings tree visibility/availability if current source categories need view state. | AC-035; Sprint 6 source behavior. | User Management gate remains policy-backed, not UI-only. | Rendered Settings tests; existing Settings tests. | No new operation target unless approved. |
| `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs` | Extend to require typed shell view preferences and authenticated preference scope shape in `Dobby.Configuration`, including Sidebar open node state, without concrete table access or raw persistence keys. | AC-016/AC-023; `D-21`; Assumption A3. | No Host-owned durable preference type and no `Dobby.Configuration -> Dobby.Infrastructure` edge. | Unit tests. | Architecture tests should remain green. |
| `tests/Dobby.SmokeTests/ShellSmokeTests.cs` | Update from minimal top-navigation check to shell route/navigation smoke, or keep and add new smoke file. | Existing smoke test will be outdated after shell expansion. | Smoke stays offline and plugin-activation-free. | Smoke project. | No package impact. |
| `tests/Dobby.UnitTests/Navigation/NavigationTests.cs` | Extend route fallback/identity tests for hierarchy while preserving existing built-in constants. | AC-023; Assumption A2. | Do not require route renaming. | Unit tests. | No architecture impact. |
| `tests/Dobby.UnitTests/Shell/WorkSessionRegistryTests.cs` | Extend current minimize/restore test or split into new lifecycle file, including progress/result/error/input/screen state retention across explicit minimize/restore. | AC-019. | Process-lifetime only. | Unit tests. | No storage impact. |
| `tests/Dobby.UnitTests/Localization/SettingsLocalizationStringCatalogTests.cs` | Extend key coverage or keep as Sprint 6 baseline with new shell coverage test. | AC-017/AC-035. | Visible strings must resolve. | Unit tests. | No package impact. |
| `tests/Dobby.IntegrationTests/Host/HostLoginRuntimeFactoryTests.cs` | Assert Sprint 7 shell/runtime composition and background operation surface wiring. | Current composition test covers Sprint 6 sources only. | No pre-login heavy work or plugin activation. | Integration tests. | No package impact. |
| `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs` | Extend to prove theme, sidebar collapse state, Sidebar open node state, Sidebar/Third Bar ordering, TopToolbar plugin ordering/hidden state, window size/position, and last active module storage persist through existing `HostConfiguration` rows without startup restore and without global shell preference keys. | AC-016/AC-023 and Assumption A3. | Schema widening, non-durable-only preference state, or user-colliding keys require Human decision required. | Integration tests. | Storage/schema guard preserved. |
| `tests/Dobby.IntegrationTests/Host/ShellPreferenceUserIsolationTests.cs` | Add two-user persistence coverage proving authenticated user A and authenticated user B can save different shell view preferences and reload their own values without collision; include sidebar collapse/open node state, Sidebar/Third Bar ordering, TopToolbar plugin ordering/hidden state, window size/position, and last active module storage-without-startup-restore. | AC-016/AC-023; `docs/specs/shell-ui.md` §Per-user shell state; Assumption A3. | Must use existing `HostConfiguration` rows without schema widening and without depending on plugin activation or unrelated user-management UI. | Integration tests. | Storage/schema and architecture boundaries preserved. |
| `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` | Modify only if a newly allowed project edge is both documented by `docs/02_ARCHITECTURE_MAP.md` and needed. | Architecture boundary enforcement. | Do not weaken existing tests. | Architecture tests. | Any new project ref must be justified. |
| `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs` | Extend package-governance parity to the Sprint 7 test-readable block (`dobby.sprint7.toolchainPins.v1`) in `toolchain-pin-gate.md`, or prove through `Sprint7ReleaseEvidenceTests.cs` that the exact approved package set/no-drift markers are verified. | Package governance; `HD-S7-001` / `Q1` evidence. | New package pin/source/native asset, package-reference target expansion, or Sprint 7 use/scope expansion of unapproved existing pins requires Human decision required. | Architecture tests. | Must not weaken existing Sprint 4/5 package-governance checks. |
| `artifacts/release-evidence/skeleton-local/architecture-baseline.md` | Implementation-time append only: Sprint 7 implementation evidence, snapshots/manual evidence, AC mapping, final build/test/format results. | Release evidence convention and Sprint 6 pattern. | Planning does not edit evidence; implementation appends after verification. | `Sprint7ReleaseEvidenceTests.cs`. | Evidence test should enforce block presence. |

## 9. Files Not Touched
| Surface | Exclusion | Source basis |
|---|---|---|
| `SPRINT_IMPLEMENTATION_PLAN.md` | Do not edit master plan. | User instruction; master plan is scope authority. |
| `SPRINT_1_EXECUTION_PLAN.md` through `SPRINT_6_EXECUTION_PLAN.md` | Do not rewrite predecessor plans. | User instruction; predecessor plans are context only. |
| `SPRINT_8_EXECUTION_PLAN.md` or later execution plans | Do not create later plans; none currently exist. | User output target restricted to `SPRINT_7_EXECUTION_PLAN.md`. |
| `docs/` ADR/spec/requirements files | Do not edit docs/ADRs/specs during planning or implementation unless a separate decision opens doc work. | User instruction; `docs/08_CODING_STANDARDS.md`; §6. |
| `docs/schemas/dobby-plugin-manifest.schema.json` | Do not alter plugin manifest schema in Sprint 7. | Sprint 8 owns plugin package trust/activation; ADR-0036 and schema are boundary context. |
| `docs/schemas/dobby-plugin-license.schema.json` | Do not alter license schema in Sprint 7. | Sprint 9 owns licensing/runtime mode; `docs/specs/license-import.md`. |
| `Directory.Packages.props` | Do not add/alter pins; `HD-S7-001` / `Q1` approves only the exact existing Sprint 7 package set listed in §11/§13 and `toolchain-pin-gate.md`. | `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md#sprint-7-package-approval---hd-s7-001--q1`. |
| `global.json` | Do not change SDK pin. | `global.json`; `docs/03_TECH_STACK_AND_VERSIONS.md`. |
| `Directory.Build.props` | Do not weaken warnings-as-errors/analyzer settings. | `Directory.Build.props`; `docs/08_CODING_STANDARDS.md`. |
| `src/Dobby.Core/Result.cs` | Do not add `ErrorCode` values. | `docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`. |
| `src/Dobby.Audit/AuditEventName.cs` | Do not add audit event names. | `docs/specs/audit-and-diagnostics.md`; Sprint 7 can use existing `diagnostics.export.created`. |
| `src/Dobby.Contracts/` and `src/Dobby.SDK/` | Do not widen plugin-facing contracts or add WPF types. | `tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs`; `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`. |
| `src/Dobby.PluginFramework/` | Do not implement plugin activation/runtime. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8. |
| `src/Dobby.Licensing/` | Do not implement license enforcement/import/runtime mode. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9; `docs/specs/license-import.md`. |
| `src/Dobby.Admin/` | Do not implement Admin artifact tools. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 10. |
| `samples/Dobby.Sample.Plugin/` | Do not implement sample plugin behavior. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 10. |
| `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs` | Do not widen schema unless Human decision required is resolved. | `artifacts/release-evidence/skeleton-local/storage-materialization.md`; `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`. |
| `installer/`, `tools/`, publish/release packaging paths if present | Do not start installer/release-gate work. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 11; `docs/09_BUILD_RUN_AND_RELEASE.md`. |
| Sprint 1 print-boundary edge | Do not close or renew the print-boundary blocker in Sprint 7. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`. |

## 10. Error / Result / Policy Mapping
This section applies to Sprint 7 because shell routing, diagnostics export, localization diagnostics, background operations, protected Settings routes/actions, and work-session cancellation cross user-facing boundaries (`docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `docs/specs/audit-and-diagnostics.md`; `docs/specs/work-sessions.md`).

| Surface | Existing Result / ErrorCode / policy mapping | Audit / diagnostics mapping | UI disclosure rule | Evidence |
|---|---|---|---|---|
| Unknown route / invalid route activation | Current `RouteRegistry.ResolveOrHome` falls back to Home without `Result`; if implementation introduces typed state, use existing `ErrorCode.NotFound` only internally. | Diagnostic warning allowed; no audit unless a protected operation is attempted. | Show Home/fallback state, not raw route internals. | `src/Dobby.Navigation/RouteRegistry.cs`; `docs/adr/ADR-0013-routing-and-deep-link-model.md`; `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`. |
| Protected Settings/User Management route/action | `OperationAuthorizationPolicy` returns `PolicyDecision.Deny("permission-denied")`; no new ErrorCode required. | Denied protected operation may audit `permission.denied` through Host coordinator, as existing tests show. | Non-disclosing disabled/denied state; no Owner detail leakage. | `src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs`; `tests/Dobby.IntegrationTests/Security/OperationAuthorizationIntegrationTests.cs`; `docs/adr/ADR-0048-operation-based-authorization.md`. |
| Diagnostics export action | Expected IO/export failures map to existing `ErrorCode.IoFailed` or `ErrorCode.InvalidInput` at Host boundary if wrapped; no new Core code. | Successful export action audits existing `diagnostics.export.created`; diagnostics package remains local and `Uploaded=false`. | Show support-safe result; no full local paths, secrets, tokens, raw license/session material. | `src/Dobby.Core/Result.cs`; `src/Dobby.Audit/AuditEventName.cs`; `src/Dobby.Diagnostics/DiagnosticsExportPackageBuilder.cs`; `docs/specs/audit-and-diagnostics.md`. |
| Missing localization key | Missing key is diagnostics-only; no ErrorCode and no audit event. | Existing event name is `localization.missing-key` diagnostics-only. | Development may show visible missing-key marker; production fallback must be non-breaking. | `src/Dobby.Diagnostics/LocalizationMissingKeyEvent.cs`; `docs/specs/audit-and-diagnostics.md`; `docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`. |
| Work-session close/cancel/navigation warning | User cancellation maps to existing `ErrorCode.Cancelled` at public/plugin boundary; internal state can use owner-local close result. | No new work-session audit event; logout/session audit events remain session-owned where already documented. | Warning choices must be localized and non-raw: minimize/stay/close semantics from spec. | `src/Dobby.Core/Result.cs`; `docs/specs/work-sessions.md`; `docs/specs/session-lifecycle.md`; `docs/adr/ADR-0050-in-memory-work-sessions.md`. |
| Background operation status/progress | Use existing `BackgroundOperationStatus` values and `FailureReason` as existing `ErrorCode?`. | Diagnostics may carry technical detail; audit only if underlying user/security action owns an audit event. | Shell shows status/progress vocabulary, not raw stack traces. | `src/Dobby.Contracts/BackgroundOperations/BackgroundOperationContracts.cs`; `docs/adr/ADR-0015-background-operation-reporting.md`; `docs/specs/shell-ui.md`. |

Local sub-code semantics:
- Owner-local user-facing reason codes are allowed only inside the owner namespace when backed by specs, such as plugin install/license future specs; Sprint 7 must not introduce new Core `ErrorCode` values, new audit event names, or new plugin-facing closed-set entries (`docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`).
- Diagnostics export and missing-key diagnostics must preserve the non-disclosing UI vs full diagnostics distinction in `docs/specs/audit-and-diagnostics.md`.

Plugin-host boundary statement:
- Sprint 7 may render plugin navigation slots and plugin page region placeholders, but plugin code does not return WPF types and plugin activation is not started (`src/Dobby.Contracts/PluginPages/PluginPageContracts.cs`; `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8).

Result-vs-exception split discipline:
- Expected route misses, validation failures, denied operations, export failures, and cancellations use existing `Result<T>`/typed state/error codes; programmer/invariant violations may throw; Host UI catches non-fatal boundary errors and shows safe localized messages (`docs/04_RESULT_VS_EXCEPTION_POLICY.md`).

## 11. Technology Stack and Versions
| Stack item | Current pin / setting | Sprint 7 impact |
|---|---|---|
| .NET SDK | `10.0.202` in `global.json` | Use existing SDK; no planning-time toolchain change. |
| TFM | `net10.0-windows` for `Dobby.Host`, `Dobby.Shell`, `Dobby.UI`, and WPF-enabled tests; `net10.0` for non-WPF contracts/services (`src/Dobby.Host/Dobby.Host.csproj`; `src/Dobby.Shell/Dobby.Shell.csproj`; `src/Dobby.UI/Dobby.UI.csproj`). | WPF UI remains in Windows-targeted projects; Contracts/SDK remain WPF-free. |
| WPF-UI | `WPF-UI` `4.3.0`; `WPF-UI.DependencyInjection` `4.3.0` in `Directory.Packages.props`. | Approved for Sprint 7 runtime UI shell/theme use by `HD-S7-001` / `Q1`; this is not generic approval for other pins or package changes. |
| MVVM | `CommunityToolkit.Mvvm` `8.4.2` in `Directory.Packages.props`. | Approved for Sprint 7 runtime MVVM/UI use by `HD-S7-001` / `Q1`; this is not generic approval for other pins or package changes. |
| Localization | `Microsoft.Extensions.Localization` `10.0.7` in `Directory.Packages.props`; `.resx` + Dobby adapter expected by `docs/03_TECH_STACK_AND_VERSIONS.md`. | Approved for Sprint 7 runtime localization use by `HD-S7-001` / `Q1`; no `WPFLocalizeExtension`, runtime language-switch package, or other localization package. |
| Testing | `xunit` `2.9.3`, `Shouldly` `4.3.0`, `NSubstitute` `5.3.0`, `Verify.Xunit` `31.12.5`, `Microsoft.NET.Test.Sdk` `18.5.1`, `Microsoft.Extensions.TimeProvider.Testing` `10.5.0`. Existing test projects also already reference `xunit.runner.visualstudio` and `coverlet.collector` as runner/coverage infrastructure. | Approved for Sprint 7 test-only snapshot/unit/integration verification use by `HD-S7-001` / `Q1`; existing `xunit.runner.visualstudio` / `coverlet.collector` references may remain unchanged as current test infrastructure, but Sprint 7 must not add, expand target usage, or treat them as newly approved package scope without separate Human decision required. |
| Build rules | Nullable enabled, implicit usings, `LangVersion` `14.0`, warnings as errors, latest analysis, code style in build, deterministic build in `Directory.Build.props`. | Sprint 7 must close with zero warnings and no analyzer suppressions unless locally justified. |
| Encoding/format | UTF-8 no BOM, final newline, CRLF, indentation rules in `.editorconfig`; encoding test in `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs`. | New Markdown/XAML/resx/cs files must be UTF-8 no-BOM. |

Package pin impact:
- Closed `HD-S7-001` / `Q1`: Sprint 7 has repository-backed human approval evidence for the exact existing UI/MVVM/localization/snapshot/test package set: `WPF-UI` `4.3.0`, `WPF-UI.DependencyInjection` `4.3.0`, `CommunityToolkit.Mvvm` `8.4.2`, `Microsoft.Extensions.Localization` `10.0.7`, `Verify.Xunit` `31.12.5`, `xunit` `2.9.3`, `Shouldly` `4.3.0`, `NSubstitute` `5.3.0`, `Microsoft.NET.Test.Sdk` `18.5.1`, and `Microsoft.Extensions.TimeProvider.Testing` `10.5.0` (`artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md#sprint-7-package-approval---hd-s7-001--q1`).
- Not approved without a separate Human decision required: any new package pin, package source, commercial UI control package, screenshot automation package, localization package, telemetry package, native asset, upgrade, downgrade, package-reference target expansion, Sprint 7 use of another existing `Directory.Packages.props` entry not listed above, or `xunit.runner.visualstudio` / `coverlet.collector` Sprint 7 scope expansion (`tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md#sprint-7-package-approval---hd-s7-001--q1`).
- Existing `xunit.runner.visualstudio` and `coverlet.collector` references in `tests/Dobby.UnitTests/Dobby.UnitTests.csproj`, `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj`, and `tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj` are current test runner/coverage infrastructure only; Sprint 7 implementation should leave those references unchanged unless a separate approval explicitly changes their scope.

Native assets:
- Sprint 7 should not add native assets. Existing native/storage package approvals remain from earlier evidence (`artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`; `artifacts/release-evidence/skeleton-local/storage-materialization.md`).

Test / runtime / TFM / conditioned-reference consequences:
- Shell/UI/Host/WPF snapshot work belongs in Windows TFM projects (`src/Dobby.Host/Dobby.Host.csproj`; `src/Dobby.Shell/Dobby.Shell.csproj`; `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj`).
- Plugin-facing `Dobby.Contracts` and `Dobby.SDK` must not reference WPF or UI assemblies (`tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs`; `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`).
- `System.Management` is approved only for the earlier infrastructure scope and must not be moved into Host/Shell (`tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`).

Zero-warning / format / encoding constraints:
- Final implementation must pass documented restore/build/test/format checks from `docs/09_BUILD_RUN_AND_RELEASE.md`; this planning turn does not run them by instruction.

## 12. Code Work Plan

### Pre-flight
1. Reconfirm sprint boundaries and closed gates.
   - Action: read `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7, `SPRINT_6_EXECUTION_PLAN.md`, `artifacts/release-evidence/skeleton-local/decision-log.md`, `artifacts/release-evidence/skeleton-local/architecture-baseline.md` Sprint 6 evidence, `docs/03_TECH_STACK_AND_VERSIONS.md`, and `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`.
   - Success criteria: implementer records that Sprint 7 is using `D-20`/`D-21`, current 487-test Sprint 6 baseline, closed package approval `HD-S7-001` / `Q1`, and source-grounded non-package planning constraints `Q2`-`Q8` from §13.
   - Out-of-scope guardrail: do not edit prior sprint plans, master plan, ADRs, specs, schemas, or evidence during pre-flight unless a separate documented decision opens that work.
2. Establish tests-before-code targets.
   - Action: create/extend tests named in §7/§14 for navigation, shell layout, theme/preference, shell preference persistence, two-user shell preference isolation, localization, hardcoded UI/static token scans, work sessions, diagnostics export action, snapshots, Sprint 7 package-governance parity/no-drift evidence, and evidence closure.
   - Success criteria: tests fail for missing Sprint 7 behavior before implementation changes.
   - Out-of-scope guardrail: do not introduce new packages or screenshot tools; use existing test stack.
3. Validate architecture/package assumptions.
   - Action: inspect `.csproj` references before adding any reference; compare against `docs/02_ARCHITECTURE_MAP.md`, `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`, `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`, and the Sprint 7 `dobby.sprint7.toolchainPins.v1` evidence.
   - Success criteria: no unapproved project edge or package scope change is planned; any needed edge/package/scope change is already allowed or recorded as Human decision required before implementation; existing `xunit.runner.visualstudio` / `coverlet.collector` references remain current infrastructure, not expanded Sprint 7 package scope.
   - Out-of-scope guardrail: do not weaken architecture tests.

### Implementation
4. Build navigation composition.
   - Action: implement Navigation-owned contribution descriptors/composer and route fallback using `src/Dobby.Navigation/DefaultUiOrderingComparer.cs`, `src/Dobby.Navigation/RouteRegistry.cs`, and `Dobby.Navigation` models; wire `Dobby.Shell` only to render/host the composed model.
   - Success criteria: `NavigationComposerTests` verifies built-in fixed positions, group/weight/tiebreak ordering, hierarchy, user overrides/reset, and unknown-route fallback; existing `NavigationTests` remain green; `Dobby.Shell` does not own descriptor normalization/composition.
   - Out-of-scope guardrail: do not seed plugin targets from manifests or activate plugins; Sprint 8 owns that.
5. Build shell layout state and WPF rendering.
   - Action: expand `src/Dobby.Host/MainWindow.xaml` and Shell view models/state to render required surfaces, minimum sizing, status/progress, profile menu, notification/feedback states, and minimized strip.
   - Success criteria: shell layout tests cover Toast/Banner/Modal notification taxonomy and loading/empty/success/error/confirmation feedback states; shell snapshot tests cover 1024x680 and 1920x1080 in light/dark and prove a synthetic nested descriptor tree renders through `TopToolbar`, `Sidebar`, and `Third Bar` without plugin activation; smoke/manual accessibility evidence covers keyboard route navigation, Home/Plugins/Settings, Sidebar collapse, theme switch, profile menu inventory (profile/account info, password change, manual lock, logout, saved-login clear), visible focus indicators, minimum contrast, and role/label mapping.
   - Out-of-scope guardrail: do not create landing/marketing pages, plugin runtime, or business-domain screens.
6. Wire theme, design tokens, and shell preferences.
   - Action: expand `Dobby.UI` token dictionaries; add `Dobby.Configuration` typed shell view preferences and authenticated preference scope; use a flat-namespace Host-owned `ShellThemeCoordinator` to connect the TopToolbar switch to existing `AppearanceSettingsModel`/`ISettingsPreferenceSource`; construct or pass only a post-login user-scoped preference source to shell/theme/settings flows; persist required shell UI preferences through authenticated-user-scoped `HostConfiguration` keys; switch light/dark dictionaries from one TopToolbar control.
   - Success criteria: theme integration tests prove TopToolbar switch and Settings Appearance share the same persisted preference; shell preference tests prove sidebar collapse state, Sidebar open node state, Sidebar/Third Bar ordering, TopToolbar plugin ordering/hidden state, window size/position, and last active module storage persist through `HostConfiguration` without startup restore; two-user isolation tests prove those shell preference values do not collide across authenticated users; snapshots show both themes; hardcoded color/spacing/font static scans pass.
   - Out-of-scope guardrail: do not add advanced palettes, per-plugin themes, new theme enum values, schema widening, global shell preference keys, or PII-bearing raw user identifiers in `ConfigKey`.
7. Wire rendered Settings/About.
   - Action: render Settings categories from `SettingsNavigationModel`, Appearance from `AppearanceSettingsModel`, Account from `AccountSettingsModel`, Security/Session from `SecuritySessionSettingsModel`, and About from `AboutInfoProvider`.
   - Success criteria: rendered Settings tests/snapshots prove Appearance, Account, Security/Session, User Management, and About visibility; rendered About evidence covers app version, build number/date, host edition/channel, redacted device code, plugin versions/status or explicit empty-state boundary, support contact, legal line, managed-user neutral Owner identity, and absence of license import/status.
   - Out-of-scope guardrail: do not reimplement Sprint 6 source logic or add license management to About/Settings.
8. Centralize localization and missing-key diagnostics.
   - Action: add shell/resource keys and `az-AZ` values to `HostResourceKeys`; expand `Host.az-AZ.resx` to full parity for Sprint 7 visible shell/Profile/Settings/work-session strings; consolidate `HostStringCatalog` through the Dobby-owned `IStringLocalizer<T>` adapter path; implement missing-key diagnostics-only behavior.
   - Success criteria: localization tests and `ShellUiStaticScanTests` cover visible shell/Profile/Settings/work-session strings, `az-AZ` default, `HostResourceKeys`/`Host.az-AZ.resx` parity, `IStringLocalizer<T>` adapter lookup/fallback behavior, missing-key diagnostics not audit, no hardcoded visible shell/built-in UI strings, and UTF-8/no-BOM scans.
   - Out-of-scope guardrail: do not implement runtime language switching or plugin localization loading.
9. Extend work-session lifecycle.
   - Action: extend `WorkSessionRegistry` and related Shell/Host coordination for explicit minimize/restore, warning choices, controlled close/cancel, metadata, progress/result/error/input/screen restore state, lock/logout behavior, and restart non-restore.
   - Success criteria: `WorkSessionLifecycleTests` and integration tests cover no silent auto-minimize, navigation warning choices, close/cancel, progress/result/error/input/screen state retention across explicit minimize/restore, lock preserving sessions, logout close/cancel, and no restart restore.
   - Out-of-scope guardrail: do not persist work sessions or plugin domain data.
10. Wire diagnostics export action.
   - Action: add authenticated shell/Profile action that invokes a flat-namespace Host diagnostics export coordinator, uses existing `DiagnosticsExportPackageBuilder`, and appends `diagnostics.export.created` audit through existing audit infrastructure.
   - Success criteria: integration test proves local package creation, redaction/exclusion, `Uploaded=false`, audit event, and no telemetry/upload path.
   - Out-of-scope guardrail: do not add upload, telemetry, crash-reporting, or new audit event names.
11. Wire background status/progress.
   - Action: render `BackgroundOperationSnapshot` values in bottom status/progress without ViewModel direct threading or ad-hoc `Task.Run`.
   - Success criteria: host runtime/background operation tests prove audit retention sweep/status can be observed by shell status model.
   - Out-of-scope guardrail: do not add new background operation contract values unless approved.

### Evidence / closure
12. Run full verification.
   - Action: run documented restore/build/test/format sequence from `docs/09_BUILD_RUN_AND_RELEASE.md` after implementation.
   - Success criteria: Release build is zero-warning, full test suite passes, format verify passes, encoding scan passes.
   - Out-of-scope guardrail: do not claim completion without command output evidence.
13. Capture UI evidence.
   - Action: record UI snapshots at 1024x680 and 1920x1080 light/dark, manual 1366x768/1920x1080/2560x1440/3840x2160 checks, and 125/150% DPI notes in release evidence.
   - Success criteria: evidence block names screenshot artifacts or snapshot tests and manual DPI/resolution rows tied to AC-003/AC-016/AC-027/AC-035/AC-040.
   - Out-of-scope guardrail: do not invent release artifact convention outside `artifacts/release-evidence/skeleton-local/architecture-baseline.md` unless repo convention expands.
14. Append Sprint 7 release evidence.
   - Action: append Sprint 7 implementation evidence to `artifacts/release-evidence/skeleton-local/architecture-baseline.md` and add `Sprint7ReleaseEvidenceTests.cs`.
   - Success criteria: evidence records AC mapping, Sprint 7 package-governance parity/no-drift verification for `dobby.sprint7.toolchainPins.v1`, no package drift beyond `HD-S7-001` / `Q1`, no new package source/native asset, no schema widening, no new Core ErrorCodes/audit events, no Contracts/SDK widening, final command results, and closed decision status.
   - Out-of-scope guardrail: do not edit master plan, ADRs, specs, schemas, or previous sprint plans for evidence closure.

## 13. Decisions and Risks

### Closed authority gates
| Gate | Disposition | Evidence |
|---|---|---|
| Sprint 6 built-in User Management target id | Closed as `D-20`; Sprint 7 consumes existing target behavior only. | `artifacts/release-evidence/skeleton-local/decision-log.md`; `src/Dobby.Security/Authorization/BuiltInOperationTargets.cs`. |
| Sprint 6 Settings namespace/preference authority | Closed as `D-21`; Settings/About source orchestration belongs in `Dobby.Host.Settings`, durable preference ownership in `Dobby.Configuration` with Host `HostConfiguration` backing. | `artifacts/release-evidence/skeleton-local/decision-log.md`; `src/Dobby.Host/Settings/HostConfigurationSettingsPreferenceSource.cs`. |
| Diagnostics export audit event | Existing event `diagnostics.export.created`; no new event required. | `src/Dobby.Audit/AuditEventName.cs`; `docs/specs/audit-and-diagnostics.md`. |
| Work-session restart behavior | Closed: no restart restore in P1. | `docs/specs/work-sessions.md`; `docs/adr/ADR-0050-in-memory-work-sessions.md`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7. |

### Human decisions closed before Sprint 7 implementation
| Decision | Status | Evidence / effect |
|---|---|---|
| `HD-S7-001` / `Q1` — Sprint 7 package approval | CLOSED. The exact existing Sprint 7 UI/MVVM/localization/snapshot/test package set is approved for Sprint 7 by repository-backed evidence. | Decision ledger: `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26`; primary evidence: `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md#sprint-7-package-approval---hd-s7-001--q1`. Approval is limited to the exact listed package IDs/versions and scope; any package/source/native asset/version/scope drift requires a separate Human decision required. |

### Source-grounded planning constraints from Sprint 7 reconciliation
| Constraint | Planning stance | Source / effect |
|---|---|---|
| `Q2` — package/source/native asset policy | No new package pin, package source, or native asset is approved for Sprint 7. | Any new package/source/native asset requires a separate Human decision required. |
| `Q3` — shell preference persistence authority | No schema widening, new durable settings table/column, Host-owned durable preference store, direct `Dobby.Configuration -> Dobby.Infrastructure` edge, non-durable-only shell preference state, global shell preference keys, or PII-bearing raw user identifiers in persisted keys. | Required shell preferences use typed `Dobby.Configuration` state with authenticated-user scope and Host-wired existing `HostConfiguration` key/value persistence. |
| `Q4` — dependency/navigation ownership | No `Dobby.Shell -> Dobby.Configuration` project reference; navigation descriptor normalization/composition remains in `Dobby.Navigation`; `Dobby.Shell` renders/hosts the composed model. | Enforced through §7/§8 file ownership, the `Dobby.Shell` allowed-reference row, and `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`; any architecture-map reverse-column inconsistency listing `Dobby.Shell` under `Dobby.Configuration` is not permission to add that edge. |
| `Q5` — closed runtime/API surfaces | No new Core `ErrorCode`, audit event, Contracts/SDK surface, plugin-visible closed-set value, or WPF exposure through Contracts/SDK. | Sprint 7 uses existing `diagnostics.export.created` and keeps plugin-facing surfaces closed. |
| `Q6` — route key migration | Preserve current `RouteRegistry` constants; no route identity migration in Sprint 7. | ADR route examples do not mandate Sprint 7 persisted route migration. |
| `Q7` — later-sprint scope boundary | Sprint 8 plugin activation/package validation, Sprint 9 licensing/runtime mode, and Sprint 10 plugin manager/Admin/sample plugin work stay out of Sprint 7. | Sprint 7 may render shell placeholders/descriptors only. |
| `Q8` — WPF `IUiDispatcher` adapter ownership | Keep `IUiDispatcher` abstraction in `Dobby.Messaging.UiDispatch`; implement/wire the WPF dispatcher through the `Dobby.Shell` boundary; do not create `src/Dobby.Host/UiDispatch/WpfUiDispatcher.cs` unless a later documented architecture/human decision approves Host-owned placement. | §7 creates `src/Dobby.Shell/UiDispatch/WpfUiDispatcher.cs`; §8 records the possible `Dobby.Shell.csproj -> Dobby.Messaging` reference impact. |
| `Q9` — Host namespace placement authority | Keep Sprint 7 Host-owned shell/theme and diagnostics coordinators in existing flat `Dobby.Host`; do not create `Dobby.Host.Shell`, `Dobby.Host.Diagnostics`, or another new public/plugin-visible or skeleton-critical Host namespace placement without a `docs/08_CODING_STANDARDS.md` §5 table row or explicit docs amendment. | §7 file placements and Assumptions A3/A4 avoid new Host subnamespace creation; deviation is a conditional Human decision gate. |

### Source-grounded implementation choices / gated closures
- Use only the exact existing WPF/UI/MVVM/localization/snapshot/test package set approved by `HD-S7-001` / `Q1`; do not treat `Directory.Packages.props` alone as generic approval evidence (`artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md#sprint-7-package-approval---hd-s7-001--q1`).
- Treat existing `xunit.runner.visualstudio` and `coverlet.collector` references as unchanged test runner/coverage infrastructure only; Sprint 7 must not add or expand their package-reference targets/scope without a separate Human decision required (`tests/Dobby.UnitTests/Dobby.UnitTests.csproj`; `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj`; `tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md#sprint-7-package-approval---hd-s7-001--q1`).
- Use existing `HostConfiguration`-backed settings preference source for required Sprint 7 shell preferences only after applying authenticated user scope; stop before schema widening, non-durable-only preference state, global shell preference keys, or PII-bearing raw user identifiers in persisted keys (`tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`).
- Use existing `BackgroundOperationSnapshot`/`BackgroundOperationStatus` for bottom status/progress (`src/Dobby.Contracts/BackgroundOperations/BackgroundOperationContracts.cs`).
- Use existing diagnostics export package builder and redactor for AC-033 action (`src/Dobby.Diagnostics/DiagnosticsExportPackageBuilder.cs`; `src/Dobby.Diagnostics/DiagnosticRedactor.cs`).
- Use existing `OperationAuthorizationPolicy` for protected Settings/Plugins/License route/action visibility and operations (`src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs`).
- Consolidate `HostStringCatalog` and `Host.az-AZ.resx` through the Dobby-owned `IStringLocalizer<T>` adapter path in Sprint 7; deferring `.resx` parity/resource consolidation requires a separate Human decision required (`docs/03_TECH_STACK_AND_VERSIONS.md`; `docs/adr/ADR-0044-localization-resource-organization.md`; `SPRINT_5_EXECUTION_PLAN.md`).
- Keep planned Host-owned shell/theme and diagnostics coordinator files in `src/Dobby.Host/` with namespace `Dobby.Host`; creating `Dobby.Host.Shell`, `Dobby.Host.Diagnostics`, or another new public/plugin-visible or skeleton-critical Host namespace requires `docs/08_CODING_STANDARDS.md` §5 authority first.

### Risks
| Risk | Mitigation | Evidence |
|---|---|---|
| WPF shell work may accidentally pull plugin activation/package validation into Sprint 7. | Keep plugin regions as host placeholders/descriptors only; no package validation/activation code. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8; `docs/adr/ADR-0036-plugin-manifest-schema.md`; `docs/specs/plugins-manager.md`. |
| Route-key examples vs current constants may cause unsupported route migration. | Preserve current constants under Assumption A2; stop before renaming. | `src/Dobby.Navigation/RouteRegistry.cs`; `tests/Dobby.UnitTests/Navigation/NavigationTests.cs`; `docs/adr/ADR-0013-routing-and-deep-link-model.md`. |
| Navigation composition may drift into `Dobby.Shell` ownership. | Keep descriptor normalization/composition in `Dobby.Navigation`; Shell only hosts/renders the composed model. | `docs/02_ARCHITECTURE_MAP.md` Row 51; `docs/adr/ADR-0013-routing-and-deep-link-model.md`; Assumption A2. |
| Theme coordination may create an unapproved `Dobby.Shell -> Dobby.Configuration` project edge. | Keep preference-aware theme coordination in `Dobby.Host`; Shell-facing state/commands stay configuration-neutral; follow the enforced `Dobby.Shell` allowed-reference row and architecture boundary test if the architecture map reverse-column wording appears inconsistent. | `docs/02_ARCHITECTURE_MAP.md` Cədvəl 2; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`; Assumption A3. |
| Host-owned coordinators may drift into unapproved new Host subnamespaces. | Use flat `Dobby.Host` placement for `ShellThemeCoordinator.cs` and `ShellDiagnosticsExportCoordinator.cs`; stop before creating `Dobby.Host.Shell` or `Dobby.Host.Diagnostics` without coding-standards namespace-placement authority. | `docs/08_CODING_STANDARDS.md` §5; §7; Assumptions A3/A4. |
| Theme/sidebar/window/order persistence may exceed current `SettingsPreferences` shape. | Add typed `ShellViewPreference` under `Dobby.Configuration` and persist required shell preferences through existing `HostConfiguration` key/value rows with authenticated user scope; stop before schema widening or non-durable-only preference state. | `src/Dobby.Configuration/SettingsPreferences.cs`; `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; AC-016/AC-023; Assumption A3. |
| Current settings persistence is global and may collide across users if reused unchanged for shell preferences. | Add `Dobby.Configuration` preference scope, wire a post-login authenticated scoped preference source, and require two-user isolation tests before closing AC-016/AC-023. | `src/Dobby.Configuration/ISettingsPreferenceSource.cs`; `src/Dobby.Host/Settings/HostConfigurationSettingsPreferenceSource.cs`; `docs/specs/shell-ui.md` §Per-user shell state; AC-016/AC-023; Assumption A3. |
| Existing centralized UI/MVVM/localization/snapshot/test pins may be mistaken for broad approval evidence. | Treat `HD-S7-001` / `Q1` as approval only for the exact Sprint 7 package set and scope listed in §11/§13; existing `xunit.runner.visualstudio` / `coverlet.collector` references are unchanged test infrastructure, not approval for Sprint 7 scope expansion; any other package/source/native asset/version/scope drift requires a separate Human decision required. | `SPRINT_IMPLEMENTATION_PLAN.md` §5; `docs/03_TECH_STACK_AND_VERSIONS.md`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md#sprint-7-package-approval---hd-s7-001--q1`; test project `.csproj` files; §13. |
| Snapshot testing may require a new package/tool. | Use only approved existing `Verify.Xunit`; any new package or screenshot automation package requires Human decision required. | `Directory.Packages.props`; Assumption A5; §13. |
| Localization missing-key diagnostics may tempt `Dobby.Localization -> Dobby.Diagnostics` coupling. | Prefer Host-composed diagnostics sink or owner-local adapter; validate against architecture map before adding refs. | `src/Dobby.Localization/Dobby.Localization.csproj`; `src/Dobby.Diagnostics/LocalizationMissingKeyEvent.cs`; `docs/02_ARCHITECTURE_MAP.md`. |
| Diagnostics export action may expose raw paths/secrets. | Reuse `DiagnosticsExportPackageBuilder`/`DiagnosticRedactor`; add integration tests for exclusions and audit. | `tests/Dobby.IntegrationTests/Diagnostics/DiagnosticsExportBoundaryTests.cs`; `docs/specs/audit-and-diagnostics.md`. |
| WPF code may put OS APIs into ViewModels. | Keep file dialogs/clipboard/OS APIs in Host adapters/code-behind/coordinators, and keep dispatcher implementation Shell-wired per Assumption A7. | `docs/08_CODING_STANDARDS.md`; `src/Dobby.Messaging/UiDispatch/IUiDispatcher.cs`; `docs/02_ARCHITECTURE_MAP.md` Row 55. |

### Carry-over requiring no new decision in Sprint 7
- Sprint 1 print-boundary closure is Sprint 9 work, not Sprint 7 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`).
- Admin-generated production recovery evidence remains Sprint 10/11 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 5; §Sprint 10; §Sprint 11).
- Plugin declared `operationTargets` seeding remains Sprint 8 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6 / AC-022 contribution; §Sprint 8).

### Human decision required items
None currently open for Sprint 7 planning. Closed decisions, source-grounded planning constraints, and remaining conditional stop gates are recorded in §13.

Conditional stop gates if implementation deviates:
- New package pin/source/native asset, upgrade, downgrade, package-reference target expansion, Sprint 7 use of another existing `Directory.Packages.props` pin beyond the package set under `HD-S7-001` / `Q1`, or `xunit.runner.visualstudio` / `coverlet.collector` scope expansion beyond unchanged runner/coverage infrastructure (`Directory.Packages.props`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md#sprint-7-package-approval---hd-s7-001--q1`).
- Schema widening, new durable settings table/column, Host-owned durable preference store, direct `Dobby.Configuration -> Dobby.Infrastructure` edge, non-durable-only shell preference state, global shell preference keys, user-colliding preference keys, or PII-bearing raw user identifiers in `ConfigKey` (`src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `artifacts/release-evidence/skeleton-local/storage-materialization.md`; `D-21`).
- New `Dobby.Shell -> Dobby.Configuration` project reference or moving descriptor normalization/composition out of `Dobby.Navigation` (`docs/02_ARCHITECTURE_MAP.md` Cədvəl 2; Assumption A2/A3).
- Creating `Dobby.Host.Shell`, `Dobby.Host.Diagnostics`, or another new public/plugin-visible or skeleton-critical Host namespace placement without a `docs/08_CODING_STANDARDS.md` §5 table row or explicit docs amendment.
- Moving the WPF dispatcher implementation/wiring to `src/Dobby.Host/UiDispatch/` or otherwise rejecting Shell-wired placement (`docs/02_ARCHITECTURE_MAP.md` Row 55; Assumption A7; `Q8`).
- New Core `ErrorCode`, audit event, Contracts/SDK surface, plugin-visible closed-set value, or WPF exposure through Contracts (`src/Dobby.Core/Result.cs`; `src/Dobby.Audit/AuditEventName.cs`; `tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs`).
- Route key migration away from current constants (`src/Dobby.Navigation/RouteRegistry.cs`; Assumption A2).
- Moving Sprint 8/9/10 scope into Sprint 7 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8-10).

## 14. Test Plan

### Unit tests
| Test file | Scenarios | AC coverage |
|---|---|---|
| `tests/Dobby.UnitTests/Shell/ShellLayoutStateTests.cs` | Required surfaces present in shell state; profile/menu/status/minimized strip metadata; profile menu inventory metadata includes profile/account info, password change, manual lock, logout, and saved-login clear; Toast/Banner/Modal notification taxonomy; loading/empty/success/error/confirmation feedback-state metadata; no direct WPF types in pure state. | AC-003, AC-027. |
| `tests/Dobby.UnitTests/Navigation/NavigationComposerTests.cs` | Home first, Settings last, built-ins fixed, group/weight/plugin/declaration tiebreak, parent/child hierarchy, user override reset, unknown route fallback, and Navigation-owned descriptor composition. | AC-023, AC-003. |
| `tests/Dobby.UnitTests/Host/ShellThemePreferenceTests.cs` | Host-owned TopToolbar theme coordinator writes same preference consumed by Settings Appearance; only light/dark values; no `Dobby.Shell -> Dobby.Configuration` dependency. | AC-016, AC-035. |
| `tests/Dobby.UnitTests/Shell/WorkSessionLifecycleTests.cs` | Explicit minimize/restore, no auto-minimize, warning choices, controlled close/cancel, metadata, progress/result/error/input/screen state retention across explicit minimize/restore, restart non-restore. | AC-019. |
| `tests/Dobby.UnitTests/Localization/ShellLocalizationCoverageTests.cs` | Visible shell/Profile/Settings/work-session keys resolve in `az-AZ`; `HostResourceKeys` and `Host.az-AZ.resx` stay in parity for Sprint 7 keys; `HostStringCatalog` uses the Dobby-owned `IStringLocalizer<T>` adapter path; missing key diagnostics behavior; no raw placeholder for required strings. | AC-017, AC-048. |
| `tests/Dobby.UnitTests/Navigation/NavigationTests.cs` | Existing fallback and built-in route identity tests retained/extended. | AC-023. |
| `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs` | Existing Sprint 6 source behavior retained; add only source-model assertions needed for rendered behavior. | AC-035. |
| `tests/Dobby.UnitTests/Host/AboutInfoProviderTests.cs` | Existing About provider field/privacy behavior retained for app version, build number/date, host edition/channel, redacted device code, plugin versions/status or empty list, support contact, legal line, neutral managed-user Owner text, and no license status exposure. | AC-035. |
| `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs` | Typed `ShellViewPreference` and authenticated preference scope exist in `Dobby.Configuration`, carry required shell preference state including Sidebar open node state, and do not expose concrete persistence/table access, global shell keys, or raw user identifiers. | AC-016, AC-023. |

### Integration tests
| Test file | Scenarios | AC coverage |
|---|---|---|
| `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs` | 1024x680 and 1920x1080 snapshots in light/dark; visible `TopToolbar`, `Sidebar`, `Third Bar`, main content, status/progress, work strip, rendered Settings/About category access, rendered About field set coverage for AC-035, and synthetic nested descriptor tree proving AC-023 hierarchy renders in `TopToolbar`/`Sidebar`/`Third Bar` without plugin activation. | AC-003, AC-016, AC-023, AC-035. |
| `tests/Dobby.IntegrationTests/Host/ShellThemeAndSettingsIntegrationTests.cs` | Theme preference persists through `HostConfiguration`; TopToolbar and Settings Appearance stay synchronized; Shell receives configuration-neutral theme state/commands. | AC-016, AC-035. |
| `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs` | Required shell preferences persist through existing authenticated-user-scoped `HostConfiguration` rows without new schema or global shell keys: sidebar collapse state, Sidebar open node state, Sidebar/Third Bar ordering, TopToolbar plugin ordering/hidden state, window size/position, and last active module storage without startup restore. | AC-016, AC-023. |
| `tests/Dobby.IntegrationTests/Host/ShellPreferenceUserIsolationTests.cs` | Two authenticated user scopes persist different shell view preferences and reload their own values without collision; assertions cover sidebar collapse/open node state, Sidebar/Third Bar ordering, TopToolbar plugin ordering/hidden state, window size/position, and last active module storage-without-startup-restore. | AC-016, AC-023. |
| `tests/Dobby.IntegrationTests/Host/ShellDiagnosticsExportActionTests.cs` | Authenticated export action creates local package, redacts/excludes secrets, records `diagnostics.export.created`, no upload/telemetry. | AC-033. |
| `tests/Dobby.IntegrationTests/Host/AzerbaijaniDiacriticsRoundTripTests.cs` | Azerbaijani diacritics across UI input/display, storage persistence, and audit-supported payloads; identifiers ordinal/normalized. | AC-048. |
| `tests/Dobby.IntegrationTests/Host/WorkSessionShellIntegrationTests.cs` | Navigation warning choices, lock preserves sessions, logout close/cancel cleanup, progress/result/error/input/screen state retained through shell restore, plugin disable placeholder behavior without activation. | AC-019, AC-003. |
| `tests/Dobby.IntegrationTests/Host/HostLoginRuntimeFactoryTests.cs` | Shell runtime composition after login; no pre-login plugin activation; background operations observable. | AC-003, AC-008 contribution, AC-016. |
| `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs` | Static scan fails on hardcoded visible shell/built-in UI strings and shell-level hardcoded color/spacing/font values outside approved resource/token surfaces. | AC-016, AC-017. |
| `tests/Dobby.IntegrationTests/Architecture/Sprint7ReleaseEvidenceTests.cs` | Sprint 7 evidence block exists and contains AC/evidence/build/test/format/no-new-surface markers, including exact Sprint 7 package approval/no-drift markers. | DoD evidence. |
| Existing architecture tests | Package governance including Sprint 7 pin parity for `dobby.sprint7.toolchainPins.v1`, ErrorCode baseline, Contracts no-WPF, encoding, project refs, unchanged existing `xunit.runner.visualstudio` / `coverlet.collector` test infrastructure, and no unapproved `Dobby.Shell -> Dobby.Configuration` edge. | Boundary preservation for AC-016/AC-017/AC-023/AC-033. |

### Smoke tests
| Test file | Scenarios | AC coverage |
|---|---|---|
| `tests/Dobby.SmokeTests/ShellSmokeTests.cs` | Built-in navigation still exposes Home/Plugins/Settings in stable order with localized labels. | AC-003, AC-023, AC-017. |
| `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs` | Keyboard-only Home/Plugins/Settings route movement, Sidebar collapse, theme switch, profile menu opening and reachability of profile/account info, password change, manual lock, logout, and saved-login clear, visible focus indicators, focus traversal, Esc/Enter/Tab/Shift+Tab behavior, and documented shortcuts. | AC-027, AC-040. |

### Manual / out-of-band test notes
- Local execution authority: when a local desktop/manual pass cannot be captured in the verification environment, rendered WPF automation at the same named resolution/DPI/theme/accessibility surfaces is an accepted local substitute only if the Sprint 7 evidence block explicitly labels it as a substitute and `Sprint7ReleaseEvidenceTests.cs` verifies the substitution marker. This does not remove the product expectation for manual smoke before a human release gate.
- Manual UI checks at documented resolutions/DPI: 1366x768, 1920x1080, 2560x1440, 3840x2160; 1920x1080 at 125% and 150% DPI; record in `artifacts/release-evidence/skeleton-local/architecture-baseline.md` Sprint 7 evidence (`docs/specs/shell-ui.md`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Verification / evidence).
- Manual single TopToolbar theme-switch flow: verify light/dark switch, reduced-motion/high-contrast expectations, and Settings Appearance synchronization (`docs/specs/design-system-assets.md`; `docs/specs/settings-and-about.md`).
- Manual/accessibility baseline: verify visible focus indicator, minimum contrast for light/dark shell and built-in screens, and basic role/label mapping for TopToolbar, Sidebar, Third Bar, theme switch, each required profile menu entry, and primary Settings controls; record in Sprint 7 evidence (`docs/07_ACCEPTANCE_CRITERIA.md` AC-027; `docs/specs/shell-ui.md` §8).
- Manual shell-state baseline: verify Toast/Banner/Modal notification presentation and loading/empty/success/error/confirmation feedback states on representative shell/built-in flows; record in Sprint 7 evidence without introducing later-sprint shared UI infrastructure.
- Manual Settings/About review: rendered categories and full About field set: app version, build number/date, host edition/channel, redacted device code, plugin versions/status or explicit empty-state boundary, support contact, legal line, managed-user neutral Owner identity, and no license import/status (`docs/specs/settings-and-about.md`; `docs/07_ACCEPTANCE_CRITERIA.md` AC-035).

Test count expectation vs previous sprint baseline:
- Sprint 6 evidence records 487 total tests, 0 failures, 0 skipped (`artifacts/release-evidence/skeleton-local/architecture-baseline.md` §Sprint 6 Implementation Evidence).
- Sprint 7 should add named unit/integration/smoke/evidence tests; final count should exceed 487 unless implementation replaces tests with an explicitly justified equivalent in release evidence.

## 15. Out of Scope
- Plugin package validation, install, activation runtime, manifest seeding of plugin `operationTargets`, plugin runtime fault isolation implementation, and plugin package trust are Sprint 8 work (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8; `docs/specs/plugins-manager.md`).
- Licensing import, license artifact validation, runtime-mode enforcement, ReadOnlyExpired behavior, and Sprint 1 print-boundary closure are Sprint 9 work (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9; `docs/specs/license-import.md`).
- Plugins Manager full list/install/enable/disable/uninstall UI, sample plugin, and Admin recovery-response tool are Sprint 10 work (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 10).
- Release gates, installer, production vendor key handover, and final release smoke consolidation are Sprint 11 work (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 11).
- Runtime language switching is P2, not Sprint 7 (`docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7).
- Work-session restore after restart is out of P1/Sprint 7 (`docs/specs/work-sessions.md`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7).
- Home/favorites polish, expanded shared UI controls, richer process reporting beyond minimum status/progress, and full audit viewer are P1-if-time/later, not Sprint 7 required closure (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Risks / Blockers; `docs/02_ARCHITECTURE_MAP.md` Future Scope).
- No new package pins, package sources, native assets, telemetry/online calls, Core `ErrorCode` values, audit event names, plugin-visible Contracts/SDK surfaces, schema widening, production cryptographic decisions, or new security/data-protection floors without Human decision required; existing `xunit.runner.visualstudio` / `coverlet.collector` references are not Sprint 7 scope-expansion approval (`Directory.Packages.props`; `docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `docs/specs/audit-and-diagnostics.md`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`).
- No direct OS/file/clipboard/dialog APIs from ViewModels; use Host adapters/coordinators (`docs/08_CODING_STANDARDS.md`).

## 16. Definition of Done
- Build/test/format:
  - `dotnet restore .\Dobby.sln`
  - `dotnet build .\Dobby.sln --configuration Release --no-restore -warnaserror`
  - `dotnet test .\Dobby.sln --configuration Release --no-build`
  - `dotnet format .\Dobby.sln --verify-no-changes`
  - Encoding/no-BOM test green (`docs/09_BUILD_RUN_AND_RELEASE.md`; `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs`).
- Architecture / boundary expectations:
  - `HD-S7-001` / `Q1` is closed before implementation starts and is limited to the exact Sprint 7 package set listed in §11/§13; `PackageGovernanceTests.cs` or `Sprint7ReleaseEvidenceTests.cs` verifies the `dobby.sprint7.toolchainPins.v1` approved representation/no-drift markers; implementation evidence proves no `Directory.Packages.props` drift beyond the approved set, no new package source, no native asset, unchanged existing `xunit.runner.visualstudio` / `coverlet.collector` infrastructure scope, and green package-governance tests unless separately approved (`tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`).
  - `ErrorCode` baseline remains unchanged (`tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`).
  - Contracts/SDK remain WPF-free and closed to new plugin-visible surface unless approved (`tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs`).
  - No schema widening without decision; `HostConfiguration` is the durable settings preference backing for authenticated-user-scoped required shell preferences, including sidebar collapse state, Sidebar open node state, Sidebar/Third Bar ordering, TopToolbar plugin ordering/hidden state, window size/position, and last active module storage-without-startup-restore; two-user isolation tests prove these values do not collide across authenticated users (`src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`; `tests/Dobby.IntegrationTests/Host/ShellPreferenceUserIsolationTests.cs`).
  - `Dobby.Navigation` owns navigation descriptor composition, and `Dobby.Shell` does not add a `Dobby.Configuration` reference (`docs/02_ARCHITECTURE_MAP.md`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`).
  - `ShellUiStaticScanTests.cs` passes for hardcoded visible shell/built-in UI strings and shell-level hardcoded color/spacing/font values.
  - Shell layout and snapshot evidence covers Toast/Banner/Modal notification taxonomy, loading/empty/success/error/confirmation feedback states, and AC-023 nested hierarchy rendering in `TopToolbar`/`Sidebar`/`Third Bar` without plugin activation.
- Per-AC behavior expectations:
| AC | Named test/evidence anchor |
|---|---|
| AC-003 | `ShellSnapshotTests.cs`; manual resolution/DPI evidence in `artifacts/release-evidence/skeleton-local/architecture-baseline.md`. |
| AC-016 | `ShellThemeAndSettingsIntegrationTests.cs`; `ShellThemePreferenceTests.cs`; `SettingsPreferenceSurfaceTests.cs`; `SettingsPreferencePersistenceTests.cs`; `ShellPreferenceUserIsolationTests.cs`; `ShellUiStaticScanTests.cs`; theme snapshot evidence. |
| AC-017 | `ShellLocalizationCoverageTests.cs`; `ShellUiStaticScanTests.cs`; `EncodingBoundaryTests.cs`; `HostResourceKeys` / `Host.az-AZ.resx` parity; Dobby-owned `IStringLocalizer<T>` adapter assertions; missing-key diagnostics assertions. |
| AC-019 | `WorkSessionLifecycleTests.cs`; `WorkSessionShellIntegrationTests.cs`; evidence must include progress/result/error/input/screen state retention across explicit minimize/restore and no restart restore. |
| AC-023 | `NavigationComposerTests.cs`; `NavigationTests.cs`; `ShellSnapshotTests.cs` synthetic nested descriptor render evidence; `SettingsPreferencePersistenceTests.cs`; `ShellPreferenceUserIsolationTests.cs`. |
| AC-027 | `ShellKeyboardSmokeTests.cs` for Home/Plugins/Settings, Sidebar collapse, theme switch, profile menu inventory (profile/account info, password change, manual lock, logout, saved-login clear), shortcuts/focus traversal, and visible focus indicators; manual accessibility evidence for minimum contrast and role/label mapping, including each required profile menu entry. |
| AC-035 | Rendered Settings/About snapshots/manual evidence for Appearance, Account, Security/Session, User Management, and About; About evidence covers app version, build number/date, host edition/channel, redacted device code, plugin versions/status or explicit empty-state boundary, support contact, legal line, managed-user neutral Owner identity, and no license import/status; existing `SettingsAndAboutViewModelTests.cs` and `AboutInfoProviderTests.cs` retained for source behavior. |
| AC-040 | `ShellKeyboardSmokeTests.cs`; manual keyboard-only evidence. |
| AC-048 | `AzerbaijaniDiacriticsRoundTripTests.cs`; localization/storage/audit evidence rows. |
| AC-033 contribution | `ShellDiagnosticsExportActionTests.cs`; existing `DiagnosticsExportBoundaryTests.cs`; audit row for `diagnostics.export.created`. |
- Evidence expectations:
  - Append Sprint 7 implementation evidence to `artifacts/release-evidence/skeleton-local/architecture-baseline.md`.
  - Evidence block must include final command results, AC mapping, UI snapshot/manual rows, profile-menu inventory evidence for profile/account info, password change, manual lock, logout, and saved-login clear, notification taxonomy and feedback-state verification, AC-023 nested hierarchy render evidence, AC-019 progress/result/error/input/screen state-retention evidence, `HD-S7-001` / `Q1` closure evidence, Sprint 7 package-governance parity/no-drift verification, unchanged existing `xunit.runner.visualstudio` / `coverlet.collector` infrastructure scope, `Q8` dispatcher placement constraint, `Q9` Host namespace-placement constraint, no `Directory.Packages.props` drift, no new package source, no native asset, no-new-schema, no-new-Core-ErrorCode, no-new-audit-event, no-Contracts/SDK-widening, plugin activation out-of-scope statement, and any implementation assumptions accepted/closed.
  - AC-035 evidence block must name the rendered Settings/About snapshot or manual artifact that shows the full About field set: app version, build number/date, host edition/channel, redacted device code, plugin versions/status or explicit empty-state boundary, support contact, legal line, managed-user neutral Owner identity, and no license import/status.
  - Localization evidence must show `HostResourceKeys`, `HostStringCatalog`, and `Host.az-AZ.resx` are consolidated through the Dobby-owned `IStringLocalizer<T>` adapter path with required Sprint 7 `.resx` parity; deferral requires Human decision required before closing Sprint 7.
  - Evidence block must include shell preference persistence closure, two-user shell preference isolation closure, hardcoded UI/static token scan result, Navigation/Shell ownership statement, no `Dobby.Shell -> Dobby.Configuration` edge statement, and no `Dobby.Host.Shell` / `Dobby.Host.Diagnostics` namespace-placement drift unless coding-standards authority was recorded before creation.
  - Add `tests/Dobby.IntegrationTests/Architecture/Sprint7ReleaseEvidenceTests.cs` to assert evidence markers.
- Boundary preservation expectations:
  - No runtime language switching.
  - No plugin activation/package validation.
  - No license import/enforcement behavior.
  - No work-session restart restore.
  - No telemetry/vendor upload.
- Open items allowed at close: none for primary Sprint 7 ACs. P1-if-time `AC-011`/`AC-012`/`AC-013` may remain unimplemented if evidence records them as non-blocking per master plan (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 / Acceptance and exit expectations).

## 17. Reviewer Focus
1. Verify Sprint 7 scope stayed inside master-plan boundaries by checking final diff against `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 and no Sprint 8/9/10 implementation surfaces.
2. Inspect `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs` and snapshot artifacts/evidence for 1024x680 and 1920x1080 light/dark shell rendering.
3. Inspect manual UI evidence rows in `artifacts/release-evidence/skeleton-local/architecture-baseline.md` for documented resolutions/DPI, keyboard/accessibility checks, and required profile menu entries.
4. Inspect `tests/Dobby.UnitTests/Navigation/NavigationComposerTests.cs` for Home-first/Settings-last, hierarchy, order, override reset, fallback coverage, and Navigation-owned composition.
5. Inspect `tests/Dobby.IntegrationTests/Host/ShellThemeAndSettingsIntegrationTests.cs`, `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs`, `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`, and `tests/Dobby.IntegrationTests/Host/ShellPreferenceUserIsolationTests.cs` for shared TopToolbar/Settings Appearance preference persistence, authenticated-user-scoped shell preference persistence through `HostConfiguration`, and two-user no-collision coverage.
6. Inspect `tests/Dobby.UnitTests/Localization/ShellLocalizationCoverageTests.cs`, `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`, and `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs` for visible-string localization, no hardcoded visible shell/built-in UI strings, no shell-level hardcoded color/spacing/font values, `az-AZ`, `HostResourceKeys` / `Host.az-AZ.resx` parity, Dobby-owned `IStringLocalizer<T>` adapter behavior, missing-key diagnostics, and UTF-8/no-BOM.
7. Inspect `tests/Dobby.UnitTests/Shell/WorkSessionLifecycleTests.cs` and `tests/Dobby.IntegrationTests/Host/WorkSessionShellIntegrationTests.cs` for explicit minimize/restore, warning choices, close/cancel, lock/logout, and restart non-restore.
8. Inspect `tests/Dobby.IntegrationTests/Host/ShellDiagnosticsExportActionTests.cs`, `tests/Dobby.IntegrationTests/Diagnostics/DiagnosticsExportBoundaryTests.cs`, and `src/Dobby.Audit/AuditEventName.cs` for `diagnostics.export.created`, local package, redaction, and no upload.
9. Inspect `tests/Dobby.IntegrationTests/Host/AzerbaijaniDiacriticsRoundTripTests.cs` for AC-048 evidence across UI/storage/audit-supported paths.
10. Inspect architecture tests: `ArchitectureDependencyBoundaryTests.cs`, `ContractsCoreSurfaceTests.cs`, `PackageGovernanceTests.cs`, `PluginAndSdkBoundaryTests.cs`, and `EncodingBoundaryTests.cs`, including Sprint 7 package pin parity/no-drift coverage, unchanged `xunit.runner.visualstudio` / `coverlet.collector` infrastructure scope, and no unapproved `Dobby.Host.Shell` / `Dobby.Host.Diagnostics` namespace-placement drift.
11. Inspect `artifacts/release-evidence/skeleton-local/architecture-baseline.md` Sprint 7 block and `tests/Dobby.IntegrationTests/Architecture/Sprint7ReleaseEvidenceTests.cs` for final verification closure, package approval/no-drift markers, and localization `.resx` parity evidence.
12. Inspect `tests/Dobby.UnitTests/Host/AboutInfoProviderTests.cs`, rendered Settings/About snapshots, and manual AC-035 evidence for the full About field set and no license import/status exposure.

## 18. Final Execution Checklist

### Before starting
- Confirm `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 is the sprint scope authority.
- Confirm `SPRINT_6_EXECUTION_PLAN.md` and Sprint 6 evidence are read; do not assume earlier plans were implemented without current code/evidence.
- Confirm `D-20` and `D-21` in `artifacts/release-evidence/skeleton-local/decision-log.md`.
- Confirm `HD-S7-001` / `Q1` exact package approval is closed in repository-backed evidence and `Q8` dispatcher placement constraint is recorded in §13.
- Confirm no new package/version/source/native asset is needed beyond the approved set and no `xunit.runner.visualstudio` / `coverlet.collector` scope expansion is planned; otherwise stop for Human decision required.
- Confirm no schema widening, global shell preference keys, user-colliding preference keys, PII-bearing raw user identifiers in persisted preference keys, Core `ErrorCode`, audit event, Contracts/SDK surface, or route identity migration is planned; otherwise stop for Human decision required.
- Confirm no `Dobby.Shell -> Dobby.Configuration` project reference and no move of navigation descriptor composition out of `Dobby.Navigation` is planned; follow the enforced `Dobby.Shell` allowed-reference row and `ArchitectureDependencyBoundaryTests.cs`, not any architecture-map reverse-column inconsistency; otherwise stop for Human decision required.
- Confirm `ShellThemeCoordinator.cs` and `ShellDiagnosticsExportCoordinator.cs` remain in flat `Dobby.Host`; if implementation proposes `Dobby.Host.Shell`, `Dobby.Host.Diagnostics`, or another new public/plugin-visible or skeleton-critical Host namespace placement, stop for coding-standards namespace-placement authority before creating it.
- Confirm `docs/assets/ui_design_light_theme.jpg` is used only as non-normative visual context for composition/light-theme direction; do not derive measurements, labels, plugin/license defaults, or scope from the image.

### During implementation
- Write tests before implementation code for each primary AC.
- Keep WPF/UI code in Windows-targeted Host/Shell/UI/test projects only.
- Keep file dialogs, clipboard, and OS APIs in Host adapters/coordinators, keep dispatcher implementation Shell-wired per Assumption A7, and keep all of them out of ViewModels.
- Keep Host-owned shell/theme and diagnostics coordinators in the existing flat `Dobby.Host` namespace unless `docs/08_CODING_STANDARDS.md` §5 authority is recorded first.
- Use centralized localization keys, required `Host.az-AZ.resx` parity, Dobby-owned `IStringLocalizer<T>` adapter behavior, and `az-AZ` strings; no hardcoded visible strings in shell XAML/code.
- Use design-system tokens; no per-screen theme colors or theme-specific layout.
- Model and verify Toast/Banner/Modal notification taxonomy and loading/empty/success/error/confirmation feedback states without creating later-sprint shared UI infrastructure.
- Keep required shell preferences in `Dobby.Configuration` typed state with authenticated-user-scoped Host-wired `HostConfiguration` persistence; no non-durable-only shell preference state and no global shell preference keys.
- Keep Navigation-owned descriptor composition separate from Shell-owned rendering/hosting.
- Preserve `Dobby.Contracts` and `Dobby.SDK` plugin-facing no-WPF boundary.
- Preserve `Dobby.Core/ErrorCode` and audit event closed sets.
- Do not implement plugin activation, package validation, licensing, Admin tools, sample plugin, installer, or release-gate consolidation.
- Do not persist work sessions across restart.
- Apply the no-invented-names rule: any new file/surface must map to §7, §7a, current code, or a cited doc/ADR/spec; otherwise stop and record an assumption or Human decision required.

### Before finishing
- All named Sprint 7 tests in §14 are green.
- `ShellUiStaticScanTests.cs` proves hardcoded visible shell/built-in UI strings and shell-level hardcoded color/spacing/font values are blocked.
- `ShellLocalizationCoverageTests.cs` proves `HostResourceKeys` / `Host.az-AZ.resx` parity, Dobby-owned `IStringLocalizer<T>` adapter lookup/fallback behavior, and missing-key diagnostics.
- Package-governance or evidence tests prove Sprint 7 `dobby.sprint7.toolchainPins.v1` parity/no-drift and unchanged existing `xunit.runner.visualstudio` / `coverlet.collector` infrastructure scope.
- `ShellKeyboardSmokeTests.cs` and manual accessibility evidence prove AC-027 keyboard-only Home/Plugins/Settings, Sidebar collapse, theme switch, profile menu inventory (profile/account info, password change, manual lock, logout, saved-login clear), visible focus indicators, minimum contrast, and role/label mapping for each required profile menu entry.
- `ShellLayoutStateTests.cs` and shell/manual evidence prove Toast/Banner/Modal notification taxonomy and loading/empty/success/error/confirmation feedback states.
- `ShellSnapshotTests.cs` proves a synthetic nested descriptor tree renders through `TopToolbar`, `Sidebar`, and `Third Bar` without plugin activation for AC-023.
- `WorkSessionLifecycleTests.cs` and `WorkSessionShellIntegrationTests.cs` prove progress/result/error/input/screen state retention across explicit minimize/restore and no restart restore.
- `SettingsPreferencePersistenceTests.cs` and `ShellPreferenceUserIsolationTests.cs` prove required shell preferences, including Sidebar open node state, persist through authenticated-user-scoped `HostConfiguration` keys without schema widening or cross-user collision.
- Full restore/build/test/format commands from `docs/09_BUILD_RUN_AND_RELEASE.md` are run and recorded.
- `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs` confirms UTF-8/no-BOM.
- `artifacts/release-evidence/skeleton-local/architecture-baseline.md` has Sprint 7 evidence with AC mapping, snapshots/manual rows, command results, package approval/no-drift markers, localization `.resx` parity evidence, and no-new-surface statements.
- `tests/Dobby.IntegrationTests/Architecture/Sprint7ReleaseEvidenceTests.cs` verifies the evidence block, including package approval/no-drift markers and localization `.resx` parity evidence.
- Rendered Settings/About snapshot or manual evidence proves the full AC-035 About field set and no license import/status exposure.
- No `Dobby.Host.Shell` or `Dobby.Host.Diagnostics` namespace-placement drift exists unless the coding-standards authority evidence was recorded before creation.
- No unresolved Human decision required gates remain.

## 19. Planning Coverage and Confidence
| Topic | Coverage | Justification |
|---|---|---|
| Sprint scope and sequencing | High | Master Sprint 7 section, previous plans, and later sprint sections were inspected and cited. |
| Prior sprint continuity | High | Sprint 5/6 execution plans, current code, tests, and Sprint 6 release evidence were inspected. |
| Shell/UI implementation state | High | Current Host/Shell/UI XAML/code, specs, ADRs, and tests were inspected. |
| Navigation/route hierarchy | High | ADR-0005/0013/0049, architecture-map Navigation ownership, current `RouteRegistry`, comparer, schema, and tests were inspected. |
| Theme/design system | High | Design-system spec, non-normative `docs/assets/ui_design_light_theme.jpg` visual context, UI token files, theme dictionaries, package pins, settings preference code, and `Dobby.Shell` dependency limits were inspected; exact Sprint 7 package approval is closed by `HD-S7-001` / `Q1`. |
| Localization/diacritics | High | Localization docs, current catalog/resx, ADR-0044, Sprint 5 deferral, diagnostics event, and acceptance criteria were inspected; Sprint 7 now requires `HostResourceKeys` / `Host.az-AZ.resx` parity and the Dobby-owned `IStringLocalizer<T>` adapter path, while final rendered diacritics evidence remains implementation-time evidence. |
| Work sessions | High | Work-session spec/ADR, current registry, and test baseline were inspected. |
| Diagnostics export action | High | Diagnostics/audit spec, builder/redactor, audit event names, and diagnostics tests were inspected. |
| Package/toolchain governance | High | Central package pins, package-governance tests, test project package references, and toolchain evidence were inspected; `HD-S7-001` / `Q1` approves only the exact Sprint 7 UI/MVVM/localization/snapshot/test package set, treats existing `xunit.runner.visualstudio` / `coverlet.collector` references as unchanged infrastructure only, and preserves separate approval for any new package/source/native asset/version/scope drift. |
| Storage/schema impact | Medium | Existing `HostConfiguration` schema and settings persistence tests were inspected; Sprint 7 must add typed shell preferences, authenticated user scope, and HostConfiguration-backed key/value persistence with two-user isolation evidence, with Human decision required only if implementation needs schema widening, global/user-colliding shell preference keys, PII-bearing persisted keys, or a new preference authority. |
| Snapshot/manual UI evidence | Medium | Required evidence is clear and the light-theme visual reference is non-normative context only, but actual screenshots/manual DPI checks are implementation-time artifacts. |
| Later-sprint boundary protection | High | Sprint 8/9/10/11 master sections and plugin/license specs were inspected for boundaries. |

Confidence limits:
- Implementation-time evidence remains required for rendered WPF snapshots, DPI/manual checks, keyboard/accessibility smoke, and final command results.
- `docs/assets/ui_design_light_theme.jpg` remains non-normative visual context only; implementation must not derive measurements, labels, plugin/license defaults, or scope from it.
- Later sprint responsibilities remain outside Sprint 7: plugin activation/package trust, licensing/runtime mode, Plugins Manager/sample plugin/Admin tools, installer/release smoke consolidation.
- `HD-S7-001` / `Q1` is closed for Sprint 7 planning. Conditional stop gates remain if implementation proposes package/source/native asset/version/scope drift, schema widening, global/user-colliding shell preference keys, PII-bearing persisted keys, route identity migration, Core/audit/Contracts closed-surface widening, telemetry/upload, Host-owned dispatcher placement, new Host namespace-placement drift, or scope movement.

Planning stance: READY FOR IMPLEMENTATION with `HD-S7-001` / `Q1` closed, `Q2`-`Q9` retained as source-grounded planning constraints, and seven implementation assumptions retained, with A7 reconciled to Shell-wired dispatcher placement.
