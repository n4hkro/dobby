# Sprint 1 Toolchain Pin Gate

## Sources

- `SPRINT_1_EXECUTION_PLAN.md`
- `SPRINT_IMPLEMENTATION_PLAN.md`
- `global.json`
- `Directory.Packages.props`
- `src/**/*.csproj`
- `tests/**/*.csproj`
- `samples/**/*.csproj`
- `docs/03_TECH_STACK_AND_VERSIONS.md`
- `docs/09_BUILD_RUN_AND_RELEASE.md`

## Package Pin Approval Status

- `dotnet --info` observed SDK `10.0.202`, matching the repository `global.json` pin.
- No repository-local human-approved SDK/package pin list was supplied during Sprint 1 execution.
- Existing `Directory.Packages.props` and `.csproj PackageReference` entries are treated as current repository state, not as approval evidence by themselves.
- Sprint 1 package governance tests enforce central package management, absence of inline versions, Tier 1 test-only package isolation, Tier 0 BCL/runtime-only non-reference rules, and Tier R rejected/deferred package absence.
- Automated approved-pin assertions were not introduced during Sprint 1 because no human-approved test-readable pin list or stop-condition representation was supplied.
- Sprint 4 adds a candidate storage pin representation below, but package-bearing work remains blocked until the representation is human-approved, all required fields are filled, and automated/test-readable enforcement is added where required by the implementation plan.
- Sprint 7 adds exact package approval evidence below for `HD-S7-001` / `Q1`; the approval is limited to the listed Sprint 7 runtime UI/MVVM/localization and test-only snapshot/unit/integration package scope.
- Final verification on 2026-05-11: `dotnet restore Dobby.sln` passed; package governance integration tests passed as part of `dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"`.

## Known Blockers

- `TOOLCHAIN-PIN-APPROVAL-MISSING`: during Sprint 1, no repository-local human-approved SDK/package pin list was supplied. Existing `Directory.Packages.props` entries were not treated as approval evidence by themselves.
- `TOOLCHAIN-PIN-REPRESENTATION-MISSING`: during Sprint 1, no human-approved test-readable pin-list or stop-condition representation was supplied, so automated approved-pin assertions were intentionally not introduced as committed failing tests.
- Architecture blockers are tracked in architecture-baseline.md

## Sprint 4 Candidate Pin Representation

Status: APPROVED - Sprint 4 storage package/native asset scope and evidence were human-approved for Phase A closure on 2026-05-14 by `n4hkro (architecture/security/release approver)`.

Sprint 4 must not add package-bearing storage changes until each selected row has an approved version/asset identifier, source URL/commit/tag, package or asset URL, license stance, vulnerability scan date, local SHA-256, approver and decision link. Upstream signed/prebuilt native assets and self-built native assets require the same base evidence fields; self-built assets additionally require reproducible Windows native build evidence. Version numbers and latest-stable status must be re-checked on the approval date; candidate values in discussion are not approval evidence by themselves.

| Package / Asset | Requirement | Version / Asset Identifier | Source URL / Commit / Tag | Package / Asset URL | SHA-256 | NuGet SHA-512 / Package Hash | NuGet Repository Signature | NuGet Author Signature / Risk | Native Upstream Digest | Native Tag / Release Provenance | License | Vulnerability Scan Date | Approver | Decision Link | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Microsoft.Data.Sqlite.Core` | Required for Sprint 4 storage provider | `10.0.8` | `https://github.com/dotnet/dotnet` commit `94ea82652cdd4e0f8046b5bd5becbd11461482ca` | `https://api.nuget.org/v3-flatcontainer/microsoft.data.sqlite.core/10.0.8/microsoft.data.sqlite.core.10.0.8.nupkg` | `dc31e393411cfabf62134176a649c5c60993bab4472ef863c0e44a48eb335656` | SHA512 `3gV3P+2kxD/pAkd9Nc07SKYzuBNMCrkQxQMFvgG96vFWOHN0H8j+ZfYPhFGpxIEkx2XZTpqecgP8XEpLWHg9Cw==` | verified; signer: NuGet.org Repository by Microsoft | verified; signer: Microsoft Corporation | N/A | N/A | MIT | 2026-05-14; NuGet advisory index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14` | APPROVED |
| `SQLitePCLRaw.core` | Required for Sprint 4 provider wiring | `3.0.3` | `https://github.com/ericsink/SQLitePCL.raw` commit `f3f967adafad853375597a6387cacb7573c5b343` | `https://api.nuget.org/v3-flatcontainer/sqlitepclraw.core/3.0.3/sqlitepclraw.core.3.0.3.nupkg` | `30bb8decacdf856c955f78b8a62e79290a7d27c251f37b020af79e52eb032aac` | SHA512 `6XZEy8awVSZJgIbWfuQU6w1c4wpFHHKQaLEzPwF4YSKiz/k+0p6HyyW5Ne/B6ZSPPSIHxuKtVnMB0uhAUpYn6g==` | verified; signer: NuGet.org Repository by Microsoft | absent; risk accepted via NuGet repository signature only | N/A | N/A | Apache-2.0 | 2026-05-14; NuGet advisory index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14` | APPROVED |
| Approved native encrypted SQLite provider asset/package | Required for Sprint 4 DB-at-rest encryption; preferred path is canonical upstream `SQLite3MultipleCiphers` release artifact when evidence is complete, otherwise self-built reproducible Windows native asset | `SQLite3MultipleCiphers v2.3.4` / `sqlite3mc-2.3.4-sqlite-3.53.1-win64.zip` | `https://github.com/utelle/SQLite3MultipleCiphers/releases/tag/v2.3.4`; tag object `6cc1882e85731a3e4d12c746a941d5ac434fe1bc`; release commit `04a7a56392d77fc46007f0023d3eb4b5b701474b` | `https://github.com/utelle/SQLite3MultipleCiphers/releases/download/v2.3.4/sqlite3mc-2.3.4-sqlite-3.53.1-win64.zip` | `f685ab7aed02f79985c6876b7dfa463bb81ebc1f6acd3ce536382cc276467b15` | N/A | N/A | N/A | `sha256:f685ab7aed02f79985c6876b7dfa463bb81ebc1f6acd3ce536382cc276467b15` | GitHub API tag verification `verified=true`, `reason=valid`, `verified_at=2026-05-06T08:29:44Z`; endpoints `/repos/utelle/SQLite3MultipleCiphers/git/refs/tags/v2.3.4` and `/repos/utelle/SQLite3MultipleCiphers/git/tags/6cc1882e85731a3e4d12c746a941d5ac434fe1bc`; GPG fingerprint capture unavailable | MIT | 2026-05-14; OSV/NVD SQLite 3.53.1 0 hits; GitHub advisories/issues 0 hits; release notes reviewed | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14` | APPROVED |
| `SQLitePCLRaw.provider.dynamic_cdecl` shim | Required for explicit provider wiring | `3.0.3` | `https://github.com/ericsink/SQLitePCL.raw` commit `f3f967adafad853375597a6387cacb7573c5b343` | `https://api.nuget.org/v3-flatcontainer/sqlitepclraw.provider.dynamic_cdecl/3.0.3/sqlitepclraw.provider.dynamic_cdecl.3.0.3.nupkg` | `ecc9f8cc6f100c7da4155a5d7b73f4fc4400fe78fc4dfd5589741ca75f71fcf8` | SHA512 `AJFBFJLq0r7UEojrZ+IFO74lQNZ/0HiWPyT3qe9mtc1q1tLwmj88/32TU4lIXTVd9UV9xcTSLUNjGZC4t1iK/A==` | verified; signer: NuGet.org Repository by Microsoft | absent; risk accepted via NuGet repository signature only | N/A | N/A | Apache-2.0 | 2026-05-14; NuGet advisory index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14` | APPROVED |
| `Dapper` | Selected for Sprint 4 storage implementation; package reference blocked until this row is complete | `2.1.72` | `https://github.com/DapperLib/Dapper` commit `288730e69b05c32cac898d9b55ebea219ea8a2d1` | `https://api.nuget.org/v3-flatcontainer/dapper/2.1.72/dapper.2.1.72.nupkg` | `f422b2cfbdbf569e7f188a3127397b4405d38f832f2938058bfedf1b2851a9fe` | SHA512 `LKgcsnSLUmi2E1tKOuVFB158aQ8unGNXHpZ5Dj399Ou8wTCMX2o+5WQd9wdQflgNNgUgahOf+NjYnEqF/KOhKQ==` | verified; signer: NuGet.org Repository by Microsoft | absent; risk accepted via NuGet repository signature only | N/A | N/A | Apache-2.0 | 2026-05-14; NuGet advisory index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14` | APPROVED |
| `Konscious.Security.Cryptography.Argon2` | Out of Sprint 4 scope; Sprint 5 login KDF decision | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | Sprint 5 / AC-051 | DEFERRED_TO_SPRINT_5 |

