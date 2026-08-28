# ADR-0053: Capability Surface Runtime Mode

**Status:** Accepted
**Date:** 2026-04-25

## Context

Owner D-3 read-only expired mode-u, D-4 isə plugin-visible licensing surface-in runtime
mode modeli olmasını seçdi. Köhnə `ICapabilityLicensing.IsFeatureAllowed(string)` binary
surface-i read-only expired state-i yaxşı ifadə etmir: plugin "tam işləyə bilərəm",
"yalnız baxışdayam" və "bloklanmışam" fərqini bilməlidir, amma raw license detail
görməməlidir.

Bu ADR `ADR-0012` və `ADR-0004`-dəki licensing nümunəsini yeniləyir. Həmin ADR-lərin
capability boundary və typed policy enforcement qərarları qüvvədə qalır.

## Decision

### 1. Plugin-visible runtime mode

Licensing capability plugin-ə raw detail yox, non-disclosing runtime mode verir.
Canonical semantic:

```csharp
public enum PluginRuntimeMode
{
    FullAccess,
    ReadOnlyExpired,
    Denied
}

public interface ICapabilityLicensing
{
    Result<PluginRuntimeMode> GetCurrentMode();
}
```

Interface və enum adları P1 public contract kimi təsdiqlənir:
`PluginRuntimeMode`, `ICapabilityLicensing` və `GetCurrentMode()` canonical
adlar olaraq qalır. `IsFeatureAllowed(string)` Phase 1 canonical licensing
surface-i deyil və yeni skeleton surface-i kimi yaradılmır.

### 2. Mode semantics

- `FullAccess`: plugin normal işləyə bilər.
- `ReadOnlyExpired`: plugin köhnə nəticələri göstərə bilər, amma mutating və
  data-extraction əməliyyatları host tərəfindən deny edilir.
- `Denied`: plugin runtime access almır.

`NoLicense` runtime mode deyil; `ADR-0051`-də `no-license` lifecycle state-i
plugin-facing surface-də `Denied` kimi görünür və UI/license status səthində
`NoLicense` / localized `No license` kimi göstərilə bilər.

Plugin bu mode-lardan license id, expiry, customer/deployment/device/package binding,
grace, no-license səbəbi və revision detail çıxara bilməməlidir.

### 3. Host enforcement remains authoritative

Plugin mode-a əsasən UI-ni uyğunlaşdıra bilər, amma security enforcement plugin-in
özünə etibar etmir. Host fail-closed gate-lər:

- storage write;
- command/action pipeline;
- background task start;
- plugin/domain import/export/report/file output;
- print target classification.

Print output yalnız host print service target-i non-file physical printer kimi
deterministic təsdiqləyə bildikdə allow olur. Unknown və virtual/file-output target-lər
deny edilir. `IPrintTargetClassifier` host-internal abstraction-dır, production
implementation-ı `Dobby.Infrastructure` print/OS adapter qatıdır və
`Dobby.Application` command/action pipeline tərəfindən çağırılır; plugin-facing
contract deyil. Acceptance test fiziki printer tələb etmir; `IPrintTargetClassifier`
test double istifadə olunur.

Canonical project/namespace placement:

- `IPrintTargetClassifier`, `PrintTargetDescriptor` və `PrintTargetClass`
  abstraction/DTO/vocabulary tipləri host-internal
  `Dobby.Application.Printing` namespace-ində yaşayır.
- Physical print/OS adapter implementation `Dobby.Infrastructure.Printing`
  namespace-ində yaşayır.
- Bu surface `Dobby.Contracts` və ya `Dobby.SDK` plugin-visible contract-ına
  əlavə edilmir.

P1 classifier contract narrow DTO və closed classification vocabulary daşıyır:

```csharp
public sealed record PrintTargetDescriptor(
    string TargetName,
    string DriverName,
    string PortName,
    bool IsLocalPhysicalPrinter);

public enum PrintTargetClass {
    PhysicalPrinter,
    VirtualOrFileOutput,
    Unknown
}

public interface IPrintTargetClassifier {
    PrintTargetClass Classify(PrintTargetDescriptor target);
}
```

`PhysicalPrinter` allow edilə bilən yeganə class-dır; `VirtualOrFileOutput` və
`Unknown` fail-closed deny olunur. Production implementation ownership
`Dobby.Infrastructure.Printing` print/OS adapter qatında qalır; consumer
`Dobby.Application` command/action pipeline-dır. Bu contract plugin-facing surface
deyil.

`.dobbylicense` import `ReadOnlyExpired` state-dən çıxmaq üçün host-level licensing
flow-dur və plugin/domain import kimi bloklanmır. Bu flow host tərəfindən signature,
binding, package fingerprint və authorization permission check-ləri ilə ayrıca enforce
olunur. User-facing entry point `Plugins > License`-dır; Plugin Details shortcut-u eyni
flow-a gedir və About import yeri deyil.

### 4. Typed policy pattern

Runtime mode hesablanması `ADR-0004` typed policy pattern-inə zidd deyil. Licensing mode
evaluation host-controlled policy-dir; plugin raw policy internals-ə çıxmır. Operation
authorization üçün `OperationPermissionCapability` modeli `ADR-0048` ilə ayrıca qalır.

### 5. Superseded examples

- `ADR-0026`-dakı `IsFeatureAllowed(string)` licensing surface-i `ADR-0051` və bu ADR ilə
  əvəz olunur.
- `ADR-0012`-dəki binary allowed/denied licensing nümunəsi bu ADR üçün superseded
  nümunədir; non-disclosing capability boundary prinsipi qüvvədə qalır.
- `ADR-0004`-dəki "ICapabilityLicensing ilk real implementasiya" nümunəsi bu runtime
  mode surface ilə oxunmalıdır.

### 6. Rejected alternatives

- **`IsFeatureAllowed(string)` saxlanılsın:** rədd edildi — read-only expired mode
  binary feature check-lərlə aydın və stabil ifadə olunmur.
- **Plugin raw license detail görsün:** rədd edildi — licensing boundary-ni pozur.
- **Read-only yalnız UI disable ilə tətbiq olunsun:** rədd edildi — host enforcement
  fail-closed olmalıdır.

## Consequences

- Plugin-facing licensing surface public contract change-dir.
- `04_RESULT_VS_EXCEPTION_POLICY.md` licensing reason code-ları əlavə edir.
- `FR-017`, `NFR-016` və `AC-021` read-only expired enforcement-i test edilə bilən
  formada bağlayır.
- Existing capability boundary ADR-ləri qüvvədə qalır, amma licensing surface nümunələri
  bu ADR-ə istinad etməlidir.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 41 `Plugin Capability Boundary`, Row 42/43 licensing, Cədvəl 3 `Capability Surface Runtime Mode`
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md` — licensing well-known reasons
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-017`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-016`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-021`
- `docs/specs/license-import.md`
- `docs/adr/ADR-0004-capability-enforcement-model.md` — typed policy pattern
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — non-disclosing capability boundary
- `docs/adr/ADR-0051-license-lifecycle-v2.md` — lifecycle state machine (superseded by `ADR-0060`; runtime-mode mapping unchanged)
