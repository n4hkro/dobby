# Dobby Stack Selection Design

**Tarix:** 2026-04-21
**Status:** Təsdiqlənib
**Scope:** Runtime, UI framework, infrastructure packages, build tooling, test stack, distribution model

---

## Constraint-lər

| Constraint | Dəyər |
|---|---|
| Target OS | Yalnız Windows |
| Məhsul tipi | Enterprise B2B |
| Komanda | Solo developer + AI pair-programming |
| UI komponentlər | Open-source |
| Lisenziya prinsipi | Yalnız pulsuz / open-source — heç bir commercial runtime/build dependency |
| OS baseline | Windows 11 22H2+ primary; Windows 10 yalnız ESU-enrolled 22H2 və ya uyğun Enterprise/IoT LTSC üçün |
| Performance baseline hardware | `ADR-0042` tərəfindən tərif olunan reference spec (i5-10xxx / 16GB DDR4 / SATA SSD / 1920x1080) — release gate P95 ölçmələri (NFR-003, AC-014) üçün kanonik baseline |
| Customer support hardware (minimum) | 4 GB RAM, 500 MB boş disk, x64 prosessor; ARM və x86 dəstəklənmir. End-user dəstək scope-u — release-gate baseline (`ADR-0042`) ilə qarışdırılmır |
| Customer support hardware (recommended) | 8 GB RAM, SSD storage |
| Excluded OS versions | Windows 7, Windows 8 və Windows 8.1 explicit dəstəklənmir; bu platformalarda install/launch cəhdi support scope-dan kənardır |

> **Qeyd:** Windows 10 consumer general support 2025-10-14-də bitib; 22H2 ESU və Enterprise/IoT LTSC ayrı servicing channel-lardır.

---

## 1. Runtime + UI

| Qat | Seçim | Qeyd |
|---|---|---|
| .NET | `net10.0-windows` (.NET 10 LTS) | Windows-specific TFM; WPF tələb edir; .NET 10 aktiv LTS-dir |
| C# | C# 14 | Source generators, required members, primary constructors, field keyword |
| UI Framework | WPF | Ən dərin ekosistem, ən güclü AI pair-programming dəstəyi, sübut olunmuş plugin hosting pattern-ləri |
| Modern theme | NuGet: `WPF-UI`; namespace: `Wpf.Ui` | Windows 11 Fluent Design: Mica, rounded corners, modern controls |
| MVVM | `CommunityToolkit.Mvvm` | Source-generated `ObservableProperty`, `RelayCommand`, `ObservableObject` |

### Seçim əsaslandırması

WPF aşağıdakı səbəblərə görə WinUI 3 və Avalonia-dan üstün tutuldu:

- **Ekosistem dərinliyi.** 18 illik WPF — StackOverflow, NuGet, blog, tutorial ən zəngindir. Solo developer üçün "stuck olma" riski minimaldır.
- **AI effektivliyi.** WPF training data-sı bütün .NET UI framework-larından çoxdur; AI pair-programming WPF-də ən güclüdür.
- **Plugin hosting.** Prism, MEF, custom composition — WPF-də plugin-based shell quran onlarla open-source nümunə var.
- **Open-source UI.** WPF-UI (Fluent Design), MahApps.Metro, MaterialDesignInXaml, HandyControl — solo developer üçün kifayət edən seçim var.
- **Enterprise B2B track record.** WPF enterprise desktop tətbiqlərdə ən çox istifadə olunan .NET UI framework-udur.

WPF-UI Windows 11 Fluent Design stili verir: NavigationView, TitleBar, Snackbar, Dialog — shell composition üçün hazır komponentlər; Dark/Light theme switching built-in; `WPF-UI.DependencyInjection` (NuGet) / `Wpf.Ui.DependencyInjection` (namespace) ilə Generic Host inteqrasiyası.

---

## 2. DI, Hosting & Infrastructure

