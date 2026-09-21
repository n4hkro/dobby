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

### Supersession header formatı

Supersession iki istiqamətdə də **header block-da** qeyd olunur, yalnız body
mətnində deyil:

- Superseded ADR-in `Status` sətri successor-u adlandırır (`Superseded by ADR-NNNN`).
  Ayrıca `**Superseded by:**` sətri əlavə izah (zəncir, qismən scope) üçün saxlanıla
  bilər, amma `Status` sətrini əvəz etmir.
- Successor ADR `**Supersedes:**` header sətrində əvəz etdiyi ADR-i adlandırır.
- `**Supersedes:**` **yalnız tam supersession** üçündür: orada adlanan ADR-in
  `Status`-u `Superseded` olmalıdır. Hələ `Accepted` qalan sənədi orada adlandırmaq
  onu retired kimi elan edir və header oxuyan reader-i yanıldır.
- Qismən amend üçün ayrıca `**Amends:**` sətri istifadə olunur: amend edilən ADR
  `Accepted` qalır və toxunulmamış bölmələri üçün authority olaraq qalır. Hər iki
  istiqamət yazılır — amend edilən ADR-in `Status` sətri də amend edəni adlandırır
  (`Accepted (… amended by ADR-NNNN)`).
- `**Clarifies:**` sətri qərar dəyişmədən mövcud ADR-in bir bölməsinin dəqiq
  mənasını sahiblənən sənədi göstərir (məsələn derivation detalı). Bu da iki
  istiqamətlidir: clarify edilən ADR `**Clarified by:**` sətri daşıyır, əks halda
  normativ detal onu tələb edən sənəddən əlçatmaz qalır.

Səbəb: audit və tooling ADR-in vəziyyətini ilk növbədə `Status` sətrindən oxuyur.
Bare `Superseded` status (successor yalnız aşağıdakı sətirdə) status-scan edən
oxucuya "successor yoxdur" kimi görünür — yəni supersession zəncirinin qırıldığı
kimi. Bu format `docs/adr/README.md § Supersession Index` cədvəli ilə birlikdə
enforce olunur, və `tests/Dobby.IntegrationTests/Architecture/AdrSupersessionConsistencyTests.cs` bu shape-i pozan header-i fail edir (`NFR-027` çərçivəsində: shape guard-dır, davranış sübutu deyil).

### Canonical sənədlərdə status terminoloji xəritəsi

Dobby canonical sənədlərində fərqli status sözləri eyni dövrə mərhələsini göstərə bilər. Aşağıdakı xəritə qarışıqlığın qarşısını alır:

| ADR status | `01_PRODUCT_SCOPE.md` | `02_ARCHITECTURE_MAP.md` Status | `02_ARCHITECTURE_MAP.md` Phase |
|---|---|---|---|
| `Accepted` | `Təsdiqlənib` | `agreed` | — (phase ayrı ölçüdür) |
| `Proposed` | — (product scope yalnız təsdiq sənədidir) | `planned` | — |
| `Superseded` | köhnəlmiş bölmə explicit işarə edilir | köhnə row `Superseded` ADR-ə istinad etmir | — |
| `Deprecated` | out-of-scope sətrinə köçür | row mövcud qalır, amma `planned` deyil | `later` və ya silinmiş |

`phase` sütunu (`P1-core / P1-if-time / P2 / P3 / later`) status-dan ayrı ölçüdür; qapalı vocabulary və keçid meyarları `docs/02_ARCHITECTURE_MAP.md § Legend / Taxonomy / Phase` bölməsində tərif olunur. Branch Shared rollout tier-ləri (`documentation tier`, `Tier A`, `Tier B`) bu ox deyil (`ADR-0056`).

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
| `ADR-0062-signing-key-custody-and-trust-anchor-provisioning.md` | Accepted | Purpose-separated vendor key hierarchy, private key custody, version-controlled public trust anchors, `keyId` selector, bounded rotation rings and per-purpose compromise response (supersedes `ADR-0033`) |
| `ADR-0063-device-license-binding-derivation.md` | Accepted | Canonical versioned license device binding digest, derivation versioning tied to artifact schema version, digest-only vendor input and non-disclosing mismatch semantics (clarifies `ADR-0060`, `ADR-0058`) |

## Supersession Index

Bu cədvəl hər superseded və ya qismən amend edilmiş ADR üçün **current authority**-ni
göstərir. Məqsəd: superseded ADR-in mətnini oxuyan işçi (və ya audit) hansı sənədin
indi qərar sahibi olduğunu axtarmadan bilsin. Superseded ADR yeni iş üçün authority
kimi istinad edilmir; onun body-si yalnız historical context-dir.

