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
- Sıra **deterministik** və culture-invariant-dır. Konkret qayda depth-first post-order-dur:
  traversal root-ları `pluginId` ordinal ascending sıra ilə seçilir, hər node-un
  `dependsOn` edge-ləri də `pluginId` ordinal ascending sıra ilə gəzilir, node isə bütün
  dependency-ləri emit olunandan **sonra** sıraya əlavə olunur. Bu, "eyni topological level-də olan
  plugin-lər `pluginId` ascending gedir" (Kahn-tipli level-by-level) qaydası **deyil**: dependency-si
  olan bir plugin, ondan asılı olmayan və `pluginId`-si daha kiçik olan plugin-dən əvvəl activate
  ola bilər, çünki onun dependency zənciri DFS ilə birlikdə emit olunur. Deterministik olan tək
  şey verilmiş candidate set üçün nəticənin həmişə eyni olmasıdır.
- Activation `IHostedService` post-login wrapper-i tərəfindən ardıcıl (sequential) icra olunur; Phase 1-də parallel activation yoxdur.

### 2. Missing dependency

- Plugin A `dependsOn` plugin B elan edibsə, amma B host-da quraşdırılmayıbsa və ya disabled-dirsə, plugin A activation-a **buraxılmır** (skipped), səbəb `missing-dependency: <pluginId>`.
- Disabled plugin-in özü də bu mərhələdə `Skipped` outcome-u alır, səbəb `disabled` — yəni disabled
  vəziyyət activation run-ın outcome siyahısında explicit görünür, sükutla buraxılmır.
- Graph mərhələsində cascade da eyni səbəb kodu ilə ifadə olunur: dependency-si activation set-ində
  olmayan **hər** plugin `missing-dependency: <pluginId>` alır — o cümlədən dependency quraşdırılıb,
  enabled və version-uyğun olduğu halda, amma özü başqa səbəbdən (cycle, öz dependency-si, disabled
  transitive zəncir) skip olunduqda. Yəni `missing-dependency` "installed deyil" iddiası deyil,
  "bu run-da activate olmayacaq" iddiasıdır; dəqiq root cause skip olunmuş dependency-nin **öz**
  outcome sətrindədir. Cascade fixed-point-ə qədər təkrarlanır, ona görə zəncir uzunluğundan asılı
  olmayaraq tranzitivdir.
- Missing dependency install-time package validation failure deyil. Valid signed package install oluna bilər; dependency vəziyyəti discovery/activation graph mərhələsində qiymətləndirilir.
- Skipped plugin üçün audit qeydi yaradılır: `plugin.activation.skipped` event-i səbəb (`missing-dependency: <pluginId>`) ilə.
- Skipped plugin UI-da `Plugins` bölməsində görünür. Manager UI-a yazılan status summary-si
  non-disclosing sabit mətndir: `Skipped - Dependency missing`, `Skipped - Dependency version too
  low`, `Skipped - Dependency cycle detected`, `Skipped - Dependency activation failed`,
  `Skipped - Plugin disabled`. Raw səbəb sətri (plugin id-ləri və versiyaları ilə) audit tərəfində
  qalır.
- Missing dependency aşkar ediləndə activation fatal deyil — digər plugin-lərin activation-ı davam edir.

### 3. Missing dependency version

- Plugin A `dependsOn` plugin B `minVersion = 1.2.0` elan edib, amma installed B `1.1.0`-dır — plugin A skipped, audit səbəb (`dependency-version-below-min: <pluginId>:<installed>:<required>`).
- Müqayisə `ADR-0037` version semantics-i ilə eynidir (SemVer precedence, `installed >= minVersion`).
  Müqayisə **fail-closed**-dur: installed və ya `minVersion` sətri parse olunmursa nəticə "uyğun
  deyil" sayılır və plugin eyni `dependency-version-below-min` səbəbi ilə skip olunur. Malformed
  version heç bir halda "uyğundur" kimi qiymətləndirilmir.

### 4. Circular dependency

- Plugin A `dependsOn` B, plugin B `dependsOn` A — hər iki plugin skipped.
- Audit qeydi: `plugin.activation.skipped` event-i səbəb (`circular-dependency: <members>`), burada
  `<members>` cycle-ın **bütün** üzvlərinin `pluginId` ordinal ascending, vergüllə (boşluqsuz)
  birləşdirilmiş siyahısıdır; iki üzvlü cycle üçün bu `circular-dependency: <pluginIdA>,<pluginIdB>`
  formasını verir, üç üzvlü cycle üçün üç id-ni.
- Circular dependency topological sort mərhələsində aşkar olunur (DFS cycle detection).
- Precedence normativdir: disabled / missing / version qiymətləndirməsi cycle detection-dan
  **əvvəl** işləyir və artıq skip olunmuş plugin-lər cycle traversal-ından çıxarılır. Ona görə
  cycle üzvü olan, amma həm də missing/below-min dependency-si olan plugin `circular-dependency`
  yerinə həmin daha spesifik səbəbi alır, qalan cycle üzvləri isə cascade ilə skip olunur. Bu
  qayda outcome sətri observable olduğu üçün implementasiya detalı deyil.