| Qat | Seçim | Qeyd |
|---|---|---|
| DI | `Microsoft.Extensions.DependencyInjection` | .NET standard DI; plugin registration ADR-0006 ilə uyğunlaşır |
| Host | `Microsoft.Extensions.Hosting` | Generic Host pattern — app lifecycle, DI, config, logging bir yerdə |
| Config | `Microsoft.Extensions.Configuration` | `appsettings.json`, environment variables, user secrets |
| Logging abstraction | `Microsoft.Extensions.Logging` | `ILogger<T>` — bütün project-lər bu abstraction-ı istifadə edir |
| Logging sink | `Serilog` + `Serilog.Extensions.Hosting` | Structured logging → local file, console; default vendor upload/telemetry yoxdur. Gələcək Seq/ELK/OpenTelemetry yalnız explicit opt-in product/security qərarı ilə əlavə oluna bilər |
| Serialization | `System.Text.Json` | BCL built-in, explicit NuGet reference lazım deyil; source generators ilə AOT-friendly |
| Validation | `FluentValidation` | Strongly-typed validation rules; rule-lar dedicated validator class-larında, UI-dan və business logic-dən ayrı saxlanılır və isolated unit-test edilir; `Dobby.Core/Validation` namespace-i buna əsaslanır |
| Resilience | `Microsoft.Extensions.Resilience` | Yalnız opsional internet əsaslı inteqrasiyalar üçün; offline core baseline-a daxil deyil |
| Http Resilience | `Microsoft.Extensions.Http.Resilience` | Yalnız opsional internet əsaslı inteqrasiya üçün HttpClient resilience pipeline-ları |
| Caching | `Microsoft.Extensions.Caching.Memory` | In-memory cache; `Dobby.Application/Caching` namespace-i üçün |
| Storage | SQLite-backed local persistence + `Dapper` | Local persistence: plugin storage, settings, licensing cache, audit. Backing DB faylları data-at-rest qorunmalıdır; exact protection provider `ADR-0054` ilə bağlanır və şifrəsiz DB release üçün qəbul edilmir. |
| Localization | `Microsoft.Extensions.Localization` | `.resx` + `IStringLocalizer<T>`; Phase 1 default culture `az-AZ`; resource skeleton `az-AZ`, `en-US`, `tr-TR`; runtime language switch P2 |
| Options pattern | `Microsoft.Extensions.Options` + `.ConfigurationExtensions` + `.DataAnnotations` | Desktop runtime default `IOptionsMonitor<T>`; one-shot `IOptions<T>`; `IOptionsSnapshot<T>` istifadə olunmur (web request-scope pattern-idir) |
| Plugin packaging | `.zip` (`.dobbyplugin`) + `manifest.json` | BCL: `System.IO.Compression.ZipFile` + `ZipArchive`; `.nupkg` seçilmədi — NuGet semantics lazımsız baggage gətirir |
| Plugin loading | `AssemblyLoadContext` (BCL) | Phase 1 per-plugin assembly/dependency isolation; əlavə NuGet lazım deyil. Out-of-process plugin worker P1 scope deyil və yalnız `ADR-0007` P2/later trigger-ləri ilə yenidən qiymətləndirilir. |
| Plugin versioning | `NuGet.Versioning` | SemVer range parsing; plugin `dependsOn.minVersion` constraint-ləri üçün |
| Plugin signature | `System.Security.Cryptography` (BCL) | Plugin package checksum/signature doğrulaması; əlavə NuGet lazım deyil |
| Secure storage | `System.Security.Cryptography.ProtectedData` + Dobby local storage protection layer | Windows DPAPI token, secret, remember-me, recovery/session material və protected config üçün; DB-at-rest qoruma açarı təkcə `deviceId`-dən törəmir və istifadəçidən əl ilə istənilmir. Protected-storage scope mapping `ADR-0055` ilə bağlanır. |
| Authentication | Device-bound local password-based auth + DPAPI | Offline-first məhsul prinsipi (ADR-0038); MSAL/Entra Phase 1 scope-una daxil deyil |
| Manifest validation | `JsonSchema.Net` (MIT) | Plugin manifest JSON Schema 2020-12 validation |
| HTTP client | `IHttpClientFactory` + typed clients | Yalnız opsional internet əsaslı inteqrasiyalar üçün; core məhsul funksionallığı buna bağlı deyil |
| Messaging (integration) | Dobby-owned `IEventBus` | `Dobby.Messaging` project-i üçün; external MediatR istifadə olunmur |
| Messaging (UI-local) | `CommunityToolkit.Mvvm` içindəki `WeakReferenceMessenger` | Ayrıca NuGet lazım deyil; artıq seçilmiş MVVM toolkit-in bir hissəsidir |
| Authorization | `Microsoft.AspNetCore.Authorization` (MIT) | `Microsoft.AspNetCore.Authorization` internal olaraq reuse oluna bilər, amma plugin-visible authorization contract-ları ADR-0004-dəki Dobby-owned `ICapabilityPolicy<TCapability>` + `PolicyDecision` surface-i olaraq qalır. Host-side evaluator/authorizer service ADR-0004 explicit əlavə etmədikcə implementation-internal sayılır |

### Generic Host pattern

`IHostBuilder` → `IHost` pattern startup, DI registration, config loading, logging hamısını bir pipeline-da birləşdirir. `IHostedService` background task lifecycle üçün istifadə oluna bilər, amma `ADR-0041` qaydasına görə pre-login ağır iş başlatmır; plugin activation, health check, lokal maintenance task-ları və yalnız opsional online capability-lər açılarsa update polling post-login orchestration ilə işləyir. WPF-UI bunu native dəstəkləyir: `WPF-UI.DependencyInjection` (NuGet) → `IServiceCollection.AddWpfUi()`.

---

## 3. Test Stack

| Qat | Seçim | Qeyd |
|---|---|---|
| Test framework | xUnit v2 (`xunit`) | .NET ekosistemində ən geniş yayılmış; `[Fact]`, `[Theory]`, parallel test execution; maksimum tooling sabitliyi üçün v2 default; v3 yalnız ayrıca human-approved toolchain/package decision ilə upgrade candidate ola bilər |
| Assertions | `Shouldly` | Oxunaqlı assertion syntax: `result.ShouldBe(expected)`; tam pulsuz (FluentAssertions v8+ commercial license tələb edir) |
| Mocking | `NSubstitute` | Interface mock üçün; Moq-dan daha təmiz syntax, solo developer üçün daha az boilerplate |
| Snapshot testing | `Verify` | Render/output snapshot müqayisəsi; UI composition ordering test-ləri üçün |
| Integration test host | `Microsoft.Extensions.Hosting` + custom helper | Generic Host-u test mühitində qaldırmaq üçün; `Hosting.Testing` preview olduğu üçün öz test helper yaradılır |

### Test project strukturu

Test project strukturu bu sənədin deyil, `docs/09_BUILD_RUN_AND_RELEASE.md`
sənədinin canonical responsibility-sidir. Phase 1 skeleton Unit / Integration /
Smoke evidence split-inə uyğun test project-ləri yaratmalıdır; per-production
project mirror test rule-u canonical deyil.

Arxitektura xəritəsindəki test strategiyaları ilə uyğunluq:
`contract`, `unit`, `integration`, `snapshot`, `manual`. Contract test-lər
interface uyğunluğunu yoxlayır (implementation-dan asılı deyil). Integration
test-lər Generic Host-u real config ilə qaldırır.

---

## 4. Build Tooling & Project Organization

| Qat | Seçim | Qeyd |
|---|---|---|
| Solution | Tək `Dobby.sln` | Bütün project-lər bir solution-da; solo developer üçün ən sadə |
| SDK pinning | `global.json` | .NET SDK patch versiyası skeleton/toolchain round-da explicit pin edilir; developer/CI SDK drift-i azalır |
| Shared props | `Directory.Build.props` | Nullable, implicit usings, treat warnings as errors — bir yerdə; TFM per-project bölünür (`Target Framework Split`) |
| Package management | `Directory.Packages.props` (Central Package Management) | Bütün NuGet versiyaları tək faylda; version drift yoxdur |
| Code style | `.editorconfig` | Naming conventions, indentation, severity levels — IDE və CI enforce edir |
| Format check | `dotnet format` | CI-da format yoxlaması; local-da save-on-format |
| Dependency upgrades | Deliberate, batched | Package upgrade-lər yalnız `Directory.Packages.props` üzərindən aparılır; ad-hoc tək paket bump license check, version drift yoxlaması və təsirlənən testlərlə birlikdə CI-dan keçməlidir |

