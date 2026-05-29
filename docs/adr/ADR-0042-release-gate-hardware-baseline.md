# ADR-0042: Release Gate Hardware Baseline

**Status:** Accepted
**Date:** 2026-04-24

## Context

`06_NON_FUNCTIONAL_REQUIREMENTS.md` NFR-003 pre/post-login startup mərhələlərinin instrument olunmasını tələb edir. `07_ACCEPTANCE_CRITERIA.md` AC-014 Phase 1 release gate-də startup P95 threshold-larını (cold pre-login ≤ 3000ms, warm pre-login ≤ 1000ms, login submit → shell first paint ≤ 800ms) observable check kimi yoxlayır.

Lakin threshold-lar **baseline hardware-siz actionable deyil**:
- fərqli hardware-də eyni kod fərqli P95 verir
- "Dobby yavaşdır" şikayətinin baseline-i olmadan rədd edilə bilməz
- regression budget (> 20% hard fail) yalnız sabit baseline qarşısında anlamlıdır

`03_TECH_STACK_AND_VERSIONS.md` OS baseline-ını (Windows 11 22H2 primary, Windows 10 22H2 ESU/LTSC) bağlayır, amma CPU/RAM/disk baseline-ı bağlanmayıb. Bu ADR release gate P95 ölçmələri üçün canonical reference hardware-i rəsmiləşdirir.

## Decision

### 1. Reference hardware baseline

Release gate P95 ölçmələri aşağıdakı minimum hardware konfiqurasiyasında aparılır:

- **CPU:** Intel i5-10xxx və ya AMD Ryzen 5 5xxx ekvivalenti — 4 physical core / 8 thread minimum, 2.5+ GHz base clock
- **RAM:** 16 GB DDR4
- **Storage:** SATA SSD (NVMe-də də ölçmə götürülə bilər — daha sürətli nəticə verir, amma **regression budget eyni qalır**)
- **Display:** 1920x1080
- **OS:** Windows 11 22H2 və ya Windows 10 22H2 ESU (03_TECH_STACK_AND_VERSIONS.md OS baseline-ı ilə uyğun)
- **Network:** yoxsun (offline-first) — internet baseline-a daxil edilmir

Bu spec Phase 1 enterprise thick-client deployment-inin `median` customer hardware-ini əks etdirir. Daha zəif hardware-də (məs., i3 / 8GB RAM) ölçmələr məlumat xarakterli saxlanılır, release gate-ə deyil.

### 2. Measurement methodology

- **Sample size:** N = 100 run per ölçmə
- **Statistic:** P95 (95th percentile)
- **Cold start pre-login:**
  - OS full reboot-dan sonra ilk launch
  - Dobby app data (local storage, cache, license cache) təmiz silinmiş
  - İlk timer tick: process start
  - Son timer tick: login UI interactive (user input qəbul edə bilir)
- **Warm start pre-login:**
  - OS cache retained (reboot yoxdur)
  - Dobby app data saxlanılır (license cache, settings, plugin manifest cache mövcud)
  - Dobby prosesi clean shutdown-dan sonra yenidən launch
  - Timer tick sərhədləri cold start ilə eyni
- **Login submit → shell first paint:**
  - İlk timer tick: authenticated user credentials submit button
  - Son timer tick: shell frame tamamən render olunmuş (user interactive)
  - Shell plugin extension point-ləri bu timer-də daxil deyil; asenkron qəlsin, baseline ölçmə shell-in öz foundation-ına odaklanır

### 3. Regression budget

- **Baseline build** = Phase 1 ilk rəsmi release git tag-i (məs., `v1.0.0`)
- Baseline build P95 ölçmələri `docs/_perf/baselines/v1.0.0.json` və ya eqvivalent artifact-a commit olunur
- Növbəti hər build `> 20%` regression verərsə CI hard fail sayılır
- Baseline refresh yalnız explicit ADR amendment ilə olur (məs., yeni OS version qəbul edildikdə)

### 4. Baseline hardware refresh schedule

- Reference hardware spec **illik yenidən qiymətləndirilir** (Phase 1-in ömrü boyu)
- Refresh decision `03_TECH_STACK_AND_VERSIONS.md` OS baseline refresh cycle-ı ilə sync olur
- Hardware spec dəyişsə, mövcud `v1.0.0` baseline invalid olur və yeni baseline build tagged edilir

### 5. Rejected alternatives

- **Threshold-ları hardware-dən asılı deyil, absolute ms-lə verək:** rədd edildi — fərqli hardware-də eyni kod fərqli nəticə verir, absolut tələb adaptasiya olunmayan gate yaradır
- **Baseline-i `03_TECH_STACK_AND_VERSIONS.md`-də tək row kimi saxlayaq, ADR yaratmayaq:** rədd edildi — measurement methodology və regression budget çox ətraflıdır, ayrıca ADR olmaq lazımdır
- **Baseline high-end hardware (i7 + 32GB + NVMe):** rədd edildi — median customer hardware-i reflect etməz, "works on my machine" sindromunu gücləndirər
- **Baseline low-end hardware (i3 + 8GB + HDD):** rədd edildi — Phase 1 thick-client enterprise target-i bu səviyyədə deyil, P95 threshold-ları realistic yüksək olardı

## Consequences

- `NFR-003` description-a baseline hardware reference artıq keçərli (threshold-lar actionable)
- `AC-014` release gate-də P95 ölçmələri ADR-0042 hardware spec-inə bağlıdır
- `03_TECH_STACK_AND_VERSIONS.md` OS baseline-a yanaşı `Performance baseline hardware` row-u əlavə olunur
- CI pipeline (future implementation) `v1.0.0` baseline artifact-ı ilə hər build P95-i müqayisə edir; `> 20%` regression-da hard fail
- Baseline ölçmə artifact-ları (`docs/_perf/baselines/`) versiya kontroluna daxil olur
- Baseline hardware dəyişdirmək üçün explicit ADR amendment tələb olunur (reproducibility qorunur)

## Cross-references

- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` NFR-003 — pre/post-login instrumentation
- `docs/07_ACCEPTANCE_CRITERIA.md` AC-014 — startup release gate P95 thresholds
- `docs/adr/ADR-0041-login-first-startup-policy.md` — pre-login forbidden work (measurement sərhədi)
- `docs/03_TECH_STACK_AND_VERSIONS.md` — OS baseline və hardware reference row
