## Sprint 10 Packaging Static Scan

Date: 2026-06-20 (Asia/Baku)

Status: IMPLEMENTED AND VERIFIED.

Production release scan scope:

- `src/**/*.csproj`
- `src/**/*.cs`
- `src/**/*.xaml`
- `src/**/*.props`
- `src/Dobby.Security/Dobby.Security.csproj`
- `src/Dobby.Host/Trust/*.pem`

Evidence:

- `ProductionReleasePackagingStaticScanTests.Production_projects_do_not_reference_sample_plugin_or_generated_smoke_artifacts_by_default` verifies production project files do not reference `samples`, `Dobby.Sample.Plugin`, `.dobbyplugin`, or `.dobbylicense` artifacts by default.
- Static scan for `Dobby.Sample.Plugin`, `samples/Dobby.Sample.Plugin`, `.dobbyplugin`, and `.dobbylicense` under `src` found only runtime package/license parsing and file-picker code paths, not production project references or generated artifacts.
- Static scan for `dobby.dev-qa` and `dev-qa-public-key` found the approved trust profile switch and verifier deny/allow logic. Default `DobbyPluginTrustProfile` remains `Production`, and `PluginTrustResourcePackagingTests.Default_production_security_assembly_contains_no_dev_qa_trust_anchor` verifies default production assemblies exclude the dev/QA trust anchor.
- No production output path includes sample plugin artifacts by default. The sample plugin remains under `samples/Dobby.Sample.Plugin` and is exercised by tests as a dev/QA verification artifact.
- Admin package/license/recovery artifacts are generated into test temp directories or explicit operator-selected output paths; no generated `.dobbyplugin`, `.dobbylicense`, or `.dobbyrecovery` file is committed as a production release artifact in Sprint 10.

Result: PASS.