### global.json nümunə məzmunu

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestPatch"
  }
}
```

`rollForward: latestPatch` security patch-lərinin absorbsiyasını qəbul edir,
feature-band drift-ini (`10.0.200+` kimi) bloklayır; major migration
governance-controlled qalır.

`global.json` skeleton/toolchain round-da yaradılır və həmin anda human-approved
toolchain/package pin list-də təsdiqlənmiş installed .NET 10 SDK patch versiyasına
pin edilir; yuxarıdakı `10.0.100` format nümunəsidir. Docs-only stack revision
exact patch uydurmur və developer maşınında olmayan SDK versiyası ilə repo-nu
bloklamır.

### Directory.Build.props nümunə məzmunu

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

### Target Framework Split

Project tipindən asılı olaraq target framework dəyişir; `Directory.Build.props` shared TFM-i pin etmir, hər project öz `.csproj`-unda TFM-i təyin edir:

- **WPF host və UI project-ləri**: `net10.0-windows` (WPF tələbi).
- **Contracts, core abstractions, plugin-visible SDK/contracts**: `net10.0` (platform-neutral; Windows-specific adapter sərhədi explicit tələb olunmadıqca).
- **Infrastructure adapter-ləri Windows API çağırarsa**: `net10.0-windows` ya da ayrıca platform-specific project-də izolyasiya olunur.

Per-project skeleton TFM matrix:

| Project | Target framework | Reason |
|---|---|---|
| `Dobby.Host` | `net10.0-windows` | WPF executable entry point and Windows desktop host. |
| `Dobby.UI` | `net10.0-windows` | Shared WPF visual components and resource dictionaries. |
| `Dobby.Shell` | `net10.0-windows` | WPF shell composition and hosting layer. |
| `Dobby.Admin` | `net10.0-windows` | Standalone WPF `WinExe` admin/recovery tool. |
| `Dobby.Contracts` | `net10.0` | Plugin-visible stable contracts stay platform-neutral. |
| `Dobby.SDK` | `net10.0` | Plugin authoring helpers stay platform-neutral and do not reference WPF/UI projects. |
| `Dobby.Core` | `net10.0` | Core abstractions and primitives stay platform-neutral. |
| `Dobby.PluginFramework` | `net10.0` | Plugin loading/registration orchestration does not expose WPF surface. |
| `Dobby.Security` | `net10.0` by default; `net10.0-windows` only if Windows-only adapters are not isolated | Security application logic stays platform-neutral unless a Windows adapter is physically kept here. |
| `Dobby.Licensing` | `net10.0` | License lifecycle and runtime mode logic stay platform-neutral. |
| `Dobby.Configuration` | `net10.0` | Configuration model stays platform-neutral. |
| `Dobby.Localization` | `net10.0` | Resource/provider boundary stays platform-neutral; WPF binding adapter belongs outside plugin-visible surface. |
| `Dobby.Navigation` | `net10.0` | Navigation descriptors and route identity stay platform-neutral. |
| `Dobby.Messaging` | `net10.0` | Event contracts and messaging abstractions stay platform-neutral. |
| `Dobby.Application` | `net10.0` | Use-case orchestration stays UI-neutral. |
| `Dobby.Diagnostics` | `net10.0` | Local diagnostics/logging abstractions stay UI-neutral. |
| `Dobby.Audit` | `net10.0` | Audit model and writer abstractions stay UI-neutral. |
| `Dobby.Infrastructure` | `net10.0` by default; `net10.0-windows` for Windows API adapter project/slice | SQLite/Dapper and host persistence stay neutral unless Windows-specific APIs are used directly. |
| `Dobby.Sample.Plugin` | `net10.0` | Sample plugin must reference only `Dobby.Contracts` / `Dobby.SDK` and stay platform-neutral. |
| Test projects | Match the project under test, or `net10.0-windows` for WPF smoke/integration tests | Test TFM follows required runtime surface. |

Plugin-visible contract layer-i Windows TFM-ə bağlı olmamalıdır; platform leakage plugin compatibility-ni bloklayır.

`Dobby.SDK` platform-neutral qaldığı üçün `Dobby.UI`-a project reference saxlamır; plugin-visible UI descriptor-ları `Dobby.Contracts`/`Dobby.SDK`, WPF implementation isə `Dobby.UI`/`Dobby.Shell` sərhədində qalır.

### Directory.Packages.props nümunə məzmunu

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- <PackageVersion Include="WPF-UI" Version="..." /> kimi bütün versiya bağlamaları burada saxlanılır -->
  </ItemGroup>
</Project>
```

`ManagePackageVersionsCentrally` Microsoft-un CPM konvensiyasına uyğun `Directory.Packages.props`-da saxlanılır; `Directory.Build.props` yalnız build-level property-lər üçün istifadə olunur.

**CPM enforcement qaydası:** `.csproj` fayllarında `PackageReference` yalnız paket adını göstərir; `Version` atributu qadağandır və bütün versiya bağlamaları `Directory.Packages.props`-dakı `PackageVersion` element-lərində mərkəzləşir. Inline version drift CI-da build error kimi qeyd olunur.

### P1 Toolchain / Package Pin Gate

Bu docs-fix pass exact SDK patch və NuGet version-ları uydurmur. Skeleton commit 1
`global.json`, `Directory.Packages.props` və `.csproj PackageReference`-ləri yalnız
human-approved toolchain/package pin list olduqda yarada bilər. Əgər həmin pin list
verilməyibsə, skeleton toolchain/package creation stop condition-da dayanmalıdır.

Pin list minimum bu sütunları daşımalıdır:

| Field | Meaning |
|---|---|
| `PackageId` / `Sdk` | NuGet package id və ya .NET SDK feature/patch entry |
| `ReferenceStance` | `Tier 0`, `Tier 1`, `Tier 2`, `Tier 3`, `Tier 4`, `Tier R`, or explicit stop condition until exact human approval |
| `ApprovedVersion` | Human-approved exact version; Tier 0 BCL/runtime-only və Tier R rejected/deferred rows üçün boş qalır |
| `Owner` | Owning ADR/spec |
| `Scope` | production, test-only, dev-tooling, future/materialization |

Human-approved pin list gələnə qədər aşağıdakı tier stance-lər canonical sayılır.
Bu table exact version seçmir; `ApprovedVersion` yalnız ayrıca human-approved
pin list-də bağlanır.