Sprint 4 invariant: `Dapper` is selected for Sprint 4 storage implementation, but any `Dapper` `PackageVersion`, `PackageReference`, code reference or evidence claim remains a Sprint 4 Definition of Done violation until this row contains complete version/source/package URL/hash/license/vulnerability-scan/approver evidence. `Konscious.Security.Cryptography.Argon2` remains absent unless a separate human-approved scope pull amends the Sprint 4 plan.

## Sprint 5 Approved Pin Representation

Status: APPROVED - Sprint 5 `System.Management` package scope and evidence were human-approved for Phase B Sprint 5 closure on 2026-05-15 by `n4hkro (architecture/security/release approver)`.

Sprint 5 may add `System.Management` only for the `net10.0-windows` hardware-identity adapter slice required by ADR-0039's five-source fingerprint model. This approval does not authorize other package drift, Argon2, non-Windows targets, or plugin-visible package exposure.

| Package / Asset | Requirement | Version / Asset Identifier | Source URL / Commit / Tag | Package / Asset URL | SHA-256 | NuGet SHA-512 / Package Hash | NuGet Repository Signature | NuGet Author Signature / Risk | Native Upstream Digest | Native Tag / Release Provenance | License | Vulnerability Scan Date | Approver | Decision Link | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `System.Management` | Required for Sprint 5 WMI-backed hardware identity adapter slice (`net10.0-windows` only) | `10.0.8` | `https://github.com/dotnet/dotnet` commit `94ea82652cdd4e0f8046b5bd5becbd11461482ca` | `https://api.nuget.org/v3-flatcontainer/system.management/10.0.8/system.management.10.0.8.nupkg` | `ab469a06e2ff39b949c3bbeafb2c635a2509b93e478baadfa9e6ec81c1f16fd1` | SHA512 `8GgwcQgAv1Xg0z4vkJ759AJOFbZpK17cX9+21SOHo+KXS+VUhwSBeFSzjDJyyaEiJwgQ0D5sGnfpEs2hjyZrmA==` | verified; signer: NuGet.org Repository by Microsoft | verified; signer: Microsoft Corporation | N/A | N/A | MIT | 2026-05-15; NuGet advisory index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint5-decisions-2026-05-15` | APPROVED |

The official NuGet flat-container index was checked on 2026-05-15. `System.Management` `10.0.8` was the latest stable package; later `11.0.0-*` entries were previews and are not approved for Sprint 5. `dotnet nuget verify --all` reported an author signature by Microsoft Corporation and a repository signature by NuGet.org Repository by Microsoft for the downloaded `10.0.8` package.

### Sprint 5 Test-Readable Pin Representation

The fenced JSON block below is the test-readable approved representation for Sprint 5 package pin approval. The Markdown table above remains the human-readable review surface.

```json
{
  "schema": "dobby.sprint5.toolchainPins.v1",
  "status": "APPROVED",
  "approvalRule": "System.Management is approved only for the Sprint 5 net10.0-windows hardware-identity adapter slice; no other package drift is approved.",
  "rows": [
    {
      "packageId": "System.Management",
      "requirement": "Required for Sprint 5 WMI-backed hardware identity adapter slice (net10.0-windows only)",
      "versionOrAsset": "10.0.8",
      "sourceUrlCommitTag": "https://github.com/dotnet/dotnet commit 94ea82652cdd4e0f8046b5bd5becbd11461482ca",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/system.management/10.0.8/system.management.10.0.8.nupkg",
      "sha256": "ab469a06e2ff39b949c3bbeafb2c635a2509b93e478baadfa9e6ec81c1f16fd1",
      "nugetSha512OrPackageHash": "SHA512 8GgwcQgAv1Xg0z4vkJ759AJOFbZpK17cX9+21SOHo+KXS+VUhwSBeFSzjDJyyaEiJwgQ0D5sGnfpEs2hjyZrmA==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "verified; signer: Microsoft Corporation",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "MIT",
      "vulnerabilityScanDate": "2026-05-15; NuGet advisory index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint5-decisions-2026-05-15",
      "targetFrameworkScope": "net10.0-windows only",
      "status": "APPROVED"
    }
  ]
}
```

## Sprint 7 Package Approval - HD-S7-001 / Q1

Status: APPROVED - Sprint 7 exact UI/MVVM/localization/snapshot/test package scope and evidence were human-approved on 2026-05-26 by `n4hkro (architecture/security/release approver)`.

This is the primary approval evidence record for `HD-S7-001` / `Q1`. `decision-log.md` is only the decision anchor; the package evidence lives here. This approval is limited to the exact package IDs and versions below for Sprint 7 runtime UI shell/theme/localization work and Sprint 7 test-only snapshot/unit/integration verification. It does not approve any new package ID, package source, native asset, upgrade, downgrade, package-reference target expansion, or Sprint 7 use of another existing `Directory.Packages.props` pin not listed here, including `xunit.runner.visualstudio`.

The official NuGet flat-container package URLs, package metadata, downloaded package SHA-256, NuGet package content hash, NuGet vulnerability index, and `dotnet nuget verify --all` output were checked on 2026-05-26. This approval makes no latest-stable upgrade claim; later stable or prerelease versions, if present, are not approved for Sprint 7 by implication.

| Package / Asset | Requirement | Version / Asset Identifier | Source URL / Commit / Tag | Package / Asset URL | SHA-256 | NuGet SHA-512 / Package Hash | NuGet Repository Signature | NuGet Author Signature / Risk | Native Upstream Digest | Native Tag / Release Provenance | License | Vulnerability Scan Date | Approver | Decision Link | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `WPF-UI` | Sprint 7 runtime UI shell/theme package for authenticated WPF shell surfaces | `4.3.0` | `https://github.com/lepoco/wpfui commit 153baadeaf475875a81a4605881ca5c2dbe53972` | `https://api.nuget.org/v3-flatcontainer/wpf-ui/4.3.0/wpf-ui.4.3.0.nupkg` | `522f9a300fb3f3a885e7c0acbdd9b991d638f6011d2a67f3e9ccc6a3c3daad2b` | SHA512 `SIHhc8lIrhEQ/1cLFZN6Dtf8cqsLm4myBULj9obnacd9TyDs2vizhKYzRzTlcPEcnzb3L4EstvPN3M6kcXbJCA==` | verified; signer: NuGet.org Repository by Microsoft | absent; risk accepted via NuGet repository signature only | N/A | N/A | MIT | 2026-05-26; NuGet vulnerability index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26` | APPROVED |
| `WPF-UI.DependencyInjection` | Sprint 7 runtime WPF-UI Generic Host/DI integration for shell composition | `4.3.0` | `https://github.com/lepoco/wpfui commit 153baadeaf475875a81a4605881ca5c2dbe53972` | `https://api.nuget.org/v3-flatcontainer/wpf-ui.dependencyinjection/4.3.0/wpf-ui.dependencyinjection.4.3.0.nupkg` | `cbbe5aea16ff7c849d0dab008a907e9149b53b134ab663d0bb0beadcff47ad92` | SHA512 `ijq5tuBuQNrafsGA4FlZ2eKHIwMSew3zQ6ENIIm3uWi0zBkYrW/gF0sGaaxIB5X7Ewb3vzA1jOodM8jlyJw8sQ==` | verified; signer: NuGet.org Repository by Microsoft | absent; risk accepted via NuGet repository signature only | N/A | N/A | MIT | 2026-05-26; NuGet vulnerability index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26` | APPROVED |
| `CommunityToolkit.Mvvm` | Sprint 7 runtime MVVM/UI ergonomics for shell/theme/localization view models | `8.4.2` | `https://github.com/CommunityToolkit/dotnet commit 35dfe7b12da0e64384bfaec7ebc0883196a89f9f` | `https://api.nuget.org/v3-flatcontainer/communitytoolkit.mvvm/8.4.2/communitytoolkit.mvvm.8.4.2.nupkg` | `8cb4b5be8e95f9f1ec26cf341d84fbee8244e88102ebc7c88242e960e0e35805` | SHA512 `WadCzGEc2U+3e20avRLng4qNtt4zoOGWrdUISqJWrHe3/FSnrYjuM5Sb4yQb09LhkBXrrI4Zt3dLKgRMbItsrg==` | verified; signer: NuGet.org Repository by Microsoft | verified; signer: Community Toolkit (.NET Foundation) | N/A | N/A | MIT | 2026-05-26; NuGet vulnerability index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26` | APPROVED |
| `Microsoft.Extensions.Localization` | Sprint 7 runtime localization abstractions for az-AZ/en-US/tr-TR host UI resources | `10.0.7` | `https://github.com/dotnet/dotnet commit b16286c2284fecf303dbc12a0bb152476d662e44` | `https://api.nuget.org/v3-flatcontainer/microsoft.extensions.localization/10.0.7/microsoft.extensions.localization.10.0.7.nupkg` | `9ec894304bcbc28f1a9fec7a7c003334ca30a33d82499c7d458e78c98ac7554a` | SHA512 `xUtyqnk0KSTrX1VZ7z++UlNcVqUQAlKgg2/NqTxmQbW9A26Ia3Zvkd1pIX71koue5F8FtV5pCZqsOlJOycvxMA==` | verified; signer: NuGet.org Repository by Microsoft | verified; signer: Microsoft Corporation | N/A | N/A | MIT | 2026-05-26; NuGet vulnerability index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26` | APPROVED |
| `Verify.Xunit` | Sprint 7 test-only snapshot verification for shell/theme/settings rendered evidence | `31.12.5` | `https://github.com/VerifyTests/Verify.git commit 43cb4f461e224a5e57f2e410f0504c4305fcfc1b` | `https://api.nuget.org/v3-flatcontainer/verify.xunit/31.12.5/verify.xunit.31.12.5.nupkg` | `08b1e892eb5cf09c914752d4d16c66838a5a8cbe5217680256feb9b355b8b3c6` | SHA512 `i1d2bPonW/3ZzzEZYTWgv8mjPyRWpKaPsIxxp/kYK7Nq8ZeSEmkLA5BkGwInDlybHkxsviFu+s8iF20y+yUcZw==` | verified; signer: NuGet.org Repository by Microsoft | absent; risk accepted via NuGet repository signature only | N/A | N/A | MIT | 2026-05-26; NuGet vulnerability index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26` | APPROVED |
| `xunit` | Sprint 7 test-only unit/integration test framework | `2.9.3` | `https://github.com/xunit/xunit commit 9712244020d385955d33136b3fe3e87de43539cd` | `https://api.nuget.org/v3-flatcontainer/xunit/2.9.3/xunit.2.9.3.nupkg` | `04fae949b8e5201ecfa07fa8702baca8c0eb3198114194b3793789cc72bf23d7` | SHA512 `TlXQBinK35LpOPKHAqbLY4xlEen9TBafjs0V5KnA4wZsoQLQJiirCR4CbIXvOH8NzkW4YeJKP5P/Bnrodm0h9Q==` | verified; signer: NuGet.org Repository by Microsoft | verified; signer: xUnit.net (.NET Foundation) | N/A | N/A | Apache-2.0 | 2026-05-26; NuGet vulnerability index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26` | APPROVED |
| `Shouldly` | Sprint 7 test-only assertion library | `4.3.0` | `https://github.com/shouldly/shouldly.git commit cb48f40b4d6087ce9fb1983041fce8a725436cb1` | `https://api.nuget.org/v3-flatcontainer/shouldly/4.3.0/shouldly.4.3.0.nupkg` | `ee9622d9a1aa3c81f3ca125e9575912d508bcc1c53890af40c24d8df6e6ae8ef` | SHA512 `sDetrWXrl6YXZ4HeLsdBoNk3uIa7K+V4uvIJ+cqdRa5DrFxeTED7VkjoxCuU1kJWpUuBDZz2QXFzSxBtVXLwRQ==` | verified; signer: NuGet.org Repository by Microsoft | absent; risk accepted via NuGet repository signature only | N/A | N/A | BSD-3-Clause | 2026-05-26; NuGet vulnerability index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26` | APPROVED |
| `NSubstitute` | Sprint 7 test-only mocking/substitution library | `5.3.0` | `https://github.com/nsubstitute/NSubstitute commit 45e00e386fc76e038ff6a5778a301f7e62bfc81c` | `https://api.nuget.org/v3-flatcontainer/nsubstitute/5.3.0/nsubstitute.5.3.0.nupkg` | `7dae879fd426a626b695a6d63acd57a762e72413877eb588c31bea29145c72cd` | SHA512 `lJ47Cps5Qzr86N99lcwd+OUvQma7+fBgr8+Mn+aOC0WrlqMNkdivaYD9IvnZ5Mqo6Ky3LS7ZI+tUq1/s9ERd0Q==` | verified; signer: NuGet.org Repository by Microsoft | absent; risk accepted via NuGet repository signature only | N/A | N/A | BSD-3-Clause | 2026-05-26; NuGet vulnerability index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26` | APPROVED |
| `Microsoft.NET.Test.Sdk` | Sprint 7 test-only VSTest SDK runner integration | `18.5.1` | `https://github.com/microsoft/vstest commit 895d0f16b1a65c8a1d060ff4083d76b7518c581f` | `https://api.nuget.org/v3-flatcontainer/microsoft.net.test.sdk/18.5.1/microsoft.net.test.sdk.18.5.1.nupkg` | `5faf7040129de46bd77ceec168f585732af07e2fe464555d1ff6b1a2b22b6b96` | SHA512 `SfqVaLiIqAbRWuPg5BP4QFwBIirQj/YIL8Dhxl6zntBKbXp0cQykoV480SmwG+yRMiWptxEI6NbHQuGSZ8b97w==` | verified; signer: NuGet.org Repository by Microsoft | verified; signer: Microsoft Corporation | N/A | N/A | MIT | 2026-05-26; NuGet vulnerability index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26` | APPROVED |
| `Microsoft.Extensions.TimeProvider.Testing` | Sprint 7 test-only deterministic time provider support | `10.5.0` | `https://github.com/dotnet/extensions commit 958df529b628f62daa615dba271f42e2225d7a97` | `https://api.nuget.org/v3-flatcontainer/microsoft.extensions.timeprovider.testing/10.5.0/microsoft.extensions.timeprovider.testing.10.5.0.nupkg` | `89a5d5b2b985090ead0e15d31bd70b71129ab2419ff59f9dae2ff3c4c3ca1dfc` | SHA512 `BW6DXn0oZWfoEN9cb+3PcM2IBHqcTnsO+UqEhW+uzdEuZSY3C9i9ITKoLiYbS8JwWm6daTpa4kRikUwr3KD+qQ==` | verified; signer: NuGet.org Repository by Microsoft | verified; signer: Microsoft Corporation | N/A | N/A | MIT | 2026-05-26; NuGet vulnerability index checked, 0 hits | `n4hkro (architecture/security/release approver)` | `artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26` | APPROVED |

