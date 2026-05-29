# ADR-0007: Plugin Load Context Strategy

**Status:** Accepted (revised 2026-04-28)
**Date:** 2026-04-24

## Context

`02_ARCHITECTURE_MAP.md` Row 31 `Assembly Isolation / Load Context Management` Phase 1-də P1-core statusda idi. Plugin-lər müstəqil vendor-lardan gəlir və fərqli library versiyalarından asılı ola bilər; naive "hər şeyi default `AssemblyLoadContext`-ə yüklə" strategiyası DLL hell, version conflict və security risklərinə səbəb olur.

Dobby Phase 1 arbitrary third-party plugin install modeli deyil. Plugin package-lər
vendor-created və vendor-signed `.dobbyplugin` artifact-lardır; install və
activation signature/trust, manifest/package validation, compatibility, license
və capability boundary-lərindən keçir. Buna görə Phase 1 isolation qərarı
malicious untrusted marketplace plugin sandbox-u kimi yox, trusted-but-buggy
vendor plugin-lər üçün dependency/fault boundary kimi qiymətləndirilir.

Açıq suallar:
- Plugin assembly-ləri hansı `AssemblyLoadContext` (ALC) içində yüklənməlidir?
- Host və plugin arasında shared dependency-lər (məs., `Dobby.Contracts`, `Dobby.SDK`, BCL) necə resolve edilməlidir?
- Plugin disable/unload zamanı ALC təmizlənməsi mümkündürmü?
- Plugin A v1.2.3 və plugin B v2.0.0 eyni third-party library-nin iki fərqli versiyasından istifadə edə bilər — bu hal necə idarə olunur?

## Decision

### 1. Hər plugin öz `AssemblyLoadContext`-ində

Hər plugin ayrıca `AssemblyLoadContext` (ALC) içində yüklənir. Plugin ALC `isCollectible: false` ilə yaradılır Phase 1-də (unload dəstəklənmir; plugin disable yalnız instance dispose edir, assembly həmişə memory-də qalır — bu sadəliyi və stability-ni qoruyur).

Because Phase 1 ALCs are `isCollectible: false`, an activated plugin assembly remains loaded until process exit. Uninstall of an activated plugin must disable the plugin, remove it from active navigation/activation state, mark runtime files as pending deletion, and require app restart before physical DLL deletion is considered complete.

```csharp
public sealed class PluginLoadContext : AssemblyLoadContext {
    private readonly string _pluginRoot;
    private readonly AssemblyDependencyResolver _resolver;
    // Host shared dependencies explicitly pinned
    private static readonly Assembly[] _sharedFromHost = new[] {
        typeof(Dobby.Contracts.SomeMarker).Assembly,
        typeof(Dobby.SDK.SomeMarker).Assembly,
    };
    // ...
}
```

### 2. Shared dependency resolution

Plugin ALC `Load(AssemblyName)` override-da aşağıdakı dependency-ləri **host ALC-dən resolve edir** (plugin öz copy-sini gətirsə belə ignore olunur):

- `Dobby.Contracts` (pinned version)
- `Dobby.SDK` (pinned version)
- `Dobby.Core` (transitive Contracts üzərindən; yalnız ADR-0001 closed list surface)
- BCL və `Microsoft.Extensions.*` (host-pinned version)
- `System.Text.Json` (serialization compatibility üçün host-pinned)

Host shared dependencies host tərəfindən pin olunur. Plugin-host compatibility
manifest root `minHostVersion` field-i və `ADR-0037` compatibility check-i ilə
yoxlanılır; Phase 1 manifest ayrıca `Dobby.Contracts`, `Dobby.SDK`,
`Dobby.Core`, BCL və ya `Microsoft.Extensions.*` üçün per-dependency
`minVersion` / `maxVersion` field-ləri daşımır.

Plugin öz özəl dependency-lərini (third-party library-lər) **plugin ALC-də** saxlayır; plugin A v1.2.3 və plugin B v2.0.0 eyni library-nin fərqli versiyalarından rahat istifadə edə bilər.

