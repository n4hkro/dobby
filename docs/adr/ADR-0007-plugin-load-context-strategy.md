# ADR-0007: Plugin Load Context Strategy

**Status:** Accepted (revised 2026-04-28; trust-boundary wording clarified by `ADR-0059`, and the § Consequences out-of-process isolation framing corrected 2026-08-31 — see ## Revision History)
**Date:** 2026-04-24

**Clarified by:** `ADR-0059` — an `AssemblyLoadContext` is dependency isolation, never a
containment or security boundary; the trust-posture consequences of that wording live in
that document. The decision in this ADR is unchanged.

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
    // Shared decision is name-based, not marker-type based (§2).
    private static readonly HashSet<string> SharedAssemblyNames =
        new(StringComparer.OrdinalIgnoreCase) {
            "Dobby.Contracts", "Dobby.Core", "Dobby.SDK", "System.Text.Json"
        };

    private readonly AssemblyDependencyResolver resolver;

    public PluginLoadContext(string pluginId, string entryAssemblyPath)
        : base($"Dobby.Plugin:{pluginId}", isCollectible: false) { /* ... */ }
    // ...
}
```

ALC-in adı `Dobby.Plugin:<pluginId>` formasındadır ki, diagnostics və crash dump-da hansı plugin
context-inin yükləndiyi birmənalı görünsün.

### 2. Shared dependency resolution

Plugin ALC `Load(AssemblyName)` override-da **shared** sayılan assembly-ləri host (`Default`) ALC-dən
resolve edir (plugin öz copy-sini package-də gətirsə belə həmin copy load olunmur). Shared qərarı
yalnız **simple assembly name** üzərində verilir və iki hissədən ibarətdir:

**(a) Exact-name shared set:**

- `Dobby.Contracts`
- `Dobby.Core`
- `Dobby.SDK`
- `System.Text.Json`

**(b) Prefix rule** — adı `System.` və ya `Microsoft.Extensions.` ilə başlayan **bütün**
assembly-lər:

- BCL (`System.*`)
- `Microsoft.Extensions.*`

Prefix rule enumerated list deyil: host-un reference etmədiyi `System.*` /
`Microsoft.Extensions.*` assembly-si də shared sayılır. Belə ad host Default ALC-də tapılmırsa
resolution **fail-closed** olur (plugin-local copy-ya fallback edilmir) və plugin load failure kimi
`ADR-0008` activation nəticəsinə çevrilir; plugin package həmin faylı daşısa da nəticə dəyişmir.
Bu qəsdən seçilmiş davranışdır: host-pinned serialization/DI/logging surface-inin iki fərqli
versiyasının eyni prosesdə yaşaması type identity split-i yaradar.

Shared resolution **version enforcement etmir**: host-daki copy tələb olunan versiyadan asılı
olmayaraq qazanır. Ona görə host↔plugin compatibility ALC-nin işi deyil — o, load-dan **əvvəl**
manifest `hostApiVersion` / `minHostVersion` və `ADR-0037` compatibility gate-i ilə qurulur (§5).
Gate keçildikdən sonra qalan uyğunsuzluq (məs., plugin host-da olmayan yeni Contracts member-inə
müraciət edir) plugin boundary-sində `TypeLoadException` / `MissingMethodException` kimi görünür və
`NFR-008` fault isolation qaydası ilə həmin plugin-in sərhədində saxlanılır — host prosesi
çökmür.

Host shared dependencies host tərəfindən pin olunur. Plugin-host compatibility
manifest root `minHostVersion` field-i və `ADR-0037` compatibility check-i ilə
yoxlanılır; Phase 1 manifest ayrıca `Dobby.Contracts`, `Dobby.SDK`,
`Dobby.Core`, BCL və ya `Microsoft.Extensions.*` üçün per-dependency
`minVersion` / `maxVersion` field-ləri daşımır.

Plugin öz özəl dependency-lərini (third-party library-lər) **plugin ALC-də** saxlayır və plugin A
v1.2.3 ilə plugin B v2.0.0 eyni library-nin fərqli versiyalarından istifadə edə bilər. Bu
side-by-side imkanı **yalnız shared olmayan adlar üçündür**: (a) set-indəki ad və `System.` /
`Microsoft.Extensions.` prefix-i daşıyan istənilən library per-plugin versiyalana bilməz. Plugin
author-ın host-pinned surface-dən fərqli versiya tələb edən dependency-si varsa, ya host pin-i
`docs/03_TECH_STACK_AND_VERSIONS.md` + `ADR-0037` compatibility qərarı ilə yenilənir, ya da
dependency prefix daşımayan öz distribution adı ilə gətirilir; "plugin öz copy-sini qoyur" həlli
işləmir.

### 3. ALC cross-boundary type sharing

Plugin və host eyni type identity-ni yalnız **shared dependency-lər üzərindən** görür. Plugin ALC-də yüklənmiş `PluginFoo` class host-a yalnız onun interface-i (`Dobby.Contracts`-dan gələn) üzərindən görünür; concrete type host tərəfdə cast olunmur.

### 4. Native dependency isolation

Plugin native binary-si (məs., P/Invoke target `.dll`) `LoadUnmanagedDll` override-ında
`AssemblyDependencyResolver.ResolveUnmanagedDllToPath` ilə **plugin runtime root-undan** resolve
olunur. Ad resolver tərəfindən həll olunmursa override `nint.Zero` qaytarır və resolution runtime-ın
default probing-inə düşür; yəni host/OS-owned native library-lər (system32 və host application
directory) shared qalır və plugin onları öz copy-si ilə əvəz edə bilmir.

Plugin ALC-nin native probing-i **yalnız signature-bound package content-i** üzərində işləyir və
bunu üç qayda birlikdə təmin edir:

- hər native library manifest `nativeLibraries` array-ində elan olunmalıdır və həmin path manifest
  `files` map-ında mövcud olmalıdır — yoxsa package `invalid-manifest` ilə rədd olunur
  (`ADR-0059` manifest v2, `ADR-0036`);
- `files` map-ı signature-bound canonical manifest-in bir hissəsidir, ona görə hər native byte
  `SHA-256` ilə örtülür (`FR-014`, `ADR-0045`);
- package content-i `files` map-ı ilə **iki tərəfli** bağlıdır: map-da olmayan zip entry
  `forbidden-content`, package-da olmayan map sətri isə eyni şəkildə rədd olunur; case-only
  fərqlənən path cütü də rədd olunur (`ADR-0045`, `docs/specs/plugins-manager.md`
  § install validation reason-ları).

Nəticə: plugin runtime root-una elan olunmamış, hash-lanmamış native DLL qoyulub sonra
`LoadUnmanagedDll` tərəfindən götürülməsi (DLL planting) install validation səviyyəsində bağlıdır.
Bu **fully trusted in-process kod üçün containment iddiası deyil** (`ADR-0059`): verified plugin
öz ALC-sindən native kod icra edir və native kod prosesin bütün imkanlarına sahibdir. Qayda
artifact integrity qaydasıdır, sandbox deyil.

### 5. Load ordering vs trust gates (normativ)

ALC yaradılması və plugin entry assembly-nin yüklənməsi **bütün pre-load gate-lərdən sonra** baş
verməlidir. Normativ sıra:

1. installed package-in signature/trust və package validation-ı (`ADR-0032`, `ADR-0045`,
   `ADR-0059`);
2. manifest compatibility qiymətləndirməsi (`hostApiVersion`, `minHostVersion`, `ADR-0037`);
3. license runtime mode qiymətləndirməsi; `Denied` activation-ı dayandırır (`ADR-0060`,
   `ADR-0053`);
4. manifest metadata-dan operation target seeding (`ADR-0048`);
5. **yalnız bundan sonra** `PluginLoadContext` yaradılır və entry assembly load olunur.

Bu sıra qərarın security-relevant hissəsidir: assembly load-u module type discovery və static
initializer icrası demək olduğu üçün gate-lərdən əvvəl load etmək **verify olunmamış kodun icrası**
olardı. Entry assembly path-i plugin runtime root-una `Path.GetFullPath` normalizasiyasından sonra
containment yoxlaması ilə məhdudlaşdırılır; root-dan kənara çıxan və ya mövcud olmayan path
`NotFound` ilə rədd olunur, ALC yaradılmır (traversal/symlink-substitution guard).

Entry assembly-də parameter-siz public constructor-a malik `IPluginModule` implementation-ı
**tam olaraq bir** olmalıdır; sıfır və ya birdən çox tapılırsa nəticə `ValidationFailed` olur və
plugin activate edilmir (ambiguous module seçimi host-un təxminini tələb etməməlidir).

### 6. Rejected alternatives

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
- Plugin manifest (`ADR-0036`) host compatibility üçün root `minHostVersion` daşıyır; compatibility check (`ADR-0037`) activation zamanı bu dəyəri host version ilə yoxlayır və bu yoxlama assembly load-dan **əvvəl** baş verir (§5)
- Plugin-scoped side-by-side versioning `System.*` / `Microsoft.Extensions.*` prefix-li və exact-name shared set-dəki library-lər üçün mövcud deyil; plugin template və author dokumentasiyası bu məhdudiyyəti explicit göstərməlidir (§2)
- Native library-lər manifest `nativeLibraries` elanı + `files` hash coverage tələb edir; undeclared native binary install validation-da rədd olunur (§4)
- Plugin disable zamanı assembly memory-də qalır (Phase 1 məhdudiyyəti); unload dəstəyi P2 amendment
- Memory usage göstəricisi plugin sayı ilə düzxətli artır — NFR-003 startup ölçmələri bu patterni qeyd edir
- Plugin author developer experience: plugin template plugin-scoped NuGet dependency-ləri shared-dən ayrı saxlayır
- ALC malicious-code sandbox deyil və heç bir trust səviyyəsində containment sayılmır;
  bu, ALC-nin konfiqurasiyası ilə düzəldilə bilən çatışmazlıq deyil, texnologiyanın
  xassəsidir
- Fully trusted olmayan paket host prosesində icra edilmir. Arbitrary/user-installable,
  untrusted və ya semi-trusted plugin ecosystem-i dəstəkləmək ayrıca approve olunmuş
  out-of-process worker tələb edir: OS-enforced identity, IPC, resource və file/network
  boundary-ləri ilə (`ADR-0059` § `Trust posture`). Bu, sonraya qoyulan re-evaluation
  trigger-i deyil, həmin vendor sinfini dəstəkləməyin ilkin şərtidir -- ecosystem-i əvvəl
  açıb isolation-ı sonra qiymətləndirmək yolu mövcud deyil
- Fully trusted vendor-signed model daxilində qalan runtime fault isolation, native
  dependency/process permission boundary və customer process-isolation policy tələbləri
  P2 mövzusu olaraq qalır və bu ADR-in future criteria-ları ilə birlikdə qiymətləndirilir;
  onların yoxluğu yuxarıdaki ilkin şərti yumşaltmır

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` Row 31 `Assembly Isolation / Load Context Management`
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — `minHostVersion` manifest surface-i
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md` — activation zamanı compatibility yoxlaması
- `docs/adr/ADR-0006-plugin-di-registration.md` — plugin DI scope ALC ilə birgə idarə olunur
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — host shared assembly pin list
- `docs/adr/ADR-0032-plugin-signature-trust-model.md` — vendor-signed plugin trust boundary
- `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md` — signed package artifact boundary, `files` map coverage
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — manifest v2 `nativeLibraries`, full-trust in-process model
- `docs/adr/ADR-0008-deferred-plugin-activation.md` — load failure-nin activation outcome-a çevrilməsi
- `docs/specs/plugins-manager.md` — install validation reason-ları və activation outcome surface-i
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-008` plugin fault isolation
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-005`, `AC-015`, `AC-032`, `AC-036`

## Revision History

- **2026-08-31:** `ADR-0059` clarification-ının reverse edge-i elan olundu və § Consequences
  daxilindəki retired isolation framing-i düzəldildi. `**Status:**` sətri artıq
  trust-boundary wording-in clarify olunduğunu bildirirdi, lakin `ADR-0059` heç bir
  `**Clarifies:**` header-i daşımırdı, yəni relation yalnız bir istiqamətdə mövcud idi
  (`docs/adr/README.md` § `Supersession header formatı` hər iki istiqaməti tələb edir;
  reverse edge olmadan `ADR-0059`-u oxuyan reader onun köhnə qərarı dəqiqləşdirdiyini
  bilmir). § Consequences əvvəl "untrusted plugin ecosystem açılarsa isolation modeli
  yenidən qiymətləndirilir" və "out-of-process plugin worker P2/later path kimi qalır"
  deyirdi -- out-of-process isolation-ı sonraya qoyulan trigger kimi göstərirdi.
  `ADR-0059` § `Trust posture` isə fully trusted olmayan paketin host prosesində icrasını
  qadağan edir və həmin worker-i ilkin şərt edir; bölməni oxuyan reader bu iki ifadədən
  "əvvəl aç, sonra qiymətləndir" nəticəsi çıxara bilərdi. §§ 4-5-dəki "sandbox deyil"
  qaydası artıq düzgün idi və dəyişmir; ALC strategiyası, load ordering, shared resolution,
  native library qaydaları və trust gate sırası da dəyişmir. Yeni öhdəlik əlavə edilmir:
  `ADR-0059`-un mövcud qərarı dəqiq ifadə olunur, ona görə bu clarification record-dur,
  successor ADR deyil.

- **2026-08-27:** §1 code block-u, §2 shared resolution qaydası və §4 native isolation-ı shipped
  `src/Dobby.PluginFramework/Activation/PluginLoadContext.cs` ilə uzlaşdırıldı; yeni §5
  (load ordering vs trust gates) əlavə olundu və `Rejected alternatives` §6-ya keçdi.
  Düzəldilən qüsurlar:
  (a) §1 blok-u mövcud olmayan `Dobby.Contracts.SomeMarker` / `Dobby.SDK.SomeMarker` marker
  type-ları üzərində `Assembly[] _sharedFromHost` pinning göstərirdi; shipped qərar **name-based**
  (`HashSet<string>` + prefix) qaydadır. Marker-type variantını hərfi implement edən oxucu
  `System.*` / `Microsoft.Extensions.*` prefix qaydasını tamamilə buraxardı.
  (b) §2 shared list-i beş bullet kimi enumerated göstərirdi; real qayda `System.` və
  `Microsoft.Extensions.` prefix-li **hər** adı, o cümlədən host-un reference etmədiyi adları
  əhatə edir və belə halda fail-closed olur. Eyni zamanda §2-nin "plugin A və plugin B eyni
  library-nin fərqli versiyalarından rahat istifadə edə bilər" cümləsi qeyd-şərtsiz idi. Bu
  ikisi birlikdə konkret plugin author səhvinə aparırdı: `Microsoft.Extensions.*` və ya
  `System.Text.Json`-un fərqli versiyasını package-ə qoyub side-by-side işləyəcəyini gözləmək —
  halbuki host copy-si qazanır, host-da yoxsa load ümumiyyətlə alınmır. Side-by-side vədi indi
  shared olmayan adlarla məhdudlaşdırılıb və pin dəyişdirmə yolu göstərilib. Həmçinin shared
  resolution-un version enforcement etmədiyi və qalan uyğunsuzluğun plugin boundary-də
  `TypeLoadException`/`MissingMethodException` kimi görünüb `NFR-008` ilə isolate olunduğu
  explicit yazıldı.
  (c) §4 manifest v2 `nativeLibraries` elanından və package content-in `files` map-ı ilə iki
  tərəfli bağlanmasından heç nə demirdi; ona görə "native library plugin root-dan resolve olunur"
  cümləsi undeclared/unhashed native DLL-in də götürülə biləcəyi kimi oxuna bilirdi (DLL planting).
  İndi elan + hash coverage + exhaustive package binding zənciri yazılıb, eyni zamanda bunun
  artifact integrity qaydası olduğu, `ADR-0059` mənasında containment olmadığı qeyd edilib.
  (d) ADR heç yerdə ALC-nin **nə vaxt** yaradıldığını bağlamırdı. Shipped activation sırası
  (`src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs`) trust/package validation →
  compatibility → license mode → operation target seeding → `PluginLoadContext` şəklindədir.
  Sıra normativ yazılmadığı üçün implementor manifest attribute-larını oxumaq məqsədilə
  assembly-ni gate-lərdən əvvəl yükləyə bilərdi; assembly load static initializer icrası demək
  olduğu üçün bu verify olunmamış kodun icrası olardı. §5 sıranı, entry path containment
  yoxlamasını və "tam bir `IPluginModule` implementation-ı" qaydasını normativ edir.
  Qərar dəyişmir (hər plugin öz `isCollectible: false` ALC-sində, shared dependency-lər host-dan
  pinned) — bu factual reconciliation və failure-path precision işidir, ona görə successor ADR
  yaradılmır. §2 prefix qaydası və onun fail-closed davranışı üçün behavior evidence-i
  `tests/Dobby.UnitTests/PluginFramework/Activation/PluginActivationCoreTests.cs` altında əlavə
  edildi: plugin package-i `System.Text.Json` və host-da olmayan `System.`-prefiksli adı öz
  `deps.json`-u ilə resolve oluna bilən şəkildə daşısa da, birinci host instance-ını qaytarır,
  ikincisi `FileNotFoundException` ilə fail-closed olur, prefix daşımayan plugin-private ad isə
  plugin qovluğundan yüklənir (`NFR-027`: `SharedAssemblyNames` field-ini oxumaq sübut sayılmır).

- **2026-04-28:** Phase 1 ALC qərarının vendor-signed plugin trust modelinə bağlı
  olduğu dəqiqləşdirildi; out-of-process plugin worker P1 üçün rejected alternative
  kimi yazıldı və P2/later reassessment trigger-ləri əlavə olundu.