### Sprint 7 Test-Readable Pin Representation

The fenced JSON block below is the test-readable approved representation for Sprint 7 package pin approval. The Markdown table above remains the human-readable review surface.

```json
{
  "schema": "dobby.sprint7.toolchainPins.v1",
  "status": "APPROVED",
  "approvalRule": "Only the exact listed Sprint 7 runtime UI shell/theme/localization packages and test-only snapshot/unit/integration verification packages are approved; any new package/source/native asset, version change, target expansion, or Sprint 7 use of another existing pin requires a separate human decision.",
  "rows": [
    {
      "packageId": "WPF-UI",
      "requirement": "Sprint 7 runtime UI shell/theme package for authenticated WPF shell surfaces",
      "versionOrAsset": "4.3.0",
      "sourceUrlCommitTag": "https://github.com/lepoco/wpfui commit 153baadeaf475875a81a4605881ca5c2dbe53972",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/wpf-ui/4.3.0/wpf-ui.4.3.0.nupkg",
      "sha256": "sha256:522f9a300fb3f3a885e7c0acbdd9b991d638f6011d2a67f3e9ccc6a3c3daad2b",
      "nugetSha512OrPackageHash": "SHA512 SIHhc8lIrhEQ/1cLFZN6Dtf8cqsLm4myBULj9obnacd9TyDs2vizhKYzRzTlcPEcnzb3L4EstvPN3M6kcXbJCA==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "absent; risk accepted via NuGet repository signature only",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "MIT",
      "vulnerabilityScanDate": "2026-05-26; NuGet vulnerability index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26",
      "status": "APPROVED"
    },
    {
      "packageId": "WPF-UI.DependencyInjection",
      "requirement": "Sprint 7 runtime WPF-UI Generic Host/DI integration for shell composition",
      "versionOrAsset": "4.3.0",
      "sourceUrlCommitTag": "https://github.com/lepoco/wpfui commit 153baadeaf475875a81a4605881ca5c2dbe53972",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/wpf-ui.dependencyinjection/4.3.0/wpf-ui.dependencyinjection.4.3.0.nupkg",
      "sha256": "sha256:cbbe5aea16ff7c849d0dab008a907e9149b53b134ab663d0bb0beadcff47ad92",
      "nugetSha512OrPackageHash": "SHA512 ijq5tuBuQNrafsGA4FlZ2eKHIwMSew3zQ6ENIIm3uWi0zBkYrW/gF0sGaaxIB5X7Ewb3vzA1jOodM8jlyJw8sQ==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "absent; risk accepted via NuGet repository signature only",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "MIT",
      "vulnerabilityScanDate": "2026-05-26; NuGet vulnerability index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26",
      "status": "APPROVED"
    },
    {
      "packageId": "CommunityToolkit.Mvvm",
      "requirement": "Sprint 7 runtime MVVM/UI ergonomics for shell/theme/localization view models",
      "versionOrAsset": "8.4.2",
      "sourceUrlCommitTag": "https://github.com/CommunityToolkit/dotnet commit 35dfe7b12da0e64384bfaec7ebc0883196a89f9f",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/communitytoolkit.mvvm/8.4.2/communitytoolkit.mvvm.8.4.2.nupkg",
      "sha256": "sha256:8cb4b5be8e95f9f1ec26cf341d84fbee8244e88102ebc7c88242e960e0e35805",
      "nugetSha512OrPackageHash": "SHA512 WadCzGEc2U+3e20avRLng4qNtt4zoOGWrdUISqJWrHe3/FSnrYjuM5Sb4yQb09LhkBXrrI4Zt3dLKgRMbItsrg==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "verified; signer: Community Toolkit (.NET Foundation)",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "MIT",
      "vulnerabilityScanDate": "2026-05-26; NuGet vulnerability index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26",
      "status": "APPROVED"
    },
    {
      "packageId": "Microsoft.Extensions.Localization",
      "requirement": "Sprint 7 runtime localization abstractions for az-AZ/en-US/tr-TR host UI resources",
      "versionOrAsset": "10.0.7",
      "sourceUrlCommitTag": "https://github.com/dotnet/dotnet commit b16286c2284fecf303dbc12a0bb152476d662e44",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/microsoft.extensions.localization/10.0.7/microsoft.extensions.localization.10.0.7.nupkg",
      "sha256": "sha256:9ec894304bcbc28f1a9fec7a7c003334ca30a33d82499c7d458e78c98ac7554a",
      "nugetSha512OrPackageHash": "SHA512 xUtyqnk0KSTrX1VZ7z++UlNcVqUQAlKgg2/NqTxmQbW9A26Ia3Zvkd1pIX71koue5F8FtV5pCZqsOlJOycvxMA==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "verified; signer: Microsoft Corporation",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "MIT",
      "vulnerabilityScanDate": "2026-05-26; NuGet vulnerability index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26",
      "status": "APPROVED"
    },
    {
      "packageId": "Verify.Xunit",
      "requirement": "Sprint 7 test-only snapshot verification for shell/theme/settings rendered evidence",
      "versionOrAsset": "31.12.5",
      "sourceUrlCommitTag": "https://github.com/VerifyTests/Verify.git commit 43cb4f461e224a5e57f2e410f0504c4305fcfc1b",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/verify.xunit/31.12.5/verify.xunit.31.12.5.nupkg",
      "sha256": "sha256:08b1e892eb5cf09c914752d4d16c66838a5a8cbe5217680256feb9b355b8b3c6",
      "nugetSha512OrPackageHash": "SHA512 i1d2bPonW/3ZzzEZYTWgv8mjPyRWpKaPsIxxp/kYK7Nq8ZeSEmkLA5BkGwInDlybHkxsviFu+s8iF20y+yUcZw==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "absent; risk accepted via NuGet repository signature only",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "MIT",
      "vulnerabilityScanDate": "2026-05-26; NuGet vulnerability index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26",
      "status": "APPROVED"
    },
    {
      "packageId": "xunit",
      "requirement": "Sprint 7 test-only unit/integration test framework",
      "versionOrAsset": "2.9.3",
      "sourceUrlCommitTag": "https://github.com/xunit/xunit commit 9712244020d385955d33136b3fe3e87de43539cd",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/xunit/2.9.3/xunit.2.9.3.nupkg",
      "sha256": "sha256:04fae949b8e5201ecfa07fa8702baca8c0eb3198114194b3793789cc72bf23d7",
      "nugetSha512OrPackageHash": "SHA512 TlXQBinK35LpOPKHAqbLY4xlEen9TBafjs0V5KnA4wZsoQLQJiirCR4CbIXvOH8NzkW4YeJKP5P/Bnrodm0h9Q==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "verified; signer: xUnit.net (.NET Foundation)",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "Apache-2.0",
      "vulnerabilityScanDate": "2026-05-26; NuGet vulnerability index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26",
      "status": "APPROVED"
    },
    {
      "packageId": "Shouldly",
      "requirement": "Sprint 7 test-only assertion library",
      "versionOrAsset": "4.3.0",
      "sourceUrlCommitTag": "https://github.com/shouldly/shouldly.git commit cb48f40b4d6087ce9fb1983041fce8a725436cb1",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/shouldly/4.3.0/shouldly.4.3.0.nupkg",
      "sha256": "sha256:ee9622d9a1aa3c81f3ca125e9575912d508bcc1c53890af40c24d8df6e6ae8ef",
      "nugetSha512OrPackageHash": "SHA512 sDetrWXrl6YXZ4HeLsdBoNk3uIa7K+V4uvIJ+cqdRa5DrFxeTED7VkjoxCuU1kJWpUuBDZz2QXFzSxBtVXLwRQ==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "absent; risk accepted via NuGet repository signature only",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "BSD-3-Clause",
      "vulnerabilityScanDate": "2026-05-26; NuGet vulnerability index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26",
      "status": "APPROVED"
    },
    {
      "packageId": "NSubstitute",
      "requirement": "Sprint 7 test-only mocking/substitution library",
      "versionOrAsset": "5.3.0",
      "sourceUrlCommitTag": "https://github.com/nsubstitute/NSubstitute commit 45e00e386fc76e038ff6a5778a301f7e62bfc81c",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/nsubstitute/5.3.0/nsubstitute.5.3.0.nupkg",
      "sha256": "sha256:7dae879fd426a626b695a6d63acd57a762e72413877eb588c31bea29145c72cd",
      "nugetSha512OrPackageHash": "SHA512 lJ47Cps5Qzr86N99lcwd+OUvQma7+fBgr8+Mn+aOC0WrlqMNkdivaYD9IvnZ5Mqo6Ky3LS7ZI+tUq1/s9ERd0Q==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "absent; risk accepted via NuGet repository signature only",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "BSD-3-Clause",
      "vulnerabilityScanDate": "2026-05-26; NuGet vulnerability index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26",
      "status": "APPROVED"
    },
    {
      "packageId": "Microsoft.NET.Test.Sdk",
      "requirement": "Sprint 7 test-only VSTest SDK runner integration",
      "versionOrAsset": "18.5.1",
      "sourceUrlCommitTag": "https://github.com/microsoft/vstest commit 895d0f16b1a65c8a1d060ff4083d76b7518c581f",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/microsoft.net.test.sdk/18.5.1/microsoft.net.test.sdk.18.5.1.nupkg",
      "sha256": "sha256:5faf7040129de46bd77ceec168f585732af07e2fe464555d1ff6b1a2b22b6b96",
      "nugetSha512OrPackageHash": "SHA512 SfqVaLiIqAbRWuPg5BP4QFwBIirQj/YIL8Dhxl6zntBKbXp0cQykoV480SmwG+yRMiWptxEI6NbHQuGSZ8b97w==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "verified; signer: Microsoft Corporation",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "MIT",
      "vulnerabilityScanDate": "2026-05-26; NuGet vulnerability index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26",
      "status": "APPROVED"
    },
    {
      "packageId": "Microsoft.Extensions.TimeProvider.Testing",
      "requirement": "Sprint 7 test-only deterministic time provider support",
      "versionOrAsset": "10.5.0",
      "sourceUrlCommitTag": "https://github.com/dotnet/extensions commit 958df529b628f62daa615dba271f42e2225d7a97",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/microsoft.extensions.timeprovider.testing/10.5.0/microsoft.extensions.timeprovider.testing.10.5.0.nupkg",
      "sha256": "sha256:89a5d5b2b985090ead0e15d31bd70b71129ab2419ff59f9dae2ff3c4c3ca1dfc",
      "nugetSha512OrPackageHash": "SHA512 BW6DXn0oZWfoEN9cb+3PcM2IBHqcTnsO+UqEhW+uzdEuZSY3C9i9ITKoLiYbS8JwWm6daTpa4kRikUwr3KD+qQ==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "verified; signer: Microsoft Corporation",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "MIT",
      "vulnerabilityScanDate": "2026-05-26; NuGet vulnerability index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-b-sprint7-package-approval-2026-05-26",
      "status": "APPROVED"
    }
  ]
}
```

