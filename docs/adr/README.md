# Dobby ADR Guide

Bu qovluq Dobby üçün Architecture Decision Record (ADR) fayllarını saxlayır.
ADR-lər artıq verilmiş qərarları rəsmiləşdirir və ya yeni qərarı sabitləşdirir ki,
sonrakı skeleton, contract və implementation pass-ləri eyni mənbəyə söykənsin.

## Naming

- Fayl adı formatı: `ADR-NNNN-kebab-case.md`
- `NNNN` dörd rəqəmli olur və sıralı verilir
- Title fayl adındakı nömrə və mövzu ilə üst-üstə düşür

## Status Lifecycle

Bu qovluqda status terminləri standard ADR keyword-ləri ilə yazılır:

- `Proposed` — qərar təklif edilib, amma hələ qəbul olunmayıb
- `Accepted` — qərar qəbul olunub və aktiv istiqamətdir
- `Superseded` — qərar sonrakı ADR ilə əvəz olunub
- `Deprecated` — qərar tarixi kontekstdə saxlanır, amma artıq istifadə olunmur

Yeni ADR yazılanda status mətni lokallaşdırılmır; sənədin qalan hissəsi isə
Azərbaycanca yazılır.

### Canonical sənədlərdə status terminoloji xəritəsi

Dobby canonical sənədlərində fərqli status sözləri eyni dövrə mərhələsini göstərə bilər. Aşağıdakı xəritə qarışıqlığın qarşısını alır:

| ADR status | `01_PRODUCT_SCOPE.md` | `02_ARCHITECTURE_MAP.md` Status | `02_ARCHITECTURE_MAP.md` Phase |
|---|---|---|---|
| `Accepted` | `Təsdiqlənib` | `agreed` | — (phase ayrı ölçüdür) |
| `Proposed` | — (product scope yalnız təsdiq sənədidir) | `planned` | — |
| `Superseded` | köhnəlmiş bölmə explicit işarə edilir | köhnə row `Superseded` ADR-ə istinad etmir | — |
| `Deprecated` | out-of-scope sətrinə köçür | row mövcud qalır, amma `planned` deyil | `later` və ya silinmiş |

`phase` sütunu (`P1-core / P1-if-time / P2 / later`) status-dan ayrı ölçüdür və `docs/02_ARCHITECTURE_MAP.md § Legend / Taxonomy / Phase` bölməsində tərif olunur.

`ADR-0000-template.md` template-only sənəddir. Onun `Proposed` status nümunəsi
məhsul qərarı kimi oxunmur və current implementation direction yaratmır.

### ADR referans formatı

Bütün canonical sənədlərdə ADR referansları 4 rəqəmli format (`ADR-NNNN`) istifadə edir; 3 rəqəmli format (`ADR-NNN`) yalnız tarixi istinadlarda qalır və yeni sətirlərdə istifadə olunmur.

### Numbering gaps

ADR nömrələri tarixi review və consolidation mərhələlərinə görə tam ardıcıl deyil. Aşağıdakı nömrələr hazırda ayrıca ADR faylı kimi mövcud deyil və yeni skeleton işi üçün blocker sayılmır:

| Gap | Status |
|---|---|
| `ADR-0002` | Event visibility modeli `ADR-0010 Plugin communication policy` daxilinə fold edilib. |
| `ADR-0014` | Messaging UI Bridge qərarı `ADR-0010` və `docs/specs/shell-ui.md` daxilində saxlanılır. |
| `ADR-0017` | Observability scope P2 deferred mövzudur. |
| `ADR-0018` | Health checks scope P2 deferred mövzudur. |
| `ADR-0020` | Feature flag model P2 deferred mövzudur. |
| `ADR-0022` | Host update strategy P2 release mövzusudur. |
| `ADR-0024` | Bulk selection ownership P2 deferred mövzudur. |
| `ADR-0025` | Querying and search boundary P2 deferred mövzudur. |
| `ADR-0034` | Intentionally unused numbering gap from earlier review/consolidation; no current canonical doc references it. |

## Required Structure

Hər ADR ən azı aşağıdakı strukturu saxlamalıdır:

1. Title
2. `Status`
3. `Date`
4. `Context`
5. `Decision`
6. `Consequences`
7. `Cross-references`

Qısa ADR-lərdə də bu bölmələr saxlanılır; boş bölmə buraxılmır.

## Writing Conventions

- Əsas mətn dili Azərbaycancadır
- Stabil texniki terminlər və type adları ingiliscə saxlanır
- Mövcud sənədlərdə artıq verilmiş qərarı formalizə edən ADR yeni product scope
  ixtira etmir; yalnız qərarı dəqiq, enforce edilə bilən formaya salır
- Əgər qərarın bir hissəsi başqa ADR-in öhdəliyindədirsə, bu sərhəd açıq şəkildə
  yazılır

## Cross-references

Hər ADR-də `Cross-references` bölməsi olmalıdır və minimum bunları etməlidir:

- `docs/02_ARCHITECTURE_MAP.md` içində mövzunun bağlı olduğu konkret row və ya sətirə
  istinad etməlidir
- Əlaqəli olduğu digər design sənədlərini (`03_TECH_STACK_AND_VERSIONS.md`,
  `04_RESULT_VS_EXCEPTION_POLICY.md`, digər ADR-lər) əlavə etməlidir