### 3. ALC cross-boundary type sharing

Plugin və host eyni type identity-ni yalnız **shared dependency-lər üzərindən** görür. Plugin ALC-də yüklənmiş `PluginFoo` class host-a yalnız onun interface-i (`Dobby.Contracts`-dan gələn) üzərindən görünür; concrete type host tərəfdə cast olunmur.

### 4. Native dependency isolation

Plugin native binary (məs., `.dll` P/Invoke target) `AssemblyDependencyResolver.ResolveUnmanagedDllToPath` ilə plugin root-dan resolve edir; host system32-dən gələn native library-lər isə həmişə shared qalır.

### 5. Rejected alternatives

- **Bütün plugin-lər default ALC-də:** rədd edildi — DLL hell və version conflict birbaşa riski var
- **`isCollectible: true` ALC (unload dəstəyi):** Phase 1-də rədd edildi — memory leak-ə qarşı ehtiyatla istifadə edilməlidir, test və diagnostic burden yaradır; P2+ release-də yenidən qiymətləndirilir
- **Out-of-process plugin worker:** Phase 1-də rədd edildi — process boundary
  daha sərt isolation verir, amma WPF UI hosting, dispatcher, IPC contract,
  lifecycle, diagnostics və debugging səthini böyüdür. Dobby P1 vendor-signed
  plugin trust modelinə söykəndiyi üçün ALC + package/signature/license/capability
  enforcement daha uyğun Phase 1 balansıdır.
- **Plugin öz `Dobby.Contracts` copy-ini istifadə etsin:** rədd edildi — host-plugin interface identity itir, type cast işləməz
- **Gac / shared library path:** rədd edildi — portable MSIX deployment ilə uyğunsuzdur

## Consequences

- `Dobby.PluginFramework / Assembly Isolation / Load Context Management` (Row 31) `PluginLoadContext` concrete implementation daşıyır
- Plugin manifest (`ADR-0036`) host compatibility üçün root `minHostVersion` daşıyır; compatibility check (`ADR-0037`) activation zamanı bu dəyəri host version ilə yoxlayır
- Plugin disable zamanı assembly memory-də qalır (Phase 1 məhdudiyyəti); unload dəstəyi P2 amendment
- Memory usage göstəricisi plugin sayı ilə düzxətli artır — NFR-003 startup ölçmələri bu patterni qeyd edir
- Plugin author developer experience: plugin template plugin-scoped NuGet dependency-ləri shared-dən ayrı saxlayır
- ALC malicious-code sandbox deyil; arbitrary/user-installable və ya untrusted
  plugin ecosystem açılarsa isolation modeli yenidən qiymətləndirilir.
- Out-of-process plugin worker P2/later path kimi qalır və yalnız trust model
  dəyişəndə, runtime fault isolation ALC ilə acceptance-ləri ödəməyəndə, native
  dependency/process permission boundary tələb olunanda və ya customer security
  policy process isolation tələb edəndə yenidən açılır.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` Row 31 `Assembly Isolation / Load Context Management`
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — `minHostVersion` manifest surface-i
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md` — activation zamanı compatibility yoxlaması
- `docs/adr/ADR-0006-plugin-di-registration.md` — plugin DI scope ALC ilə birgə idarə olunur
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — host shared assembly pin list
- `docs/adr/ADR-0032-plugin-signature-trust-model.md` — vendor-signed plugin trust boundary
- `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md` — signed package artifact boundary
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-005`, `AC-015`, `AC-032`, `AC-036`

## Revision History

- **2026-04-28:** Phase 1 ALC qərarının vendor-signed plugin trust modelinə bağlı
  olduğu dəqiqləşdirildi; out-of-process plugin worker P1 üçün rejected alternative
  kimi yazıldı və P2/later reassessment trigger-ləri əlavə olundu.