## Approval-Day Verification Protocol

All selected NuGet package rows must be verified on the approval date:

1. Re-check latest stable status from the official NuGet package source.
2. Download the `.nupkg` from the official NuGet flat-container URL recorded in the table.
3. Compute local `Get-FileHash -Algorithm SHA256 <package>.nupkg`; record this value in the `SHA-256` field.
4. Capture NuGet package hash metadata when available, including SHA-512/package hash, as separate evidence. This is an independent package-content hash, not a source for deriving SHA-256.
5. Run `dotnet nuget verify --all <package>.nupkg` or the approved equivalent package-signature verification command.
6. Record NuGet repository signature and NuGet author signature separately. If author signature is absent, record an explicit risk-accepted note such as `absent; risk accepted via NuGet repository signature only`.
7. Record package URL, version, repository/source URL, repository commit when present in package metadata, license, vulnerability scan date, approver, and decision link in the same approval patch.

All selected native asset rows must be verified on the approval date:

1. Record the canonical upstream release URL/tag or self-built source tag/commit.
2. Record the exact asset URL, local SHA-256 of the downloaded or produced asset, license, vulnerability scan date, approver, and decision link.
3. For upstream release assets, record upstream-published asset digest separately from the local SHA-256.
4. Record git tag signature/provenance and release commit separately when available. Tag signature evidence proves source provenance; it does not replace asset integrity evidence.
5. For self-built assets, also record reproducible Windows native build evidence, including toolchain pin, build script/path, build environment description, and produced artifact SHA-256.

