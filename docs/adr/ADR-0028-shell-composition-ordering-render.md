# ADR-0028: Shell Composition Ordering Render

**Status:** Accepted
**Date:** 2026-04-22

## Context

Ordering contract-ı təkcə `Dobby.Contracts` daxilində düzgün model kimi
yazılmaqla bitmir; bu modelin shell render mərhələsində də eyni şəkildə tətbiq
olunması lazımdır. Əks halda contract deterministic görünər, amma UI real
render zamanı fərqli sıra göstərə bilər.

Owner qərarı shell render-in yalnız ordering yox, konkret shell səthlərini də
canonical daşımasını tələb edir: TopToolbar, Sidebar, Third Bar, Main Content
Area, bottom status/progress və minimized work strip.

Arxitektura xəritəsi bu məsələni artıq blocker olmayan, amma qəbul edilmiş
boundary kimi saxlayır. Stack selection sənədi də snapshot testing-in bu
ordering davranışı üçün seçildiyini deyir.

## Decision

Shell composition render qatı ordering modelini contract-dan ayrıca şərh etmir.
Render səviyyəsi `group + weight + stable tiebreak` modelini olduğu kimi tətbiq
edir və visible order üçün əlavə qeyri-rəsmi prioritet qaydası yaratmır.

Shell render canonical səthləri:

- TopToolbar solda Dobby logo/ad, mərkəzdə navigation, sağda tək light/dark theme
  switch və profile button göstərir.
- Sidebar geniş rejimdə icon/logo + ad, yığcam rejimdə yalnız icon/logo göstərir;
  yığcam rejim hover zamanı ad tooltip/popover göstərə bilər, avtomatik expand etmir.
- Third Bar Main Content Area-nın yuxarı-mərkəz hissəsində island/kəsik vizualı
  kimi render olunur.
- Bottom status/progress sətri host-controlled status və background progress üçün
  ayrılır.
- Minimized active work strip bottom status/progress sətrinin üstündə tab/chip
  kimi görünür.

Notification taxonomy shell render tərəfindən vahid saxlanır:

- toast — qısa, non-blocking məlumat;
- banner — persistent warning/status;
- modal — user təsdiqi tələb edən və ya riskli əməliyyatı dayandıran hal.

Profile menu inventory shell səviyyəsində minimum bu entry-ləri daşıyır:
profile/account info, password change, manual lock, logout və saved login-i
unutmaq. Login ekranındakı saved-login clear yolu ayrıca qalır.

Bu davranış integration və snapshot test-lərlə yoxlanılır. Testlərin məqsədi
yalnız "nə isə render oldu" yox, eyni input üçün order-in stabil qalmasını da
isbat etməkdir.

## Consequences

- Ordering contract ilə UI render arasında semantic drift azalır
- Plugin discovery order və ya container enumeration kimi qeyri-sabit detallara
  görünən order bağlanmır
- Shell layout və notification surface üçün canonical visual/behavioral
  contract yaranır
- Profile menu və bottom status/progress arbitrary plugin UI surface-ə çevrilmir
- Render davranışı regression test ilə qoruna bilir

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `Shell Composition / Hosting` row
- `docs/02_ARCHITECTURE_MAP.md` — `ADR-0028` risk sətri
- `docs/02_ARCHITECTURE_MAP.md` — `UI Extension Ordering` convention
- `docs/adr/ADR-0005-ui-ordering-model.md` — canonical ordering model
- `docs/03_TECH_STACK_AND_VERSIONS.md` — `## 3. Test Stack`, Snapshot testing row
- `docs/specs/shell-ui.md` — shell visual layout, notification və accessibility spec