### 5. Self-dependency

- Plugin A `dependsOn` A — self-reference manifest validation failure hesab olunur (`ADR-0036`
  manifest validation, reason code `self-dependency`), yəni belə paket install mərhələsində rədd
  olunur.
- Graph tərəfi bundan asılı deyil: əgər self-referencing manifest hər hansı yolla activation-a
  çatarsa, cycle detection onu tək üzvlü cycle kimi görüb `circular-dependency: <pluginId>` ilə skip
  edir. Bu defense-in-depth-dir, manifest validation-ın əvəzi deyil.

### 6. Activation failure side-effect

- Plugin B-nin activation-ı hər hansı fazada uğursuz olarsa — module instantiation, DI
  registration prepare, `InitializeAsync`, `ActivateAsync` və ya v1 `OnActivated` — B-nin outcome-u
  `PluginExceptionBoundary` klassifikasiyasına görə `Faulted` (plugin defect) və ya `Rejected` (host
  policy/contract) olur; səbəb kodu module fazası üçün `ErrorCode`, lifecycle fazaları üçün
  `lifecycle-<phase>-<errorcode>` formasındadır.
- Belə failure **run-ı dayandırmır**: post-login activation wrapper-i `Completed` run qaytarır və
  digər plugin-lər activate olmağa davam edir. Wrapper failure result-u yalnız DI commit
  addımının uğursuzluğunda qaytarır (`ErrorCode.PluginInternalError` və ya commit-in öz error
  kodu) — plugin-in öz activation defect-i host-un activation nəticəsini failure-a çevirmir
  (`NFR-008`).
- Plugin A `dependsOn` B və B activate olmayıbsa — plugin A activation skipped, audit səbəb
  (`dependency-activation-failed: <pluginId>`).
- Bu skip **tranzitivdir**: skip olunmuş A özü də "activate olmayanlar" çoxluğuna daxil edilir, ona
  görə C `dependsOn` A → C də `dependency-activation-failed: A` ilə skip olunur. Heç bir plugin
  `dependsOn` zəncirinin hər hansı halqası activate olmadan activate olunmur. Səbəb kodu hər
  halqada həmin plugin-in **birbaşa** dependency-sini göstərir, ilkin fault mənbəyini deyil; mənbə
  zəncirin ilk halqasının öz outcome sətrindədir.

### 7. Rejected alternatives

- **Best-effort activation (skipped plugin ignored):** rədd edildi — user-a skipped plugin-ləri görünməli və səbəbi açıq olmalıdır.
- **Parallel activation:** Phase 1-də rədd edildi — sequential activation deterministik davranış verir; parallel P2-də audit lifecycle bitdikdən sonra açıla bilər.
- **Retry on missing dependency:** rədd edildi — restart-da dependency dəyişə bilər; within-session retry semantics gətirir.

## Consequences

- Plugin author `dependsOn` field-ini düzgün elan etməli və minimum versiyanı düzgün göstərməlidir.
- Host UI `Plugins` bölməsində skipped plugin-lər və səbəb görünür; user dependency problem-ini müstəqil həll edə bilər (məs. dependency plugin install).
- Activation ardıcıl olduğu üçün böyük plugin sayında total activation time lineer böyüyür; Phase 1 plugin say baseline-ı ≤ 20 olduğu üçün bu kifayətdir.
- Plugin B activation fails və digər plugin-lər A `dependsOn` B olduğu üçün skip olunur — cascading
  skip user-a explicit görünür və zəncir boyu tranzitivdir (§6), yəni bir plugin heç vaxt
  yarımçıq dependency zənciri ilə işə düşmür.
- Activation sırası deterministikdir, amma level-stable deyil (§1); test və plugin author gözləntisi
  bu qaydaya söykənməlidir — "pluginId-si kiçik olan həmişə əvvəl activate olur" gözləntisi yanlışdır.
- `missing-dependency` səbəb kodu iki halı birləşdirir (quraşdırılmayıb/disabled və bu run-da skip
  olunub); root cause analizi həmişə dependency-nin öz outcome sətrini tələb edir (§2).
- Manager UI status summary-ləri qapalı, non-disclosing sabit mətn siyahısıdır (§2); yeni skip səbəb
  kodu əlavə edilərsə həmin siyahıya da uyğun sabit mətn əlavə olunmalıdır, əks halda user
  `Skipped - Plugin skipped` kimi actionable olmayan mətn görür.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 33 `Plugin Dependency Resolution`, Cədvəl 3 `Plugin Dependency Resolution` risk sətri
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — `dependsOn` schema, `self-dependency` manifest validation reason-u
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md` — `minVersion` comparison semantics
- `docs/adr/ADR-0041-login-first-startup-policy.md` — activation post-login qaydası
- `docs/adr/ADR-0008-deferred-plugin-activation.md` — activation outcome/status surface-i və səbəb kodları
- `docs/specs/plugins-manager.md` — manager UI status surface-i
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-008` plugin fault isolation, `NFR-027` behavior-first proof