For an upstream `SQLite3MultipleCiphers` release-tag path, the native tag/release provenance evidence must include:

- tag reference API endpoint, for example `/repos/utelle/SQLite3MultipleCiphers/git/refs/tags/<tag>`;
- tag object API endpoint, for example `/repos/utelle/SQLite3MultipleCiphers/git/tags/<tag-object-sha>`;
- tag verification result, including `verified: true` and `reason: valid`;
- tag object SHA;
- release commit SHA targeted by the signed tag;
- verification timestamp in ISO-8601 format;
- maintainer GPG key fingerprint when it is available from local `git tag -v` or equivalent GPG verification.

The maintainer GPG key fingerprint is capture-if-available evidence, not a blocker when the GitHub tag verification result is valid and the asset digest/hash evidence is complete.

Selected native asset vulnerability review must also be recorded on the approval date:

1. SQLite version carried by the selected asset and NVD/OSV check result for that SQLite version.
2. `SQLite3MultipleCiphers` GitHub Security advisories and relevant Issues check result for the selected tag/release.
3. Release-notes review for cipher/security-relevant changes, including hardcoded key/nonce/IV or similar cryptographic anti-pattern concerns.
4. Bundled native component inventory from the selected asset, including any separately versioned bundled component that needs an additional vulnerability check.