| Superseded / amended ADR | Status sətri | Current authority |
|---|---|---|
| `ADR-0026-license-lifecycle.md` | Superseded by ADR-0051 (chain: ADR-0051 superseded by ADR-0060) | `ADR-0060` (via `ADR-0051`); plugin-facing runtime mode `ADR-0053` |
| `ADR-0033-signing-key-ownership.md` | Superseded by ADR-0062 | `ADR-0062` — purpose-separated keys, `keyId` selector, anchor provisioning |
| `ADR-0035-tenant-customer-context-scope.md` | Superseded by ADR-0052 | `ADR-0052` |
| `ADR-0039-owner-password-recovery.md` | Superseded by ADR-0058 | `ADR-0058` (recovery binding, nonce/request consumption), `ADR-0062` (recovery response signing key), `ADR-0063` (device binding derivation) |
| `ADR-0051-license-lifecycle-v2.md` | Superseded by ADR-0060 | `ADR-0060` + `docs/schemas/license-v3-lifecycle-truth-table.md`; key identity `ADR-0062` |
| `ADR-0006`, `ADR-0021`, `ADR-0049` | Accepted (partial) | page/lifecycle minimum portions → `ADR-0059` |
| `ADR-0007`, `ADR-0011`, `ADR-0012` | Accepted (partial) | trust/security-boundary claims → `ADR-0059` |
| `ADR-0008` | Accepted (partial) | pre-auth construction/I-O → `ADR-0061`; lifecycle hooks → `ADR-0059`; license → `ADR-0060` |
| `ADR-0019`, `ADR-0041` | Accepted (partial) | atomicity / runtime boundary → `ADR-0061` |
| `ADR-0023` | Accepted | recovery artefakt binding və purpose key → `ADR-0058`, `ADR-0062` (generation ownership dəyişmir) |
| `ADR-0031` | Accepted | Owner recovery session/nonce invalidation → `ADR-0058` |
| `ADR-0032` | Accepted (partial) | trust anchor identity, key purpose separation, rotation → `ADR-0062`; signature-format profile və plugin trust UI/status ownership → `ADR-0059` § `Signatures, fingerprint, and keys` |
| `ADR-0036`, `ADR-0037` | Accepted (partial) | package limits, `hostApiVersion`, lifecycle → `ADR-0059` |
| `ADR-0038` | Accepted (partial) | credential storage və throttle → `ADR-0058` |
| `ADR-0040` | Accepted (partial) | recovery proof material → `ADR-0058`; license device binding → `ADR-0063` (artifact authority `ADR-0060`) |
| `ADR-0055` | Accepted (partial) | remembered-login, device recovery key pair, throttle ledger scope → `ADR-0058`; trust anchor material → `ADR-0062` |
| `ADR-0057` | Accepted (partial) | credential və BranchSetup proof → `ADR-0058`, `ADR-0060` |

Qayda: superseded ADR-in **qərar body-si** rewrite edilmir və oraya yeni normativ
sətir əlavə edilmir; qərar dəyişirsə successor ADR yazılır. Header-ə bitişik,
sərhədləri bəlli routing note-u (oxucunu cari authority-yə yönəldən) istisnadır və
body qərarını dəyişmir.

Qismən amend olunan Accepted ADR üçün iki şərt **birlikdə** məcburidir:

1. düzəliş dated bölmə ilə açıq göstərilir — `## Revision History` (üstünlük verilən,
   repo-da hakim forma) və ya dated `## … Amendment`; ikisi eyni öhdəliyi daşıyır və
   hansının seçilməsi sərbəstdir;
2. `Status` sətri bunu qeyd edir (`Accepted (… — see ## Revision History)`), çünki
   audit və tooling ilk növbədə `Status`-u oxuyur. Bare `Accepted` status oxucuya
   "mətn son review-dan bəri dəyişməyib" siqnalı verir.

Nə düzəlişdir, nə successor tələb edir: mövcud və enforce olunan davranışı dəqiq
ifadə etmək düzəlişdir; yeni öhdəlik əlavə etmək və ya mövcud öhdəliyi ləğv etmək
successor ADR tələb edir. Şübhə halında successor yazılır.

## Audit Classification Vocabulary

Traceability və audit nəticələri **yalnız** bu state-lərlə işarələnir. Vocabulary
əvvəllər `docs/00_DOCUMENT_AUTHORITY_AND_AUDIT_BASELINE.md`-də yaşayırdı; o fayl
`a8a5914` ilə retire edildikdə tərif repo-da heç yerdə qalmadı, ona görə burada
bərpa olunur (governance mətninin canonical yeri `README.md` § `Authority Order`
tərəfindən bu fayla yönəldilir).

| State | Mənası |
|---|---|
| `Implemented` | Runtime davranış mövcuddur və mənalı verification var. |
| `Implemented-Weak` | Əsas davranış var, lakin material design, resilience, security, usability və ya proof zəifliyi daşıyır. |
| `Partial` | Tələb olunan end-to-end davranışın yalnız bir hissəsi mövcuddur. |
| `Incorrect` | İmplementasiya controlling requirement-ə zidddir və ya correctness/security defekti var. |
| `Unimplemented` | Production implementasiyası yoxdur. |
| `Deferred-by-design` | Qəsdən cari release scope-undan kənardır. |
| `Evidence-only` | Sənəd, static scan, fixture və ya artifact mətni var, kifayət qədər runtime sübut yoxdur. |

`Evidence-only` `NFR-027` / `ADR-0061` sərhədinin adıdır: source-text, regex,
file-existence və dependency-graph assertion-ı yalnız yoxladığı mətni sübut edir,
runtime davranış, təhlükəsizlik və ya release readiness sübutu deyil. Bir tələbin
yeganə dayağı belə bir test isə onun state-i `Implemented` deyil, `Evidence-only`-dir.

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