| Package / primitive | Tier stance | Reason | Source |
|---|---|---|---|
| `.NET SDK` | stop condition | `global.json` exact SDK patch human approval tələb edir. | Pin gate |
| `System.Text.Json` | Tier 0 | BCL/runtime-only; explicit package reference yaratma. | Full dependency list, BCL rule |
| `System.IO.Compression`, `System.IO.Compression.ZipFile` | Tier 0 | BCL/runtime-only; explicit package reference yaratma. | Full dependency list, BCL rule |
| `System.Threading.Channels` | Tier 0 | Initial skeleton üçün BCL/runtime-only stance; future target/runtime evidence explicit package tələb edərsə pin list-də ayrıca bağlanır. | Background tasks row |
| `IHostedService`, `IProgress<T>`, `CancellationToken`, `TimeProvider`, `Guid.CreateVersion7()` | Tier 0 | Runtime/BCL primitives; explicit package reference yaratma. | Runtime primitives |
| BCL `Rfc2898DeriveBytes` | Tier 0 | PBKDF2 fallback əlavə package tələb etmir. | Full dependency list |
| `Microsoft.Extensions.Hosting` | Tier 1 | Generic Host skeleton-readiness foundation; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.DependencyInjection` | Tier 1 | DI foundation; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.Configuration` | Tier 1 | Configuration foundation; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.Configuration.Binder` | Tier 1 | Typed config binding; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.Options` | Tier 1 | Options pattern foundation; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | Tier 1 | Options binding; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.Options.DataAnnotations` | Tier 1 | Options validation; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.Configuration.Json` | Tier 1 | `appsettings.json` provider; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | Tier 1 | Environment variable provider; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.Configuration.UserSecrets` | Tier 1 | Development secrets provider; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.Logging` | Tier 1 | Logging abstraction; exact package version yenə pin list tələb edir. | Full dependency list |
| `Serilog.Extensions.Hosting` | Tier 1 | Logging integration; exact package version yenə pin list tələb edir. | Full dependency list |
| `Serilog.Sinks.File` | Tier 1 | Local file logging; exact package version yenə pin list tələb edir. | Full dependency list |
| `Serilog.Sinks.Console` | Tier 1 | Dev console logging; exact package version yenə pin list tələb edir. | Full dependency list |
| `Serilog.Settings.Configuration` | Tier 1 | Serilog config binding; exact package version yenə pin list tələb edir. | Full dependency list |
| `FluentValidation` | Tier 1 | Validation foundation; exact package version yenə pin list tələb edir. | Full dependency list |
| `FluentValidation.DependencyInjectionExtensions` | Tier 1 | Validator DI registration; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.Caching.Memory` | Tier 1 | In-memory cache foundation; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.Extensions.Localization` | Tier 1 | Localization abstractions; exact package version yenə pin list tələb edir. | Full dependency list |
| `NuGet.Versioning` | Tier 1 | Plugin dependency constraint parsing; exact package version yenə pin list tələb edir. | Full dependency list |
| `JsonSchema.Net` | Tier 1 | Manifest schema validation; exact package version yenə pin list tələb edir. | Full dependency list |
| `Microsoft.AspNetCore.Authorization` | Tier 1 | Policy engine foundation; exact package version yenə pin list tələb edir. | Full dependency list |
| `System.Security.Cryptography.ProtectedData` | Tier 1 | Windows DPAPI/protected-data surface; exact package/reference stance pin list-də bağlanır. | Full dependency list |
| `xunit` | Tier 1 test-only | v2 default; exact v2 pin human-approved olmalıdır, v3 autonomous seçilmir. | Full dependency list, test-only rule |
| `xunit.runner.visualstudio` | Tier 1 test-only | Test runner; exact version human-approved olmalıdır. | Full dependency list, test-only rule |
| `Shouldly` | Tier 1 test-only | Approved assertion package; production project-lərinə sızmır. | Full dependency list, test-only rule |
| `NSubstitute` | Tier 1 test-only | Approved mocking package; production project-lərinə sızmır. | Full dependency list, test-only rule |
| `Verify.Xunit` | Tier 1 test-only | Snapshot test package; production project-lərinə sızmır. | Full dependency list, test-only rule |
| `Microsoft.Extensions.TimeProvider.Testing` | Tier 1 test-only | Deterministic time tests; production project-lərinə sızmır. | Full dependency list, test-only rule |
| `Microsoft.NET.Test.Sdk` | Tier 1 test-only | Test SDK; production project-lərinə sızmır. | Full dependency list, test-only rule |
| `coverlet.collector` | Tier 1 test-only | Coverage collector; production project-lərinə sızmır. | Full dependency list, test-only rule |
| `Konscious.Security.Cryptography.Argon2` | Tier 2 | Auth materialization package; version/license/health pin-i human-approved olmalıdır. | Full dependency list, ADR-0038 |
| `Microsoft.Data.Sqlite.Core`, `SQLitePCLRaw.core` | Tier 3 | P1 Persistence Materialization Pass owns exact package/cipher/native proof; initial provider-neutral skeleton production provider package-i pin etmir. | Full dependency list, ADR-0054 |
| Native encrypted SQLite provider packages | Tier 3 | Native provider proof materialization pass-da bağlanır. | Pin gate |
| `Dapper` | Tier 3 | Persistence/materialization ORM; provider-neutral skeleton onu production DB evidence kimi istifadə etmir. | Full dependency list |
| `WPF-UI` | Tier 4 | UI/Shell package; exact version later UI/Shell package-bearing pass-da human-approved pin list tələb edir. | Full dependency list |
| `WPF-UI.DependencyInjection` | Tier 4 | WPF-UI integration; exact version later UI/Shell package-bearing pass-da human-approved pin list tələb edir. | Full dependency list |
| `CommunityToolkit.Mvvm` | Tier 4 | MVVM UI/application ergonomics; exact version later UI/Shell package-bearing pass-da human-approved pin list tələb edir. | Full dependency list |
| Section 8 commercial-license rows except `Moq` | Tier R | `commercial-license rejected`. | `Commercial License Do-Not-Use` |
| `Moq` | Tier R | `supply-chain/trust rejected`. | `Commercial License Do-Not-Use` |
| `SQLitePCLRaw.bundle_*` encrypted bundle packages | Tier R | `supply-chain/deprecated rejected`. | `Supply-Chain / Deprecated Do-Not-Use` |
| `Microsoft.Extensions.Resilience`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Http.Resilience` | Tier R | `capability-gated deferred`; only explicit internet-based optional capability qərarı ilə açılır. | Offline core rule |

### Build Metadata And Version Source

Host version `Directory.Build.props` və ya project metadata-sındakı `<Version>` /
`<AssemblyVersion>` dəyərlərindən build zamanı assembly metadata-sına embed edilir.
About, diagnostics/export metadata, backup metadata və plugin-visible
`IHostVersionProvider.Current` eyni build/project metadata-derived dəyəri expose edir;
`appsettings.json` dəyəri cache/display edə bilər, amma canonical version source kimi
define edə bilməz.

P1 host version provider shape typed SemVer value üzərində bağlanır:

```csharp
public sealed record HostVersion(int Major, int Minor, int Patch)
    : IComparable<HostVersion>;

