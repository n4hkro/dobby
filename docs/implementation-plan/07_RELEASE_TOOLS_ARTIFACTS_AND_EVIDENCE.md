# Release Tools, Artifacts and Evidence Execution Plan

**Dependencies:** security/platform workstreams complete before GA evidence  
**Completion blockers addressed:** F-021 through F-025; legacy rows 57–69

## RL-01 — Produce a production-complete sample plugin

**Priority:** P1-High  
**Depends on:** PL-02 through PL-07, UI-04

### Files to inspect/modify

- `samples/Dobby.Sample.Plugin/Dobby.Sample.Plugin.csproj`
- `samples/Dobby.Sample.Plugin/SamplePlugin.cs`
- `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json`
- sample assets/localization bundles
- sample manifest, integration and release-evidence tests

### Steps

1. Upgrade the sample to manifest/API v2 and the declarative plugin component
   contract. Demonstrate localized navigation/page, validated form, table/list,
   asynchronous cancellable action, progress, typed denial/result and an actual
   plugin exception isolated by the host.
2. Make work-session lifecycle balanced: every created session is completed/
   closed or deliberately kept with visible ownership; disable/uninstall cleans
   all sessions and operations.
3. Build the DLL/assets first, compute real SHA-256 values, generate the canonical
   manifest, sign with an explicit TEST-ONLY release-test profile, package and
   independently verify. Never commit `000...` hashes or `AQ` signatures as the
   positive sample.
4. Keep tampered/dummy packages only under clearly named negative fixtures.
5. Add a clean-root behavior test that installs through Plugins Manager, enables,
   navigates, changes culture, executes/cancels, minimizes/restores/closes, disables,
   restarts, re-enables and uninstalls.

### Acceptance

- The positive `.dobbyplugin` is accepted by the independent verifier and the
  production install path.
- Runtime evidence proves all sample behaviors and leaves no session/operation/
  installed-state residue after uninstall.

## RL-02 — Complete deterministic admin and verification tools

**Priority:** P1-High  
**Depends on:** PL-03, LC-01/02, ID-06

### Files to inspect/modify/create

- `src/Dobby.Admin/Commands/AdminCommandHost.cs`
- `tools/sign-plugin-package/*`
- `tools/create-plugin-license/*`
- `tools/create-recovery-response/*`
- new `tools/verify-plugin-package/*`
- tool unit/integration/static-scan tests

### Steps

1. Give every command non-interactive deterministic parameters, help, stable exit
   codes and JSON/text-safe output. Never print private keys, artifact bodies or
   sensitive full paths.
2. Update signer/license/recovery tools to the shared purpose-separated key-ring
   and canonical P1363/base64url profile. Support explicit TEST-ONLY fixtures and
   reject them under Production profile.
3. Implement `verify-plugin-package` as an independent command/wrapper over the
   same production validation policy without installing or executing the plugin.
   It reports safe per-stage results and nonzero exit for any rejection.
4. Generate license-v3 fixtures for Granted/Revoked and boundary dates/revisions;
   do not generate lifecycle-derived labels as signed authority.
5. Update recovery tool for request-bound v2 response and proof; it must consume a
   request file rather than accepting independently retyped device fields.
6. Add round-trip plus cross-tool negative tests: sign→verify, create→import,
   recovery request→response→consume; wrong purpose/key/profile/tamper fails.
7. Pin tool/runtime build versions in evidence and make wrappers discover the
   repository/tool output without hard-coded developer machine paths.

### Acceptance

- Tools are scriptable, deterministic and safe in logs.
- Independent verification accepts the exact positive sample hash and rejects all
  negative fixtures.
- Production mode cannot use a committed TEST-ONLY key/profile.

## RL-03 — Enforce portable as a support/development artifact

**Priority:** P1-Medium  
**Depends on:** Phase 1 security work, UI-04

### Files to inspect/modify

- `tools/publish-portable/publish-portable.ps1`
- `tools/publish-portable/README.md`
- `src/Dobby.Configuration/RuntimeEnvironmentName.cs`
- `src/Dobby.Host/appsettings.json` and startup/about metadata
- portable script/static/launch tests

### Steps

1. Make the script produce a clean versioned output directory from Release bits,
   fail on stale files and write a manifest containing commit, version, runtime,
   file hashes and `artifactMode=PortableSupport`.
2. Embed/read artifact mode at runtime. Display it in About and diagnostics;
   reject any production-only assumption that the portable artifact satisfies the
   MSIX/GA gate.
3. Define storage behavior explicitly. Default to an isolated supplied support
   data root or a clearly identified local profile; never silently operate on a
   customer profile during automated smoke tests.
4. Scan output for PDB/source maps/test keys, placeholder contacts, developer
   paths, negative fixtures and unintended environment settings.
5. Launch the published executable in a fresh profile and an explicitly copied
   migration fixture; prove login-first, encrypted store and clean exit.
6. Refresh portable output after every development behavior change using
   `.\tools\publish-portable\publish-portable.ps1 -NoRestore` when assets permit,
   and report the exact executable/hash.

### Acceptance

- Portable support constraints are enforced and visible in the artifact, not only
  in README prose.
