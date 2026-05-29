# ADR-0012: Plugin Capability Boundary

**Status:** Accepted
**Date:** 2026-04-24

**Licensing surface note:** Bu ADR-in capability boundary və non-disclosing prinsipi
qüvvədə qalır. Licensing üçün binary `allowed / denied` nümunəsi `ADR-0053`
runtime mode surface-i ilə əvəz olunub.

## Context

`02_ARCHITECTURE_MAP.md` Row 41 (`Plugin Capability Boundary`) və Cədvəl 3 `Plugin Capability Boundary` sətri plugin-ə hansı host API surface görünür sualını Phase 1 blocker kimi göstərir. Engineering convention `Capability Boundary vs Sandbox` ikisinin fərqini açıq yazır: `Capability Boundary nəyin görünəcəyini müəyyən edir; Sandbox isə görünənin necə istifadə ediləcəyini enforce edir`.

`01_PRODUCT_SCOPE.md` licensing-in plugin-ə raw license detail verməməsini və yalnız non-disclosing runtime mode göstərməsini tələb edir. Bu həm licensing, həm də digər host capability-lər üçün `non-disclosing surface` prinsipini qoyur.

Bu ADR plugin capability boundary-ni rəsmiləşdirir.

## Decision

Phase 1 plugin capability boundary modeli:

### 1. Capability = explicit plugin-visible host surface

- Capability host-un plugin-ə verdiyi dar və explicit API surface-dir.
- Plugin-visible capability-lər yalnız `Dobby.Contracts` və ya `Dobby.SDK` altında elan olunur; host implementation project-ləri capability surface sayılmır.
- Plugin manifest-də capability ehtiyacı `ADR-0036` `capabilities = { name, scope }` modeli ilə elan olunur.

### 2. Capability access host-controlled olur

- Plugin capability access-i host tərəfindən verilir və yoxlanılır; plugin özbaşına service discovery və ya internal host service resolution etmir.
- `IServiceProvider`, container və internal runtime service-lər plugin-ə açıq verilmir.
- Plugin elan etmədiyi və ya host-un dəstəkləmədiyi capability-yə birbaşa çatmır; activation-time və runtime enforcement host tərəfində qalır.

### 3. Non-disclosing principle

- Capability surface plugin-ə yalnız minimum zəruri məlumatı qaytarır.
- Licensing capability bu qaydanın canonical nümunəsidir: plugin yalnız `ADR-0053` runtime mode surface-i görür; raw license artifact, expiry, grace, customer/device/package binding detail-i görmür.
- Eyni prinsip storage, settings və digər host-managed capability-lərə də tətbiq olunur: backend implementation detail plugin-facing surface-ə çıxmır.

### 4. Scope və enforcement ayrılığı

- Capability `scope` manifest üzərindən elan olunur və yalnız host-defined closed vocabulary qəbul edir (`shell`, `plugin-page`, `background-task`).
- Capability Boundary nəyin görünəcəyini müəyyən edir.
- Sandbox / policy enforcement isə görünən capability-nin necə istifadə olunacağını məhdudlaşdırır.
- Bu iki mexanizm ayrı öhdəlikdir və biri digərinin əvəzi deyil.

### 5. Phase 1 minimum capability nəticəsi

Phase 1-də canonical minimum davranış budur:

- plugin storage access `IPluginStorage` contract-ı ilə gedir;
- licensing decision non-disclosing runtime mode surface-i ilə gedir (`ADR-0053`);
- plugin-to-plugin və plugin-to-host əlaqəsi yalnız public contract, event və declared extension point-lərlə gedir;
- plugin page/screen hosting yalnız official `Dobby.Contracts` / `Dobby.SDK`
  page/extension contract-ları ilə gedir; plugin host-un daxili WPF, shell view
  model və ya implementation service-lərinə birbaşa toxunmur;
- yeni capability əlavəsi deliberate contract change sayılır və ayrıca ADR amendment tələb edir.

### 6. Runtime fault isolation

Capability boundary həm də runtime fault-un hard stop sərhədidir. Plugin screen,
task və ya lifecycle hook unhandled fault verərsə:

- host process çökməməlidir;
- fault həmin plugin instance/screen/task sərhədində isolate edilir;
- user-facing mesaj non-technical qalır;
- raw exception detail yalnız diagnostics-də, lifecycle/security baxımından
  lazımdırsa audit-də görünür;
- digər müstəqil plugin-lərin runtime və shell işləməyə davam edir.

### 7. Rejected alternatives

- **`IServiceProvider` açıq plugin-ə verilsin**: rədd edildi — plugin host-un bütün DI container-ini görür və istənilən service-ə çata bilər; capability boundary pozulur.
- **Dictionary-based və ya string-only capability API**: rədd edildi — explicit contract surface discoverability və boundary clarity verir.
- **Plugin-ə raw internal service vermək**: rədd edildi — capability boundary-ni mənasız edir.
- **Plugin WPF control-larını host shell-ə birbaşa yerləşdirsin**: rədd edildi —
  plugin host UI implementation detail-inə bağlanır və sandbox/capability boundary
  zəifləyir.

## Consequences

- Plugin host internal services-ə birbaşa çıxmır; capability access həmişə host-controlled boundary-dən keçir.
- Licensing capability non-disclosing olduğu üçün plugin yalnız runtime mode alır; plugin license detail UX-i yaza bilmir.
- Plugin UI Main Content Area-da host tərəfindən təqdim olunan contract-larla
  göstərilir; WPF/internal shell tipləri plugin-facing contract-a çevrilmir.
- Runtime fault isolation plugin failure-ını host process failure-ına çevirmir.
- Yeni capability əlavəsi public contract dəyişikliyi sayılır və ayrıca review/ADR tələb edir.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — plugin-level licensing non-disclosing prinsipi
- `docs/02_ARCHITECTURE_MAP.md` — Row 41 `Plugin Capability Boundary`, Engineering Conventions `Capability Boundary vs Sandbox`, Cədvəl 3 `Plugin Capability Boundary` risk sətri
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — Contracts surface stability
- `docs/adr/ADR-0010-plugin-communication-policy.md` — icazəli kanallar
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md` — `IPluginStorage` capability
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — `capabilities` schema
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — licensing runtime mode surface
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-026`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-015`, `AC-032`
- `docs/specs/plugins-manager.md`
