# ADR-0011: Plugin Trust and Policy Enforcement Principle

**Status:** Accepted (trust-boundary wording amended by ADR-0059)
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

Out-of-process plugin worker, brokered process permissions, per-plugin OS isolation
və hardened native isolation P2 trigger-ləridir və yalnız `ADR-0007` future
criteria-ları açılarsa yenidən qiymətləndirilir.

### 4. Test evidence

Skeleton və release evidence minimum aşağıdakıları yoxlamalıdır:

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
- `docs/specs/plugins-manager.md`
- `docs/specs/sample-plugin.md`