- Clean/fresh launch tests use candidate bits and never touch real user data.

## RL-04 — Prove MSIX install, upgrade and data preservation

**Priority:** P0 for GA  
**Depends on:** all Standalone behavior work, RL-01/02

### Files to inspect/modify/create

- `installer/msix/build-rc-msix.ps1`
- `installer/README.md`
- new versioned MSIX manifest/assets/configuration templates as appropriate
- new `tools/verify-msix-install/*`
- packaging/installed-smoke tests and evidence manifests

### Steps

1. Remove hard-coded SDK/tool version candidates and developer paths. Discover an
   approved Windows SDK deterministically and fail with actionable prerequisites.
2. Freeze package family/name/publisher, architecture, minimum Windows version,
   execution level, capabilities, upgrade identity and uninstall/data policy.
3. Replace placeholder graphics/metadata and stage only an allow-listed publish
   manifest. Scan for secrets, test keys, fixtures, source and portable-only config.
4. Build version N and N+1 with identical upgrade identity and distinct product
   version. Hash/sign each candidate and preserve logs.
5. In a disposable Windows test account/VM: clean install N; launch installed
   entry point; create known encrypted app/user/plugin/license state; exit; upgrade
   to N+1; launch; authenticate; verify migration and exact data; exercise repair/
   uninstall policy. Capture package identity and resolved runtime roots.
6. Explicitly test packaged file/registry virtualization assumptions. Verify that
   canonical `%LocalAppData%\Dobby` data is where runtime expects it and is not
   redirected/split in a way that breaks upgrade or support diagnostics.
7. Test install/launch over a supported existing Standalone data fixture and a
   rejected/corrupt fixture with safe recovery. Never use real customer data.

### Acceptance

- Evidence is from the installed package, not the staged `Dobby.Host.exe`.
- N→N+1 preserves expected app data and runs migrations once.
- Package identity/storage locations are recorded and match runtime/support docs.

## RL-05 — Production signing, supply chain and customer-content gate

**Priority:** P0 for GA  
**Depends on:** RL-01 through RL-04

### Owner inputs required

- production MSIX certificate/publisher and timestamp policy;
- production public key rings and controlled private-key/signing service access;
- real support contact, legal/owner metadata and distribution channel identity.

Implementation may proceed with TEST-ONLY material, but GA cannot pass without
the owner inputs.

### Steps

1. Separate development/test/production signing profiles. Production private keys
   are external, least-privilege and never passed in logs/command line when a
   protected input/signing service is available.
2. Verify publisher/certificate chain/timestamp and all artifact signatures after
   creation. Produce SHA-256 manifest for MSIX, portable support artifact, sample
   plugin and admin tools.
3. Generate SBOM/dependency inventory; run vulnerability and license review using
   pinned tool/database provenance. Resolve or explicitly owner-accept findings.
4. Run a customer-content scan for placeholder domains/graphics, TEST-ONLY keys,
   dummy hashes/signatures, developer paths, raw exception/payload data, unneeded
   symbols and forbidden files.
5. Document key rotation/revocation and emergency release procedure without
   embedding secrets.

### Acceptance

- Candidate artifact identities, hashes, signatures, certificate/profile and SBOM
  refer to the exact tested bytes.
- No placeholder/test-only/customer-unsafe material remains.

## RL-06 — Consolidate a reproducible evidence runner

**Priority:** P1  
**Depends on:** all preceding release tasks

### Files to inspect/modify/create

- existing `tests/Dobby.IntegrationTests/Architecture/*ReleaseEvidenceTests.cs`
- `artifacts/release-evidence/` structure
- new `tools/verify-security-regressions/*`
- new `tools/verify-release-candidate/*`
- CI workflow/configuration if repository-managed CI is later added

### Steps

1. Define a new empty evidence directory per commit/version/run. Never mix old
   evidence into a candidate.
2. Runner records environment/toolchain, git commit/status, restore provenance,
   command line, exit code, duration, test counts, artifact paths and hashes.
3. Execute format, Release build, unit/integration/smoke, flake-sensitive repeats,
   malicious artifact suites, portable publish/launch, plugin verification and
   MSIX installed scenarios. Stop on failure; never convert a failed command to a
   passing markdown claim.
4. Produce one machine-readable manifest and a short human summary linked to raw
   logs. Validate that all referenced files exist and hashes match.
5. Retire/mark historical sprint evidence as historical. Source/static scans stay
   supplemental and cannot close behavior/package gates.

### Acceptance

- A reviewer can reproduce the candidate from the manifest and independently
  verify every artifact hash/result.
- Missing, stale or mismatched evidence fails the release gate.

## Workstream Verification

```powershell
dotnet format .\Dobby.sln --verify-no-changes --no-restore
dotnet build .\Dobby.sln -c Release --no-restore
dotnet test .\Dobby.sln -c Release --no-restore
.\tools\publish-portable\publish-portable.ps1 -NoRestore
.\tools\verify-release-candidate\verify-release-candidate.ps1 -NoRestore -EvidenceRoot <new-empty-directory>
```

Report exact portable executable and MSIX paths, hashes and installed test
environment. A successful self-signed RC remains RC evidence, not GA completion.
