# ADR-0021: SDK Surface Area

**Status:** Accepted
**Date:** 2026-04-30

## Context

`Dobby.SDK` plugin authors üçün convenience qatıdır, amma SDK şişərsə plugin-lər
host implementation detail-lərinə bağlana bilər. Architecture Map Cədvəl 3 SDK
surface-inin bağlanmasını tələb edirdi.

## Decision

### 1. SDK-nın təbiəti

`Dobby.SDK` convenience və authoring helper qatıdır; runtime implementation bypass
yolu deyil. Plugin-visible stable contract-lar `Dobby.Contracts`-da qalır. SDK
yalnız həmin contract-ları daha rahat istifadə etmək üçün helper/builder/adapter
surface təqdim edə bilər.

### 2. Dependency boundary

`Dobby.SDK` yalnız aşağıdakı project-lərə reference saxlaya bilər:

- `Dobby.Core`
- `Dobby.Contracts`
- `Dobby.Localization`

`Dobby.SDK` `Dobby.UI`, `Dobby.Shell`, `Dobby.Infrastructure`, `Dobby.Security`,
`Dobby.PluginFramework`, `Dobby.Licensing` və host implementation project-lərinə
reference saxlamır.

### 3. P1 allowed SDK surface

Phase 1 SDK surface aşağıdakı kateqoriyalarla məhdudlaşır:

- plugin module registration helper-ləri;
- manifest/extension descriptor builder-ləri;
- typed integration event publish/subscribe helper-ləri;
- Result/error ergonomics helper-ləri;
- storage contract convenience wrapper-ləri;
- localization key/resource helper-ləri;
- route/extension descriptor helper-ləri;
- background operation progress helper-ləri `Dobby.Contracts` operation/progress
  contract-larına adaptasiya kimi.

SDK yeni capability, storage backend, logging channel, telemetry channel, raw
license surface, WPF view type və ya host service locator təqdim etmir.

Storage convenience wrapper-ləri yalnız `Dobby.Contracts.PluginStorage`
contract-larını və `Result<T>`/`Result` semantics-ini ergonomik şəkildə wrap edir.
Onlar yeni backing store, yeni capability, host service locator, plugin-owned
connection/path access və ya `PluginBase` üzərində built-in Storage property-si
yaratmır.

### 3a. P1 `PluginBase` SDK shape

Architecture Map Row 81-dəki `PluginBase : IPluginModule` helper-i Phase 1-də
aşağıdakı minimal shape-i daşıyır:

`PluginBase` class-ının canonical namespace-i `Dobby.SDK.PluginFramework`-dür.

```csharp
public abstract class PluginBase : IPluginModule {
    public abstract void RegisterServices(IPluginServiceCollection services);
    public virtual void OnActivated(IPluginActivationContext context) { }
}
```

- `RegisterServices(...)` abstract və mandatory-dir; plugin author service və
  extension-point registration intent-ini açıq göstərməlidir.
- `OnActivated(...)` virtual no-op və optional-dır; post-load activation işi
  olmayan plugin boş override yazmağa məcbur edilmir.
- `PluginBase` Phase 1-də disable, uninstall və ya deactivate hook-u əlavə
  etmir; bu sərhəd `ADR-0006` lifecycle qərarına tabedir.
- `PluginBase` built-in Logger, Storage, EventBus convenience property-si,
  host service locator və ya yeni logging/telemetry channel təqdim etmir.
  Plugin-lər logging və digər services-ləri approved Contracts/SDK surface və
  normal DI pattern-ləri ilə alır.

### 4. Versioning

SDK breaking change plugin-facing breaking change-dir və Contracts review ilə
birlikdə idarə olunur. SDK helper əlavə etmək mümkündür, amma helper existing
contract semantic-lərini dəyişdirə bilməz. Deprecated helper ən azı bir release
cycle saxlanılmadan silinmir.

### 5. Sample plugin rule

Sample plugin yalnız `Dobby.Contracts` və `Dobby.SDK` public surface-lərinə
reference saxlamalıdır. Sample plugin implementation host internals-i istifadə
edirsə, AC-036 evidence etibarsız sayılır.

## Consequences

- Plugin authoring rahatlığı host internals leakage yaratmır.
- Static dependency scan AC-043 üçün qapalı reference list əldə edir.
- SDK skeleton platform-neutral `net10.0` qala bilər; WPF-specific surface SDK-ya
  daşınmır.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `Dobby.SDK`, plugin-facing surface qaydaları
- `docs/03_TECH_STACK_AND_VERSIONS.md` — Target Framework split
- `docs/adr/ADR-0001-contracts-dependency-boundary.md`
- `docs/adr/ADR-0010-plugin-communication-policy.md`
- `docs/specs/sample-plugin.md`
