# ADR-0037: Plugin Compatibility and Versioning

**Status:** Accepted
**Date:** 2026-04-24

## Context

`02_ARCHITECTURE_MAP.md` Row 29 (`Plugin Compatibility / Versioning`) və Cədvəl 3 `Plugin Compatibility / Versioning` sətri Phase 1 minimum davranışı `Phase 1 minimum decision` blocker kimi göstərir: `host ilə plugin arasındakı compatibility tələbi ödənməyən plugin activation-a buraxılmır`.

`ADR-0036` manifest schema-da `minHostVersion` field-i əlavə edilib (`Identity və integrity field-ləri` bölməsi). Bu ADR həmin field-in semantic-ini və exact comparison behavior-ını rəsmiləşdirir.

## Decision

Phase 1 plugin compatibility və versioning modeli:

### 1. Host version

- Host version SemVer 2.0.0 format-da təqdim olunur (`MAJOR.MINOR.PATCH`).
- Host version `Directory.Build.props` və ya project metadata-sındakı `<Version>` /
  `<AssemblyVersion>` dəyərlərindən build zamanı assembly metadata-sına embed edilir
  və runtime-da `IHostVersionProvider.Current` üzərindən əldə olunur.
- `IHostVersionProvider` `Dobby.Core.Versioning` namespace-indəki primitive-dir; plugin-lər bu surface-ni `Dobby.Contracts` üzərindən görür.

### 2. Plugin version

- Plugin manifest-də `version` field-i SemVer 2.0.0 format-dadır (ADR-0036).
- Pre-release versiyalar (`1.0.0-alpha`, `1.0.0-beta`) Phase 1-də manifest validation failure-dır; yalnız stable build-lər qəbul olunur.

### 3. `minHostVersion` compatibility check

- Plugin manifest `minHostVersion` field-i SemVer 2.0.0 format-dadır.
- Plugin activation zamanı aşağıdakı yoxlama aparılır:

  ```
  if (hostVersion < manifest.minHostVersion) { reject with "host-version-below-min" }
  ```
- SemVer comparison canonical qaydalar ilə: major first, minor second, patch third (ascending müqayisə).
- Uyğunsuzluqda plugin activation-a buraxılmır; user UI-da `Plugin requires host version ≥ X.Y.Z; installed host is A.B.C` mesajı görünür.
- Audit qeydi: `plugin.activation.rejected` event-i səbəb (`host-version-below-min: required=X.Y.Z installed=A.B.C`).

### 4. `maxHostVersion` optional (Phase 1-də yoxdur)

- Phase 1-də `maxHostVersion` field-i manifest-də elan olunmur.
- Forward-compatibility strategy: plugin yeni host versiyalarında çalışır (host backward-compatible olmalıdır); host major bump breaking-change demək olur.
- P2-də `maxHostVersion` açılırsa ayrıca ADR amendment edilir.

### 5. Plugin API version (Contracts SemVer)

- `Dobby.Contracts` assembly öz SemVer-indədir.
- Major bump `Dobby.Contracts` = plugin re-compile tələb edir (binary incompatible).
- Minor bump = new feature, old plugin-lər işləməyə davam edir.
- Patch bump = bug fix, breaking change yoxdur.
- Contracts major bump host major bump ilə sinxron olur; plugin author yeni contracts release-ə uyğun yeni plugin version yaradır və `minHostVersion` yüksəldilir.

### 6. Runtime compatibility matrix

Phase 1 compatibility matrix-i sadədir:

| Host version | Contracts version | Plugin minHostVersion effect |
|---|---|---|
| 1.0.0 | 1.0.0 | plugin minHostVersion ≤ 1.0.0 activate olur |
| 1.1.0 | 1.1.0 | plugin minHostVersion ≤ 1.1.0 activate olur |
| 2.0.0 | 2.0.0 | yalnız 2.0.0+ plugin-lər; 1.x plugin-lər manifest validation failure |

### 7. Rejected alternatives

- **Plugin-side host version compatibility check (runtime-da plugin-in özü yoxlayır)**: rədd edildi — host responsibility-dir; plugin bunu gizli edə bilər.
- **Patch-only compatibility enforcement**: rədd edildi — plugin author-lara predictability vermir; SemVer canonical ascending comparison istifadə edilir.
- **Pre-release plugin-lərə icazə**: Phase 1-də rədd edildi — stable release product foundation-a aiddir; pre-release plugin-lər P2-də dev-mode-da işləyə bilər.

## Consequences

- Plugin author `minHostVersion`-ni düzgün elan etməlidir; köhnə host versiyalarında çalışmayacaq plugin-lər install zamanı və ya activation zamanı rejected olur.
- Host major bump plugin ecosystem-i yenidən qurulmasını tələb edir; bütün plugin-lər yeni manifest ilə yenidən release olunmalıdır.
- Host backward-compatible olmalıdır minor bump-da (SemVer semantics); yeni host versiyalarında köhnə plugin-lər işləyir.
- SemVer comparison primitive-i `Dobby.Core` daxilindəki utility-dir (pre-existing `NuGet.Versioning` package istifadə oluna bilər, ya da lightweight custom parser).

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 29 `Plugin Compatibility / Versioning`, Cədvəl 3 `Plugin Compatibility / Versioning` risk sətri
- `docs/03_TECH_STACK_AND_VERSIONS.md` — `NuGet.Versioning` package selection
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — `minHostVersion` və `version` schema
- `docs/adr/ADR-0009-plugin-dependency-graph.md` — `dependsOn.minVersion` ortaq SemVer semantics
- `docs/adr/ADR-0041-login-first-startup-policy.md` — plugin activation post-login gating