public interface IHostVersionProvider {
    HostVersion Current { get; }
}
```

`HostVersion` Dobby-owned `Dobby.Core / Versioning` primitive-idir və
`Dobby.Contracts` üzərindən plugin-visible transitive Core surface-ə yalnız
`ADR-0001` siyahısında göstərildiyi üçün çıxır. Dəyər üç componentli SemVer
2.0.0 (`MAJOR.MINOR.PATCH`) formatıdır; pre-release və build metadata P1
manifest validation-da qəbul edilmir. `NuGet.Versioning` parsing/validation üçün
internal istifadə oluna bilər, amma plugin-visible surface-ə leak etmir.

---

## 5. Distribution Model

| Qat | Seçim | Qeyd |
|---|---|---|
| Primary packaging | MSIX | Modern Windows packaging; IT Group Policy dəstəyi, sandbox-friendly |
| Fallback | MSI (WiX Toolset) | MSIX-i blok edən enterprise mühitlər üçün klassik installer |
| Publish mode | Self-contained (single-file opsional) | .NET runtime dependency yoxdur; MSIX-də single-file məcburi deyil, MSI/portable-da opsionaldır |
| Portable/support artifact | Self-contained zip və ya single-file build | Yalnız support/dev/offline test artifact; primary customer deployment formatı deyil və Phase 1 release blocker sayılmır |
| Installer code signing | Əvvəlcə self-signed (dev); production-da commercial cert | Authenticode/MSIX/MSI artifact signing üçündür; plugin package/vendor master signing key ilə eyni key custody deyil |
| Vendor plugin/license signing | ECDSA P-256 vendor master key (`ADR-0032`, `ADR-0033`) | `.dobbyplugin`, `.dobbylicense` və recovery artifact trust üçündür; private key vendor/admin boundary-də qalır və installer certificate ilə qarışdırılmır |
| Auto-update | Local/manual replacement default; AppInstaller optional enhancement | Phase 1-də yalnız manual local replacement path dəstəklənir; ayrıca `Dobby.Update` project-i və internet əsaslı auto-update P2-də dərinləşir |

### Niyə MSIX primary

- Enterprise IT üçün ən asan deploy: sideload, Group Policy, Intune.
- Lokal/manual update baseline-i; AppInstaller yalnız opsional online enhancement-dir.
- Clean install/uninstall — registry pollution yoxdur.
- Solo developer üçün WiX-dən daha az konfiqurasiya.

Commercial dependency qadağası runtime/build NuGet və UI component-lərə aiddir; code-signing certificate dependency/library deyil, release compliance xərcidir.

MSI yalnız MSIX-ə keçməmiş enterprise mühitlər və ya custom install path tələb edən ssenarilər üçün saxlanır. Phase 1-də MSI skeleton saxlanır, amma aktiv istifadə yalnız müştəri tələbi ilə.

---

## Tam Phase 1 dependency siyahısı

| Package | Məqsəd |
|---|---|
| `WPF-UI` | Windows 11 Fluent Design theme + controls |
| `WPF-UI.DependencyInjection` | Generic Host + WPF-UI inteqrasiyası |
| `CommunityToolkit.Mvvm` | Source-generated MVVM |
| `Microsoft.Extensions.Hosting` | Generic Host |
| `Microsoft.Extensions.DependencyInjection` | DI container |
| `Microsoft.Extensions.Configuration` | Config binding |
| `Microsoft.Extensions.Configuration.Binder` | Typed config binding: `Get<T>()`, `Bind()` |
| `Microsoft.Extensions.Options` | Options pattern əsas abstraksiyası |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `services.Configure<T>(config.GetSection(...))` |
| `Microsoft.Extensions.Options.DataAnnotations` | Options validation via DataAnnotations |
| `Microsoft.Extensions.Configuration.Json` | appsettings.json |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | Environment variable config provider |
| `Microsoft.Extensions.Configuration.UserSecrets` | Development secrets |
| `Microsoft.Extensions.Logging` | Logging abstraction |
| `Serilog.Extensions.Hosting` | Serilog + Generic Host |
| `Serilog.Sinks.File` | File logging |
| `Serilog.Sinks.Console` | Console logging (dev) |
| `Serilog.Settings.Configuration` | Serilog config `appsettings.json`-dan; rebuild-siz log level dəyişikliyi |
| `System.Text.Json` | Serialization (BCL built-in, explicit reference lazım deyil) |
| `System.IO.Compression` / `System.IO.Compression.ZipFile` | Plugin package zip read/write (BCL built-in, explicit reference lazım deyil) |
| `FluentValidation` | Validation rules |
| `FluentValidation.DependencyInjectionExtensions` | DI auto-registration |
| `Microsoft.Extensions.Caching.Memory` | In-memory cache |
| `xunit` | Test framework (v2 default; v3 yalnız ayrıca human-approved toolchain/package decision ilə upgrade candidate) |
| `xunit.runner.visualstudio` | VS test runner |
| `Shouldly` | Test assertions (pulsuz; FluentAssertions v8+ commercial) |
| `NSubstitute` | Mocking |
| `Verify.Xunit` | Snapshot testing |
| `Microsoft.Extensions.Localization` | Localization abstractions + `.resx` support |
| `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.core` | SQLite local persistence provider candidate; DB-at-rest protection provider `ADR-0054` guardrail-lərinə görə `P1 Persistence Materialization Pass` bu explicit native bootstrap yolunu qiymətləndirir. Initial skeleton provider-neutral qalır və production DB-at-rest provider package-i pin etmir. Native encrypted provider proof keçmədən heç bir production DB-at-rest provider package default provider kimi qəbul edilmir. Full bundled `Microsoft.Data.Sqlite` və `SQLitePCLRaw.bundle_*` provider proof-u kimi qəbul edilmir. |
| `Dapper` | Lightweight ORM — plugin storage, settings, audit |
| `NuGet.Versioning` | SemVer range parsing — plugin dependency constraints |
| `System.Security.Cryptography.ProtectedData` | Windows DPAPI — password hash, remember-me secret, recovery/session material və protected config storage (`ADR-0038`, `ADR-0030`, `ADR-0055`) |
| `Konscious.Security.Cryptography.Argon2` | Password hashing üçün default seçim (`Argon2id`, ADR-0038); yalnız package health, license compatibility və ya `ADR-0042` baseline-da ölçülmüş login latency `AC-014` gate-ini pozarsa PBKDF2 fallback seçilir |
| BCL `Rfc2898DeriveBytes` | Password hashing fallback (`PBKDF2-HMAC-SHA256`, explicit `HashAlgorithmName.SHA256`, minimum `600_000` iteration); əlavə package tələb etmir |
| `JsonSchema.Net` | Plugin manifest JSON Schema validation using `docs/schemas/dobby-plugin-manifest.schema.json` |
| `Microsoft.AspNetCore.Authorization` | Policy engine — `IAuthorizationService`, `AuthorizationHandler<T>`, requirements (web middleware olmadan istifadə edilir) |
| `Microsoft.Extensions.TimeProvider.Testing` | `FakeTimeProvider` — time-dependent kodun deterministik testləri |
| `Microsoft.NET.Test.Sdk` | Test SDK |
| `coverlet.collector` | Code coverage |

**Test-only qayda:** `xunit`, `xunit.runner.visualstudio`, `Shouldly`, `NSubstitute`, `Verify.Xunit`, `Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` yalnız `*.Tests` project-lərində reference olunur və production runtime project-lərinə sızmamalıdır.

**BCL qayda:** `System.Text.Json` və `System.IO.Compression` / `System.IO.Compression.ZipFile` BCL built-in entry kimi göstərilir; `PackageReference` tələb etmir — stack decision visibility üçün sadalanır.

**Offline core qaydası:** `Microsoft.Extensions.Resilience`, `Microsoft.Extensions.Http` və `Microsoft.Extensions.Http.Resilience` offline-only core baseline-a daxil deyil. Bu paketlər yalnız açıq şəkildə internet əsaslı opsional capability (məs. kənar servis inteqrasiyası, optional online sync, optional update polling) qərarı veriləndə əlavə olunur.

**Package status legend:** accepted rows are implementation inputs only when the
owning ADR/spec has accepted the dependency for the current phase. `candidate`,
`optional candidate`, `reserved`, `P2`, `future` and similar status words are
planning/evaluation vocabulary, not implementation authorization and not
release evidence. Candidate provider packages especially do not satisfy
ADR-0054 DB-at-rest release proof until provider-enabled startup evidence is
accepted.

---

## 6. Authentication Strategy

**Status:** Əsas qərar ADR-0038 ilə bağlanıb. Dobby-nin core auth yolu device-bound local password-based auth-dır; offline-first məhsul prinsipi ilə uzlaşdırılıb. MSAL.NET, WAM və Entra ID / OIDC Phase 1 scope-una daxil deyil və default auth yolunun hissəsi kimi saxlanmır.

| Qat | Seçim | Qeyd |
|---|---|---|
| Default (və yeganə Phase 1 yolu) | Device-bound local password-based auth | Offline-first məhsul prinsipi (ADR-0038); identity cihazdadır və cihazla bağlıdır |
| Password hashing | `Argon2id` default niyyəti (`Konscious.Security.Cryptography.Argon2`) və ya BCL `Rfc2898DeriveBytes` fallback (`PBKDF2-HMAC-SHA256`) | Skeleton/toolchain gate-də human-approved package pin list tələb olunur: `Argon2id` seçilir, yalnız package health, license compatibility və ya `ADR-0042` baseline-da ölçülmüş login latency `AC-014` gate-ini pozarsa PBKDF2 fallback seçilir. Pin list yoxdursa skeleton package creation dayanır; agent version uydurmur. `Argon2id` üçün starter floor `m=19 MiB`, `t=2`, `p=1`; PBKDF2 üçün explicit `HashAlgorithmName.SHA256` və minimum `600_000` iteration; hər credential üçün minimum 128-bit random salt; tuning bu floor-lardan aşağı düşə bilməz |
| Credential / session storage | `System.Security.Cryptography.ProtectedData` (DPAPI) | Password hash, remember-me secret və session material cihazda DPAPI/protected storage ilə saxlanır; exact scope mapping `ADR-0055` ilə bağlanır |
| Session semantics | ADR-0031 və ADR-0038 ilə tənzimlənir | Phase 1-də session refresh yoxdur və yalnız bir active user session olur. Logout authenticated context və in-memory user state-i təmizləyib login flow-a qaytarır; lock/manual lock/auto-lock current session-u saxlayıb unlock və ya logout seçimi verir. Remember-me auto-login/session refresh deyil, ADR-0038-də pre-auth masked-fill rahatlığı kimi tənzimlənir |
| Cloud identity (Entra / OIDC və digər) | Phase 1 scope-una daxil deyil | Gələcəkdə yalnız opsional plugin/modul kimi qoşula bilər; core host məcburi internet-dən asılı olmur |

---

## Security Floors

Kod və qəbul olunmuş ADR-larla müəyyən edilən security floor-ları runtime config
tərəfindən aşağı salına bilməz. `§ 6 Authentication Strategy`-də verilmiş
`Argon2id` / `PBKDF2` minimumları, `ADR-0054` DB-at-rest provider, signature
verification və `ADR-0055` protected-storage minimumları config ilə zəiflədilməyə
cəhd olunarsa, startup fail-closed davranır. `appsettings.json` bu floor-ları
aşağı sala bilməz; istisna yalnız ayrıca ADR amendment ilə verilir.

---

## 7. Core Primitives (Dobby-owned)

Bu primitive-lər Dobby-nin özü implement edir; 3rd-party library istifadə olunmur.

| Primitive | Yer | Qeyd |
|---|---|---|
| `Result<T>` | `Dobby.Core/ResultStandardization` | Vahid success/failure/result modeli; arxitektura xəritəsi #5 |
| `IEventBus` | `Dobby.Messaging` | Integration event publish/subscribe; external MediatR istifadə olunmur |
| `TimeProvider` | BCL (.NET 8+) | `Dobby.Core/Time` namespace-i BCL `TimeProvider`-ı istifadə edir; test üçün `FakeTimeProvider` |
| UI-local messaging | `CommunityToolkit.Mvvm` içindəki `WeakReferenceMessenger` | Ayrıca NuGet lazım deyil |
| Async context | `AsyncLocal<T>` (BCL) | `ICorrelationContextAccessor`, `ICurrentUser`, customer/deployment context və `ICurrentTenant` kimi local current-scope primitive-lər; background task enqueue zamanı explicit propagation (ADR-0029). Tenant/customer context v2 `ADR-0052`-də bağlanıb (Phase 1 single active customer/deployment + `default` tenant storage primitive; multi-customer switching P2) |
| ID generation | `Guid.CreateVersion7()` (BCL, .NET 9+) | Non-secret system IDs üçün (correlation, audit, job); security token və secret üçün istifadə olunmur. Gələcəkdə SQL Server `uniqueidentifier` storage gəlirsə UUIDv7 sort davranışı ayrıca yoxlanılır; SQLite/text/blob üçün problem daha azdır |

### P1 Approach (package deyil, yanaşma)

| Sahə | Yanaşma | Qeyd |
|---|---|---|
| Forms | `ObservableValidator` (`CommunityToolkit.Mvvm`) + `FluentValidation` | Artıq seçilmiş iki kitabxananın inteqrasiyası |
| DataGrid | WPF built-in DataGrid + WPF-UI theming | Phase 1 üçün built-in DataGrid kifayətdir; daha güclü open-source grid ehtiyacı yaranarsa ayrıca qiymətləndirilir |
| WPF localization binding | `.resx` + `IStringLocalizer<T>` + Dobby-owned binding/resource adapter | Runtime culture switch və fallback `Dobby.Localization` altında idarə olunur; əlavə package yoxdur — `WPFLocalizeExtension` yalnız P2 candidate |
| Online integrations | `IHttpClientFactory` + typed clients + `Microsoft.Extensions.Http.Resilience` | Yalnız opsional internet əsaslı capability-lər üçün; core məhsul funksionallığının hissəsi deyil. `Refit` stable API-lər üçün P2 optional candidate |
| Background tasks | `IHostedService` + `System.Threading.Channels` + `IProgress<T>` + `CancellationToken` | `IHostedService`, `IProgress<T>`, `CancellationToken` BCL; `System.Threading.Channels` initial skeleton üçün BCL/runtime-only stance daşıyır və explicit PackageReference yaradılmır. Future target/runtime evidence explicit package tələb edərsə human-approved pin list-də ayrıca bağlanır. Shell progress/work-session reporting ilə `IProgress<T>` inteqrasiyası (ADR-0015, ADR-0050); `IHostedService` post-login gating ADR-0041-də |
| Audit storage | Dapper + append-only repository wrapper over protected local persistence | Public API update/delete vermir; compliance invariant kod səviyyəsində qorunur və backing DB data-at-rest protected olmalıdır (ADR-0019, ADR-0030) |
| Command/Action pipeline | Dobby-owned abstraction | Optional implementation candidate: `Mediator` (martinothamar, MIT, source-generated) — ADR ilə seçilərsə; MediatR commercial line istifadə olunmur |
| Search | BCL debounce + `string.Normalize` (accent tolerance) + SQLite `LIKE`; scale lazım olarsa SQLite FTS5 | P1-də əlavə package yoxdur; `Lucene.NET` yalnız P2 candidate (ADR-0025 placeholder / P2 deferred; bax `docs/adr/README.md` numbering gaps) |
| Deep link | MSIX `<uap:Extension Category="windows.protocol">` + `System.Uri` + Dobby-owned route matcher | Package tələb etmir; Windows protocol registration + BCL URI parsing (ADR-0013) |
| UI dispatcher | Host-internal `IUiDispatcher` abstraction + WPF `Application.Current.Dispatcher` | Plugin-facing `Dobby.Contracts` / `Dobby.SDK` səthində deyil; plugin UI feedback yalnız declared page/extension, notification və progress descriptor contract-ları üzərindən gedir; implementation WPF Dispatcher ilə `Dobby.Messaging` / `Dobby.Shell` host-internal Messaging UI Bridge-də istifadə olunur (`ADR-0010` + `docs/specs/shell-ui.md`) |

---

## 8. Commercial License Do-Not-Use

Aşağıdakı paketlər commercial license və ya supply-chain/trust riski səbəbi ilə Dobby-də **istifadə olunmamalıdır**:

| Paket | Səbəb | Pulsuz alternativ |
|---|---|---|
| Commercial-license MediatR line (LuckyPennySoftware) | Commercial license | Dobby-owned `IEventBus` |
| EPPlus v5+ | Commercial for non-nonprofit | `ClosedXML` (MIT) və ya `NPOI` (Apache 2.0) |
| FluentAssertions v8+ | Commercial license | `Shouldly` (MIT) |
| Syncfusion / DevExpress / Telerik | Commercial UI components | WPF built-in + `WPF-UI` |
| Moq | SponsorLink supply-chain + privacy trust risk | `NSubstitute` (MIT) |
| AutoMapper commercial line | MediatR ilə eyni commercial ekosistem | Manual mapping və ya `Mapperly` (MIT, source-generated, zero-reflection) |

---

## Supply-Chain / Deprecated Do-Not-Use

Aşağıdakı paketlər commercial license səbəbi ilə yox, maintenance/deprecation və
wrong-native-provider riski səbəbi ilə Dobby-də default yol kimi istifadə
olunmamalıdır:

| Paket | Səbəb | Qəbul olunan yol |
|---|---|---|
| `SQLitePCLRaw.bundle_e_sqlite3mc` və digər encrypted SQLite `SQLitePCLRaw.bundle_*` paketləri | Deprecated və ya yanlış native provider gətirmə riski; `ADR-0054` Option A custom encrypted provider proof tələb edir | Skeleton/security round `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.core` + explicit native provider bootstrap yolunu qiymətləndirir |

---

## 9. Phase 2 Package Candidates

Bu paketlər Phase 1-də lazım deyil, amma P2-də qiymətləndiriləcək:

| Paket | Məqsəd | Phase / Status |
|---|---|---|
| `CsvHelper` (Apache 2.0) | CSV import/export | P2 |
| `ClosedXML` (MIT) | Excel import/export (EPPlus əvəzinə) | P2 |
| `OpenTelemetry.Extensions.Hosting` (Apache 2.0) | Metrics, traces, observability; yalnız explicit opt-in, default no-telemetry modelini pozmadan | P2 |
| `Microsoft.Extensions.Diagnostics.HealthChecks` | Runtime health check | P2 |
| `Microsoft.FeatureManagement` (MIT) | Feature flags / capability settings | P2 |
| `Velopack` (MIT) | Auto-update alternative; MSIX limitations aşkarlanarsa evaluate | P2 |
| `WixToolset.Sdk` | MSI fallback build tooling; yalnız MSIX blok olunan enterprise mühitlər üçün | fallback |
| `Microsoft.Extensions.Resilience` | Opsional internet əsaslı inteqrasiyalar üçün retry/timeout/circuit-breaker | P2 |
| `Microsoft.Extensions.Http` | Opsional internet əsaslı capability-lər üçün `IHttpClientFactory` | P2 |
| `Microsoft.Extensions.Http.Resilience` | Opsional internet əsaslı `HttpClient` resilience pipeline-ları | P2 |
| `Refit` (MIT) | Type-safe HTTP client; stable API-lər üçün optional | P2 |
| `Mediator` (martinothamar, MIT) | Source-generated command/action pipeline; ADR ilə seçilərsə Dobby-owned abstraction-ı implement edir | P2 |
| `System.IO.Hashing` (MIT) | Non-cryptographic checksum (cache, general file hashing); plugin integrity üçün YOX — orada SHA-256 qalır | P2 |
| `LinqToDB` (MIT) | LINQ-based ORM + migrations; Dapper kifayət etmədiyi complex query ssenariləri üçün | P2 |
| `AwesomeAssertions` (Apache 2.0) | FluentAssertions community fork; Shouldly kifayət etmədiyi halda alternativ | P2 |
| `TUnit` (MIT) | Async-native test framework; ekosistem yetişəndə qiymətləndirilir | P2 |

---

## 10. Lisenziya watch-list

Bu paketlər hazırda pulsuzdur, amma gələcək version upgrade-lərində lisenziya yoxlanılmalıdır:

| Paket | Cari lisenziya | Risk / qeyd |
|---|---|---|
| `FluentValidation` | Apache 2.0 | Maintainer keçmişdə commercial model müzakirə edib; upgrade zamanı lisenziya yoxlanılır (do-not-use deyil) |
| `WPF-UI` | MIT | Tək maintainer (Lepo.co); fork imkanı açıqdır, backup: MahApps.Metro |
| `Serilog` | Apache 2.0 | Sponsor-dependent; .NET Foundation üzvü deyil |
| `JsonSchema.Net` | MIT | Manifest validation critical path-dədir; major upgrade zamanı schema draft (2020-12) uyğunluğu yoxlanılır — license riski deyil, compatibility riski |
| `WixToolset.Sdk` | MS-RL source license; WiX v6+ OSMF/EULA terms | MSI fallback yolu üçün (`§ 5 Distribution Model`); WiX v6+ Open Source Maintenance Fee (OSMF) kommersiya/revenue threshold-u olan istifadəçilər üçün sponsorship/compliance yoxlaması tələb edə bilər; version pin qərarı (older free line vs OSMF line) MSI fallback skeleton round-unda bağlanır |

---

## Təxirə salınan qərarlar

Bu spec-in scope-undan kənar, gələcək iterasiyalarda bağlanacaq:

- **CI/CD pipeline:** GitHub Actions və ya Azure Pipelines — ayrıca spec.
- **Plugin SDK packaging:** Plugin developer-lər üçün NuGet template — SDK surface area (ADR-0021) bağlanandan sonra.
- **Plugin manifest polymorphism:** ADR-0036 implementation zamanı `JsonPolymorphic` / `JsonDerivedType` attribute-larının istifadəsi implementation guideline olaraq bağlanır.

---

## Branch Shared Storage Stack Reservation

`ADR-0056` Branch Shared mode üçün PostgreSQL 16.x-i Tier B/customer-release
storage target kimi rezerv edir. Mərhələ 1 sənəd pass-i və gələcək Tier A
skeleton interface shape-i Npgsql runtime dependency-si əlavə etmir və live
PostgreSQL test tələb etmir.

`Npgsql` yalnız Branch Shared runtime adapter implementation açıldıqda active
dependency ola bilər. O vaxta qədər PostgreSQL setup, schema/grant smoke,
backup/restore və operational protection evidence
`docs/specs/postgres-deployment-ops.md` daxilində sənəd/readiness mövzusu kimi
qalır.

`docs/schemas/branchshared.schema.json` Branch Shared connection profile üçün
JSON Schema owner-dir. `serverIdentityPinMaterial` lowercase 64-character
SHA-256 hex formatında saxlanır.

## 2026-08 Security Profile Amendment (Controlling)

- Password storage keeps the accepted Argon2id/PBKDF2 floors, unique salts and
  versioned parameters, but no arbitrary composition rule is added. Password
  length, breached/common-value screening where an offline maintained list is
  available, paste/password-manager support and durable throttling are the
  controlling controls.
- Every ECDSA wire format explicitly names curve, hash and
  `IeeeP1363FixedFieldConcatenation`; signatures use unpadded base64url and a
  versioned, domain-separated canonical payload. Default library encodings are
  never relied on.
- Key material is selected from purpose-separated, versioned key rings. The
  package, license, recovery-response and BranchSetup-authorization purposes do
  not reuse one leaf key.
- ZIP verification enforces limits before allocation and while streaming:
  manifest size, entry count, per-entry uncompressed bytes, aggregate
  uncompressed bytes and compression ratio. Extraction uses normalized relative
  paths, Windows-name validation, collision detection, final full-path
  containment and no-overwrite semantics.
- In-process plugin loading is restricted to trusted signed product code.
  Untrusted execution requires a separately versioned IPC contract and an OS
  process boundary; `AssemblyLoadContext` remains a load/unload mechanism only.
- Production MSIX validation must execute installed launch, upgrade and data
  preservation tests with the actual package identity and storage paths. A
  self-signed package build alone is development evidence, not GA evidence.
