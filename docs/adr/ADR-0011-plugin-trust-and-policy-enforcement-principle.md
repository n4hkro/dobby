# ADR-0011: Plugin Trust and Policy Enforcement Principle

**Status:** Accepted (trust-boundary wording and § 3 isolation framing amended by `ADR-0059`; § 4 evidence strength amended by `ADR-0061` — see ## Revision History)
**Date:** 2026-04-30

## Context

Architecture Map-in köhnə `Plugin Isolation / Sandbox` sətri wrapper obyektinin
təhlükəsizlik sərhədi olmadığını qeyd edirdi. Dobby Phase 1-də arbitrary third-party
plugin modeli açmır; plugin-lər vendor-created və vendor-signed olur. Buna baxmayaraq
host skeleton-u plugin-lərin hansı sərhədlərlə məhdudlaşdırılacağını guess etmədən
yaratmalıdır.

Bu ADR `ADR-0007`, `ADR-0012`, `ADR-0032`, `ADR-0045`, `ADR-0048` və `ADR-0053`
qərarlarını bir enforcement prinsipinə bağlayır.

## Decision

### 1. P1 trust modeli

Phase 1 yalnız reviewed, vendor-signed və tam etibarlı in-process kodu dəstəkləyir.
Aşağıdakı host-controlled enforcement qatları uyğunluq, bütövlük və məhsul
policy-si üçündür; onlar hostile kod üçün sandbox və ya containment deyil:

- vendor signature və trust verification;
- `.dobbyplugin` package validation və forbidden-content reject qaydaları;
- per-plugin `AssemblyLoadContext` dependency isolation;
- `Dobby.Contracts` / `Dobby.SDK`-only plugin-visible surface;
- plugin DI registration bridge-inin host internal service-ləri gizlətməsi;
- operation/capability authorization;
- plugin storage compartment boundary;
- license runtime mode enforcement;
- plugin boundary Result/exception wrapper-i;
- audit/diagnostics redaction və non-disclosing user-facing failure.

### 2. Enforced fail-closed behavior

Host aşağıdakı hallarda fail-closed davranır:

- unsigned, tampered, untrusted və ya forbidden-content package install olunmur;
- plugin host implementation project-inə, başqa plugin assembly-sinə və ya
  `Dobby.Infrastructure`-a direct reference saxlayırsa reject edilir;
- plugin capability/operation icazəsi yoxdursa request deny edilir;
- license mode `Denied`-dirsə plugin runtime access almır;
- `ReadOnlyExpired` mode-da mutation, export/report/file output, print-to-file,
  background task start və plugin/domain import deny edilir;
- runtime screen/task/lifecycle hook fault-u host prosesini çökdürmür, həmin
  plugin instance/screen/task `Faulted` və ya stopped state-ə keçir.

### 3. P1 in-process isolation limitləri

Phase 1 in-process plugin modeli malicious vendor-signed code-a qarşı tam memory,
CPU, native-call, thread, environment və process containment zəmanəti vermir.
Vendor signing trust boundary-nin hissəsidir. Corrupted-state fatal exception-lar
(`OutOfMemoryException`, `StackOverflowException`) host tərəfindən tutulmur.

Fully trusted olmayan paket host prosesində icra edilmir. Untrusted və ya semi-trusted
vendor dəstəyi ayrıca approve olunmuş out-of-process worker tələb edir: OS-enforced
identity, IPC, resource və file/network boundary-ləri ilə. Bu, P1 üçün yenidən
qiymətləndirilən trigger deyil, həmin vendor sinfini dəstəkləməyin ilkin şərtidir
(`ADR-0059` § `Trust posture`). Brokered process permissions, per-plugin OS isolation və
hardened native isolation fully trusted vendor-signed model üçün P2 mövzusu olaraq qalır
və `ADR-0007` future criteria-ları ilə birlikdə qiymətləndirilir; onların yoxluğu
yuxarıdaki ilkin şərti yumşaltmır.
### 4. Test evidence

Skeleton və release evidence minimum aşağıdakıları yoxlamalıdır. Bu siyahı zəruri
minimumdur, kifayət edən sübut dəsti deyil: static/dependency scan və forbidden-content
reject documentation/architecture shape-i enforce edir, lakin runtime containment sübutu
sayılmır və arbitrary .NET API-lərin məhdudlaşdırıldığını göstərmir. Runtime davranış,
təhlükəsizlik və release readiness üçün behavior-first evidence tələb olunur
(`ADR-0059` § `Trust posture`, `ADR-0061`, `NFR-027`):

- plugin project static/dependency scan yalnız `Dobby.Contracts` / `Dobby.SDK`
  reference-lərinə icazə verir;
- package validation forbidden content və implementation reference-ləri reject edir;
- permission və runtime mode deny-ları non-disclosing user-facing result qaytarır;
- sample plugin fault action host process-i çökdürmədən fault isolation yaradır;
- diagnostics raw detail saxlayır, audit yalnız lifecycle/security-safe typed
  reason və correlation id saxlayır.

## Consequences

- Policy wrapper-ları authorization və məhsul qaydalarını enforce edir, lakin
  in-process kodu host prosesindən təhlükəsizlik baxımından təcrid etmir.
- P1 scope arbitrary/user-installed third-party plugin marketplace-i açmır.
- Security review OS/process isolation olmamasını P1 limit kimi görür, amma bu
  limit vendor-signed plugin modelinə uyğun documented tradeoff-dur.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 39 `Plugin Trust / Policy Enforcement`
- `docs/adr/ADR-0007-plugin-load-context-strategy.md`
- `docs/adr/ADR-0012-plugin-capability-boundary.md`
- `docs/adr/ADR-0032-plugin-signature-trust-model.md`
- `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md`
- `docs/adr/ADR-0048-operation-based-authorization.md`
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md`
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — current trust posture, bounded package validation and signature/key profile authority
- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md` — current license runtime mode authority
- `docs/adr/ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md` — behavior-first verification authority for the `## 4` evidence list; source-text and dependency scans enforce shape but do not prove runtime behavior, security or release readiness
- `docs/specs/plugins-manager.md`
- `docs/specs/sample-plugin.md`

## Revision History

- **2026-08-31:** Amendment record yazıldı. `**Status:**` sətri artıq trust-boundary
  wording-in `ADR-0059` ilə amend olunduğunu bildirirdi, lakin sənəddə nə dated bölmə,
  nə də amend olunmuş cümlələrin düzəlişi var idi -- yəni `docs/adr/README.md`-in birgə
  tələb etdiyi iki şərtdən yalnız biri ödənilirdi və § 3 hələ də retired qaydayı
  daşıyırdı. § 3 əvvəl out-of-process worker, brokered process permissions və per-plugin
  OS isolation-ı bir yerdə "P2 trigger-ləridir və yalnız `ADR-0007` future criteria-ları
  açılarsa yenidən qiymətləndirilir" kimi təsvir edirdi. `ADR-0059` § `Trust posture` isə
  fully trusted olmayan paketin host prosesində icrasını qadağan edir və out-of-process
  worker-i həmin vendor sinfi üçün ilkin şərt edir; bölməni oxuyan reader isolation
  qaydasını § 3-dən götürdüyü üçün retired mətn səhv qaydaya yönləndirirdi. İki hal indi
  ayrılıb: fully trusted olmayan vendor üçün ilkin şərt, fully trusted model üçün qalan
  P2 mövzuları. § 4 evidence siyahısı isə "zəruri minimum, kifayət edən dəst deyil" kimi
  qeyd olunub -- `## Cross-references` bunu artıq `ADR-0061`-ə həvalə edirdi, lakin siyahı
  özü static scan bullet-lərini acceptance evidence kimi göstərirdi (`NFR-027`).
  Yeni öhdəlik əlavə edilmir və ləğv edilmir: hər iki düzəliş `ADR-0059` və `ADR-0061`-in
  mövcud qərarlarını təkrar ifadə edir, ona görə bu amendment record-dur, successor ADR
  deyil. Reverse edge `ADR-0059` `**Amends:**` header-ində qeyd olunub.