Any mismatch between recorded evidence, local hashes, NuGet metadata, signature/digest verification, or approved source URL blocks approval.

## Test-Readable Pin Representation

The fenced JSON block below is the test-readable approved representation for Sprint 4 package pin approval. The Markdown table above remains the human-readable review surface.

```json
{
  "schema": "dobby.sprint4.toolchainPins.v2",
  "status": "APPROVED",
  "approvalRule": "Selected Sprint 4 rows are human-approved for Phase A closure; Argon2 remains deferred to Sprint 5.",
  "rows": [
    {
      "packageId": "Microsoft.Data.Sqlite.Core",
      "requirement": "Required for Sprint 4 storage provider",
      "versionOrAsset": "10.0.8",
      "sourceUrlCommitTag": "https://github.com/dotnet/dotnet commit 94ea82652cdd4e0f8046b5bd5becbd11461482ca",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/microsoft.data.sqlite.core/10.0.8/microsoft.data.sqlite.core.10.0.8.nupkg",
      "sha256": "dc31e393411cfabf62134176a649c5c60993bab4472ef863c0e44a48eb335656",
      "nugetSha512OrPackageHash": "SHA512 3gV3P+2kxD/pAkd9Nc07SKYzuBNMCrkQxQMFvgG96vFWOHN0H8j+ZfYPhFGpxIEkx2XZTpqecgP8XEpLWHg9Cw==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "verified; signer: Microsoft Corporation",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "MIT",
      "vulnerabilityScanDate": "2026-05-14; NuGet advisory index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14",
      "status": "APPROVED"
    },
    {
      "packageId": "SQLitePCLRaw.core",
      "requirement": "Required for Sprint 4 provider wiring",
      "versionOrAsset": "3.0.3",
      "sourceUrlCommitTag": "https://github.com/ericsink/SQLitePCL.raw commit f3f967adafad853375597a6387cacb7573c5b343",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/sqlitepclraw.core/3.0.3/sqlitepclraw.core.3.0.3.nupkg",
      "sha256": "30bb8decacdf856c955f78b8a62e79290a7d27c251f37b020af79e52eb032aac",
      "nugetSha512OrPackageHash": "SHA512 6XZEy8awVSZJgIbWfuQU6w1c4wpFHHKQaLEzPwF4YSKiz/k+0p6HyyW5Ne/B6ZSPPSIHxuKtVnMB0uhAUpYn6g==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "absent; risk accepted via NuGet repository signature only",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "Apache-2.0",
      "vulnerabilityScanDate": "2026-05-14; NuGet advisory index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14",
      "status": "APPROVED"
    },
    {
      "packageId": "Approved native encrypted SQLite provider asset/package",
      "requirement": "Required for Sprint 4 DB-at-rest encryption; preferred path is canonical upstream SQLite3MultipleCiphers release artifact when evidence is complete, otherwise self-built reproducible Windows native asset",
      "versionOrAsset": "SQLite3MultipleCiphers v2.3.4 / sqlite3mc-2.3.4-sqlite-3.53.1-win64.zip",
      "sourceUrlCommitTag": "https://github.com/utelle/SQLite3MultipleCiphers/releases/tag/v2.3.4; tag object 6cc1882e85731a3e4d12c746a941d5ac434fe1bc; release commit 04a7a56392d77fc46007f0023d3eb4b5b701474b",
      "packageOrAssetUrl": "https://github.com/utelle/SQLite3MultipleCiphers/releases/download/v2.3.4/sqlite3mc-2.3.4-sqlite-3.53.1-win64.zip",
      "sha256": "f685ab7aed02f79985c6876b7dfa463bb81ebc1f6acd3ce536382cc276467b15",
      "nugetSha512OrPackageHash": "N/A",
      "nugetRepositorySignature": "N/A",
      "nugetAuthorSignatureOrRisk": "N/A",
      "nativeUpstreamDigest": "sha256:f685ab7aed02f79985c6876b7dfa463bb81ebc1f6acd3ce536382cc276467b15",
      "nativeTagReleaseProvenance": "GitHub API tag verification verified=true, reason=valid, verified_at=2026-05-06T08:29:44Z; endpoints /repos/utelle/SQLite3MultipleCiphers/git/refs/tags/v2.3.4 and /repos/utelle/SQLite3MultipleCiphers/git/tags/6cc1882e85731a3e4d12c746a941d5ac434fe1bc; GPG fingerprint capture unavailable",
      "license": "MIT",
      "vulnerabilityScanDate": "2026-05-14; OSV/NVD SQLite 3.53.1 0 hits; GitHub advisories/issues 0 hits; release notes reviewed",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14",
      "status": "APPROVED"
    },
    {
      "packageId": "SQLitePCLRaw.provider.dynamic_cdecl",
      "requirement": "Required for explicit provider wiring",
      "versionOrAsset": "3.0.3",
      "sourceUrlCommitTag": "https://github.com/ericsink/SQLitePCL.raw commit f3f967adafad853375597a6387cacb7573c5b343",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/sqlitepclraw.provider.dynamic_cdecl/3.0.3/sqlitepclraw.provider.dynamic_cdecl.3.0.3.nupkg",
      "sha256": "ecc9f8cc6f100c7da4155a5d7b73f4fc4400fe78fc4dfd5589741ca75f71fcf8",
      "nugetSha512OrPackageHash": "SHA512 AJFBFJLq0r7UEojrZ+IFO74lQNZ/0HiWPyT3qe9mtc1q1tLwmj88/32TU4lIXTVd9UV9xcTSLUNjGZC4t1iK/A==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "absent; risk accepted via NuGet repository signature only",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "Apache-2.0",
      "vulnerabilityScanDate": "2026-05-14; NuGet advisory index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14",
      "status": "APPROVED"
    },
    {
      "packageId": "Dapper",
      "requirement": "Selected for Sprint 4 storage implementation; package reference blocked until this row is complete",
      "versionOrAsset": "2.1.72",
      "sourceUrlCommitTag": "https://github.com/DapperLib/Dapper commit 288730e69b05c32cac898d9b55ebea219ea8a2d1",
      "packageOrAssetUrl": "https://api.nuget.org/v3-flatcontainer/dapper/2.1.72/dapper.2.1.72.nupkg",
      "sha256": "f422b2cfbdbf569e7f188a3127397b4405d38f832f2938058bfedf1b2851a9fe",
      "nugetSha512OrPackageHash": "SHA512 LKgcsnSLUmi2E1tKOuVFB158aQ8unGNXHpZ5Dj399Ou8wTCMX2o+5WQd9wdQflgNNgUgahOf+NjYnEqF/KOhKQ==",
      "nugetRepositorySignature": "verified; signer: NuGet.org Repository by Microsoft",
      "nugetAuthorSignatureOrRisk": "absent; risk accepted via NuGet repository signature only",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "Apache-2.0",
      "vulnerabilityScanDate": "2026-05-14; NuGet advisory index checked, 0 hits",
      "approver": "n4hkro (architecture/security/release approver)",
      "decisionLink": "artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-2026-05-14",
      "status": "APPROVED"
    },
    {
      "packageId": "Konscious.Security.Cryptography.Argon2",
      "requirement": "Out of Sprint 4 scope; Sprint 5 login KDF decision",
      "versionOrAsset": "N/A",
      "sourceUrlCommitTag": "N/A",
      "packageOrAssetUrl": "N/A",
      "sha256": "N/A",
      "nugetSha512OrPackageHash": "N/A",
      "nugetRepositorySignature": "N/A",
      "nugetAuthorSignatureOrRisk": "N/A",
      "nativeUpstreamDigest": "N/A",
      "nativeTagReleaseProvenance": "N/A",
      "license": "N/A",
      "vulnerabilityScanDate": "N/A",
      "approver": "N/A",
      "decisionLink": "Sprint 5 / AC-051",
      "status": "DEFERRED_TO_SPRINT_5"
    }
  ]
}
```

