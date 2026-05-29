# ADR-0009: Plugin Dependency Graph

**Status:** Accepted
**Date:** 2026-04-24

## Context

`ADR-0036` plugin manifest-də `dependsOn = { pluginId, minVersion }` array-ini elan edir. `02_ARCHITECTURE_MAP.md` Row 33 (`Plugin Dependency Resolution`) və Cədvəl 3 `Plugin Dependency Resolution` risk sətri `dependsOn graph-ını, activation sırasını, missing/circular dependency davranışını` Phase 1 blocker kimi göstərir.

Bu ADR həmin boşluğu bağlayır.

## Decision

Phase 1 plugin dependency graph resolution modeli:

### 1. Activation sırası

- Plugin activation order plugin discovery-dən sonra topological sort ilə hesablanır.
- `dependsOn` edge-ləri istiqamətli-dir: plugin A `dependsOn` plugin B = B əvvəl activate olmalıdır.
- Topological sort stable-dir: eyni topological level-də olan plugin-lər `pluginId` ordinal (culture-invariant) ascending order ilə activate olunur.
- Activation `IHostedService` post-login wrapper-i tərəfindən ardıcıl (sequential) icra olunur; Phase 1-də parallel activation yoxdur.

### 2. Missing dependency

- Plugin A `dependsOn` plugin B elan edibsə, amma B host-da quraşdırılmayıbsa və ya disabled-dirsə, plugin A activation-a **buraxılmır** (skipped).
- Missing dependency install-time package validation failure deyil. Valid signed package install oluna bilər; dependency vəziyyəti discovery/activation graph mərhələsində qiymətləndirilir.
- Skipped plugin üçün audit qeydi yaradılır: `plugin.activation.skipped` event-i səbəb (`missing-dependency: <pluginId>`) ilə.
- Skipped plugin UI-da `Plugins` bölməsində görünür amma `Skipped (missing dependency)` status ilə işarələnir.
- Missing dependency aşkar ediləndə activation fatal deyil — digər plugin-lərin activation-ı davam edir.

### 3. Missing dependency version

- Plugin A `dependsOn` plugin B `minVersion = 1.2.0` elan edib, amma installed B `1.1.0`-dır — plugin A skipped, audit səbəb (`dependency-version-below-min: <pluginId>:<installed>:<required>`).

### 4. Circular dependency

- Plugin A `dependsOn` B, plugin B `dependsOn` A — hər iki plugin skipped.
- Audit qeydi: `plugin.activation.skipped` event-i səbəb (`circular-dependency: <pluginIdA>,<pluginIdB>`).
- Circular dependency topological sort mərhələsində aşkar olunur (DFS cycle detection).

### 5. Self-dependency

- Plugin A `dependsOn` A — self-reference manifest validation failure hesab olunur (ADR-0036 manifest validation).

### 6. Activation failure side-effect

- Plugin B activation fails (exception throw olur `StartAsync`-də); wrapper `ErrorCode.PluginInternalError` qaytarır.
- Plugin A `dependsOn` B və B failed — plugin A activation skipped, audit səbəb (`dependency-activation-failed: <pluginId>`).

### 7. Rejected alternatives

- **Best-effort activation (skipped plugin ignored):** rədd edildi — user-a skipped plugin-ləri görünməli və səbəbi açıq olmalıdır.
- **Parallel activation:** Phase 1-də rədd edildi — sequential activation deterministik davranış verir; parallel P2-də audit lifecycle bitdikdən sonra açıla bilər.
- **Retry on missing dependency:** rədd edildi — restart-da dependency dəyişə bilər; within-session retry semantics gətirir.

## Consequences

- Plugin author `dependsOn` field-ini düzgün elan etməli və minimum versiyanı düzgün göstərməlidir.
- Host UI `Plugins` bölməsində skipped plugin-lər və səbəb görünür; user dependency problem-ini müstəqil həll edə bilər (məs. dependency plugin install).
- Activation ardıcıl olduğu üçün böyük plugin sayında total activation time lineer böyüyür; Phase 1 plugin say baseline-ı ≤ 20 olduğu üçün bu kifayətdir.
- Plugin B activation fails və digər plugin-lər A `dependsOn` B olduğu üçün skip olunur — cascading skip user-a explicit görünür.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 33 `Plugin Dependency Resolution`, Cədvəl 3 `Plugin Dependency Resolution` risk sətri
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — `dependsOn` schema
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md` — `minVersion` comparison semantics
- `docs/adr/ADR-0041-login-first-startup-policy.md` — activation post-login qaydası