## Revision History

- **2026-08-28:** ADR shipped `src/Dobby.PluginFramework/Activation/PluginDependencyGraph.cs`,
  `src/Dobby.PluginFramework/Activation/PostLoginPluginActivator.cs` və
  `src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs` ilə uzlaşdırıldı; §2–§6 dəqiqləşdirildi
  və iki davranış defekti düzəldildi. Qərar (topological sort, skip-instead-of-fail, cascade)
  dəyişmir, ona görə successor ADR yaradılmır.
  (a) §1 "topological sort stable-dir: eyni topological level-də olan plugin-lər `pluginId` ordinal
  ascending order ilə activate olunur" yazırdı. Shipped resolution Kahn-tipli level traversal deyil,
  DFS post-order-dur; `a (dependsOn z)` və `b` üçün nəticə `z, a, b`-dir, level-stable qayda isə
  `b, z, a` verərdi. Bu yanlış qayda əsasında yazılan test və ya plugin author gözləntisi səhv
  olardı, ona görə §1 real deterministik qaydanı (ordinal root seçimi + ordinal edge traversal +
  post-order emit) yazır və level-stable oxunuşu explicit rədd edir.
  (b) §2 disabled plugin-in **öz** `Skipped` / `disabled` outcome-unu və graph-level cascade-in
  `missing-dependency: <depId>` səbəb kodunu təkrar istifadə etdiyini heç yerdə demirdi. Nəticədə
  operator quraşdırılmış və enabled dependency üçün "Dependency missing" mətnini görüb səbəbi
  yanlış diaqnoz edə bilərdi. §2 indi hər iki halı, cascade-in fixed-point/tranzitiv olduğunu və
  root cause-un dependency-nin öz outcome sətrində olduğunu yazır; UI status summary-lərinin qapalı
  non-disclosing siyahısı da real shipped mətnlərlə göstərilir (əvvəlki `Skipped (missing
  dependency)` mətni mövcud deyil).
  (c) §3 müqayisə semantics-i və malformed version davranışını bağlamırdı; indi `ADR-0037` semantics-i
  və fail-closed qayda (parse olunmayan version "uyğundur" sayılmır) explicit-dir. §4 cycle səbəb
  sətrinin bütün üzvləri ordinal ascending daşıdığını və disabled/missing/version qiymətləndirməsinin
  cycle detection-dan əvvəl işlədiyini (precedence) normativ edir. §5 shipped manifest reason code-u
  (`self-dependency`) və graph-level defense-in-depth-i qeyd edir.
  (d) §6 "exception throw olur `StartAsync`-də; wrapper `ErrorCode.PluginInternalError` qaytarır"
  yazırdı — hər ikisi yanlış idi: plugin lifecycle fazaları `InitializeAsync`/`ActivateAsync`
  (və v1 `OnActivated`)-dır, `StartAsync` host hosted-service metodudur, və plugin activation
  defect-i wrapper nəticəsini failure-a çevirmir (`NFR-008`) — failure result yalnız DI commit
  addımından gəlir. §6 indi faza siyahısını, `Faulted`/`Rejected` klassifikasiyasını,
  `lifecycle-<phase>-<errorcode>` səbəb formasını və run-ın davam etdiyini yazır.
  (e) **Davranış defekti düzəldildi (kod):** `PostLoginPluginActivator` cascade-i yalnız *failed*
  plugin-lər üzərində aparırdı, skip olunmuş plugin-i "activate olmayanlar" çoxluğuna əlavə etmirdi.
  Ona görə `C → A → B` zəncirində B activation-da fault verəndə A düzgün skip olunur, amma C
  **activate olurdu** — yəni plugin öz `dependsOn` dependency-si yüklənmədən işə düşürdü (registered
  service/extension point gözləntiləri pozulur). §6-nın tranzitivlik qaydası indi normativdir və
  kod `unavailablePlugins` çoxluğu ilə (failed ∪ skipped) uyğunlaşdırıldı. Behavior evidence:
  `tests/Dobby.UnitTests/PluginFramework/Activation/PostLoginPluginActivatorTests.cs`
  (`Activation_cascades_dependency_skip_transitively_through_the_dependency_chain`) və
  `tests/Dobby.IntegrationTests/Plugins/PluginActivationRuntimeIntegrationTests.cs`
  (`Host_activation_reports_version_skip_summary_and_cascades_skips_transitively`) — test əvvəlcə
  `leaf`-in activate olduğunu göstərərək defekti təsdiqlədi, düzəlişdən sonra keçir (`NFR-027`).
  (f) **Diaqnostika defekti düzəldildi (kod):** `dependency-version-below-min` manager UI-da heç bir
  safe summary-yə map olunmurdu və user `Skipped - Plugin skipped` görürdü. Artıq
  `Skipped - Dependency version too low` mətni verilir (§2 siyahısı); non-disclosing qayda pozulmur,
  çünki mətn nə versiya, nə plugin id daşımır. Behavior evidence yuxarıdakı integration test-dədir.