Automated consistency invariant: Sprint 4 package governance work must add a test-readable check that parses this fenced JSON block and the Markdown table above, normalizes Markdown code formatting, and verifies field-by-field equality. A mismatch between the human-readable table and JSON representation must fail CI before package-bearing implementation is accepted.

Freeze window: after Phase A closure and before the first Sprint 4 implementation commit/task, this file must not change. Any required change during that window reopens Phase A and requires a separate approval pass before implementation continues. The automated consistency test is the first Sprint 4 package-governance task after closure; until then, the freeze window protects this ledger from manual drift.

Decision link rule: Phase A closure requires a decision-log anchor under `artifacts/release-evidence/skeleton-local/decision-log.md#phase-a-<date>`. A signed git tag is optional and conditional on this workspace being an initialized git repository with signing configured; it is not a Phase A prerequisite in this workspace. If `.git` and signing configuration materialize during Sprint 4, a signed tag may be added later as additive audit evidence without reopening Phase A, provided the ledger content and approved evidence values do not change.

## Resolved Stop Conditions

- `TOOLCHAIN-PIN-APPROVAL-MISSING`: resolved for selected Sprint 4 storage package/native asset rows by the Phase A 2026-05-14 evidence-layer approval patch.
- `TOOLCHAIN-PIN-REPRESENTATION-MISSING`: resolved for Phase A by the human-readable Markdown table plus matching approved fenced JSON representation above. The automated consistency test remains the first Sprint 4 package-governance implementation task after closure, protected by the freeze window.
- `HD-S7-001` / `Q1`: resolved for the exact Sprint 7 UI/MVVM/localization/snapshot/test package set by the Sprint 7 2026-05-26 approval evidence record above. Any package/source/native asset/version/scope drift remains a separate human-decision stop condition.
- Architecture blockers are tracked in architecture-baseline.md