- Əgər bu ADR başqa ADR-in scope-unu məhdudlaşdırır və ya ona söykənirsə, bunu
  açıq göstərməlidir

## Update Rule

Qərar dəyişəndə mövcud Accepted ADR gizlicə edit edilmir. Əvəzində:

- ya yeni ADR yazılıb köhnə sənəd `Superseded` edilir
- ya da decision dəyişməyib, yalnız wording dəqiqləşibsə minimal redaksiya edilir

Bu qovluqdakı ADR-lər `docs/02_ARCHITECTURE_MAP.md` ilə sinxron qalmalıdır. Yeni
Accepted ADR açılırsa, xəritədə uyğun status, blocker və risk mətnləri də
yenilənməlidir.

## Documentation Governance

İki canonical sənəd ziddiyyətli görünəndə mövzuya sahib olan sənəd üstündür; bu üstünlük yalnız ADR və ya explicit amendment ownership-i dəyişdirənə qədər davam edir.

- Product behavior `docs/01_PRODUCT_SCOPE.md`, `docs/05_FUNCTIONAL_REQUIREMENTS.md`, executable spec-lər və qəbul olunmuş ADR-lərə aiddir.
- Architecture sərhədləri `docs/02_ARCHITECTURE_MAP.md` və qəbul olunmuş ADR-lərə aiddir.
- Feature davranışı uyğun `docs/specs/*` faylına aiddir.
- Implementation convention-ları engineering standards (`docs/08_CODING_STANDARDS.md`) və build/run (`docs/09_BUILD_RUN_AND_RELEASE.md`) sənədlərinə aiddir.

Repo ilə canonical sənəd arasında uyğunsuzluq documentation drift və ya implementation drift kimi qeyd olunmalıdır; səssiz uyğunlaşdırma qadağandır. Yeni plan hansı canonical sənədlərə söykəndiyini və hansı ziddiyyətləri bilərəkdən həll etdiyini explicit göstərməlidir.

## Current Storage / Deployment ADRs

The storage/deployment close-out is represented by these Accepted ADRs:

| ADR | Status | Canonical responsibility |
|---|---|---|
| `ADR-0056-deployment-modes-and-storage-providers.md` | Accepted | Standalone vs Branch Shared deployment modes, DeviceLocal/BranchShared scopes, SQLite/PostgreSQL provider boundaries and Branch Shared operational tiering |
| `ADR-0057-mode-aware-authentication-boundary.md` | Accepted | Branch Shared authentication/session/setup boundary, BranchSetup setup proof, pre-auth audit exception and resolved auth-related HD items |

These ADRs were accepted after `HD-001..HD-007` were resolved by human decision.
This is documentation lifecycle acceptance only; Tier A skeleton implementation
still requires separate explicit approval.

## 2026-08 Hardening ADRs

The repository audit completed against the 2026-08-16 cutoff introduced the
following controlling decisions. They override conflicting older wording while
preserving unaffected parts of the earlier ADRs.

| ADR | Status | Canonical responsibility |
|---|---|---|
| `ADR-0058-credential-and-recovery-hardening-v2.md` | Accepted | Username-only remembered login, durable account/device throttling, password-change proof, device-generated recovery keys and support-safe device codes |
| `ADR-0059-plugin-trust-package-and-ui-contract-v2.md` | Accepted | Trusted in-process plugin posture, bounded package validation, signature/key profiles, host API compatibility and declarative plugin UI |
| `ADR-0060-license-lifecycle-and-import-v3.md` | Accepted | Signed grant/revocation intent, derived runtime lifecycle, atomic import/LKG rules and dedicated BranchSetup authorization |
| `ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md` | Accepted | Strict pre-auth runtime boundary, atomic state/audit mutation contract, asynchronous UI work and behavior-first verification |

## Resolved Human Decision Index

Resolved `HD-*` / `DM-*` identifiers are indexed here so later audits can trace
the human decision source instead of reopening accepted decisions by preference.

| Human decision | Decision memo | Resolving canonical docs |
|---|---|---|
| `HD-001` | `DM-071` | `ADR-0057`; `docs/specs/onboarding-and-provisioning.md` |
| `HD-002` | `DM-089` | `ADR-0057`; `docs/specs/audit-and-diagnostics.md`; `docs/specs/postgres-deployment-ops.md` |
| `HD-003` | `DM-090` | `ADR-0057`; `ADR-0055`; `docs/specs/device-lifecycle.md`; `docs/specs/onboarding-and-provisioning.md` |
| `HD-004` | `DM-091` | `ADR-0057`; `ADR-0019`; `docs/specs/device-lifecycle.md`; `docs/specs/onboarding-and-provisioning.md` |
| `HD-005` | `DM-092` | `ADR-0056`; `ADR-0047`; `docs/09_BUILD_RUN_AND_RELEASE.md`; `docs/specs/postgres-deployment-ops.md` |
| `HD-006` | `DM-093` | `ADR-0056`; `ADR-0057`; `ADR-0052`; `docs/specs/license-import.md` |
| `HD-007` | `DM-094` | `ADR-0056`; `docs/specs/README.md` |
| `HD-008` | `DM-095` | `ADR-0040`; `docs/08_CODING_STANDARDS.md` |
| `HD-009` | `DM-096` | `ADR-0036`; `ADR-0048` |
