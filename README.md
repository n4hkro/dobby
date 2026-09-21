# Dobby

Dobby plugin-based Windows desktop host-udur: WPF shell, offline-first enterprise
B2B istifadə modeli və signed plugin/license artefakt boundary-si ilə. Bu fayl repo
entry point-idir; normativ məzmun `docs/` altındadır.

## Documentation Entry Points

| Sənəd | Rolu |
|---|---|
| [docs/01_PRODUCT_SCOPE.md](docs/01_PRODUCT_SCOPE.md) | Məhsul scope-u, prinsiplər, out-of-scope sətirləri |
| [docs/02_ARCHITECTURE_MAP.md](docs/02_ARCHITECTURE_MAP.md) | Component ownership, status/phase, boundary cədvəlləri |
| [docs/03_TECH_STACK_AND_VERSIONS.md](docs/03_TECH_STACK_AND_VERSIONS.md) | Toolchain və package pin authority |
| [docs/04_RESULT_VS_EXCEPTION_POLICY.md](docs/04_RESULT_VS_EXCEPTION_POLICY.md) | Typed result vs exception qaydası |
| [docs/05_FUNCTIONAL_REQUIREMENTS.md](docs/05_FUNCTIONAL_REQUIREMENTS.md) | `FR-*` requirement-lər |
| [docs/06_NON_FUNCTIONAL_REQUIREMENTS.md](docs/06_NON_FUNCTIONAL_REQUIREMENTS.md) | `NFR-*` requirement-lər |
| [docs/07_ACCEPTANCE_CRITERIA.md](docs/07_ACCEPTANCE_CRITERIA.md) | `AC-*` acceptance criteria |
| [docs/08_CODING_STANDARDS.md](docs/08_CODING_STANDARDS.md) | Engineering convention-ları |
| [docs/09_BUILD_RUN_AND_RELEASE.md](docs/09_BUILD_RUN_AND_RELEASE.md) | Build/run/test/publish və release gate-lər |
| [docs/adr/README.md](docs/adr/README.md) | ADR index, status lifecycle və documentation governance |
| [docs/specs/README.md](docs/specs/README.md) | Executable spec index və konflikt authority sırası |
| [docs/schemas/](docs/schemas/) | Artefakt/config JSON Schema authority-si (`$id` reserved) |

## Authority Order

Ziddiyyət halında sıra: (1) ən yeni applicable Accepted ADR, (2) owning schema,
(3) owning executable spec, (4) canonical `docs/01`–`docs/09`, (5) cari
implementation plan — **bu tier hazırda boşdur**: `docs/implementation-plan/`
ağacı `a8a5914` / `35148c2` ilə retire edilib, ona görə konflikt (4)-dən birbaşa
(6)-ya keçir; (6) tarixi plan/evidence. Mövcud kod və yaşıl test product
decision üçün avtomatik authority deyil — test spec-in tələb etdiyi davranışın
sübutudur. Tam mətn: [docs/specs/README.md](docs/specs/README.md) § `Authority Note`
və [docs/adr/README.md](docs/adr/README.md) § `Documentation Governance`.

Runtime, security və release gate-ləri yalnız behavior-first sübutla qapanır
(`NFR-027`, `ADR-0061`); source-text/regex scan, fayl mövcudluğu və ya "test list
green" iddiası kifayət deyil.

## Local Developer Commands

Canonical command sırası [docs/09_BUILD_RUN_AND_RELEASE.md](docs/09_BUILD_RUN_AND_RELEASE.md) § 4-dədir.
Gündəlik flow:

```powershell
dotnet --info
dotnet restore .\Dobby.sln
dotnet build .\Dobby.sln -c Debug --no-restore
dotnet test .\Dobby.sln -c Debug --no-build
dotnet format .\Dobby.sln --verify-no-changes
dotnet run --project .\src\Dobby.Host\Dobby.Host.csproj -c Debug
```

Portable support/dev folder:

```powershell
.\tools\publish-portable\publish-portable.ps1
```

.NET SDK versiyası `global.json` ilə pin edilir; NuGet versiyaları yalnız
`Directory.Packages.props` (Central Package Management) vasitəsilə idarə olunur.

## Repository Layout

| Path | Rolu |
|---|---|
| `src/` | Host, shell, domain, security, licensing və SDK project-ləri |
| `src/Dobby.Host/Trust/` | Purpose-separated **public** trust anchor-ları (version-controlled build input, `ADR-0062` § 3) |
| `tests/` | `Dobby.UnitTests`, `Dobby.IntegrationTests`, `Dobby.SmokeTests` evidence-split test project-ləri |
| `samples/` | Sample/test plugin |
| `tools/` | Signing, license/recovery artefakt generation, verification və publish script-ləri |
| `installer/` | MSIX installer assets |
| `artifacts/` | Publish output və release evidence |
| `docs/` | Canonical sənədlər, ADR-lər, spec-lər və schema-lar |

Private signing material (`*.pfx`, `*.p12`, `*.snk`, `*.key`, `*-private-key.pem`)
heç vaxt bu repository-də saxlanmır; yalnız public trust anchor-lar track olunur.

## Contributing

Agent və developer iş qaydaları [AGENTS.md](AGENTS.md) faylındadır. Qərar
dəyişirsə mövcud Accepted ADR səssiz edit edilmir — successor ADR yazılır və köhnə
sənəd `Superseded` edilir ([docs/adr/README.md](docs/adr/README.md) § `Update Rule`).