## Sprint 4 Package Application Evidence

Date: 2026-05-15 (Asia/Baku)

Status: IMPLEMENTED AND VERIFIED for the approved Sprint 4 package/native-asset set.

Applied pins and references:

- `Directory.Packages.props` centrally pins `Microsoft.Data.Sqlite.Core` `10.0.8`, `SQLitePCLRaw.core` `3.0.3`, `SQLitePCLRaw.provider.dynamic_cdecl` `3.0.3`, and `Dapper` `2.1.72`.
- `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj` references the approved Sprint 4 storage packages and remains the only project that loads the native SQLite provider.
- `Konscious.Security.Cryptography.Argon2` remains absent and deferred to Sprint 5.
- The approved SQLite3MultipleCiphers archive digest was verified as `sha256:f685ab7aed02f79985c6876b7dfa463bb81ebc1f6acd3ce536382cc276467b15`.
- The extracted runtime asset is `src/Dobby.Infrastructure/native/win-x64/sqlite3mc_x64.dll`; the runtime loader verifies extracted DLL `sha256:3744689bf8427b40b222c1ec046ebc5e4f08c5c9898d56573112006ff2c4def8` before provider registration.

Verification evidence:

- Package governance tests parse and compare the Markdown table and fenced JSON representation in this ledger.
- Package governance tests verify approved Sprint 4 pins/references and reject deferred Argon2 plus rejected SQLitePCLRaw bundle packages.
- `dotnet build .\Dobby.sln -c Debug --no-restore -warnaserror` passed on 2026-05-15 with 0 warnings and 0 errors.
- `dotnet test .\Dobby.sln -c Debug --no-build --logger "console;verbosity=minimal"` passed on 2026-05-15 with 198 total tests, 0 failures.
