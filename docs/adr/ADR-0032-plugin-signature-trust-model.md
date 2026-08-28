# ADR-0032: Plugin Signature Trust Model

**Status:** Accepted (shared-master-key and shared-primitive wording amended by ADR-0058/ADR-0059 — plugin package signatures use a purpose-separated versioned plugin trust key ring with `IeeeP1363FixedFieldConcatenation` base64url-no-padding signatures; the ECDSA P-256/SHA-256 primitive choice remains)
**Date:** 2026-04-24

## Context

`01_PRODUCT_SCOPE.md` `## Plugins və Licensing` plugin installasiya artefaktının `vendor-admin tərəfindən hazırlanmış imzalanmış plugin paketi` olmasını tələb edir; `vendor, paket imzası və manifest/trust yoxlamaları` install zamanı məcburidir. `02_ARCHITECTURE_MAP.md` Cədvəl 3 `Plugin Signature Trust Model` sətri bunu Phase 1 blocker kimi göstərir və `unsigned və ya untrusted plugin activation-a buraxılmır` qaydasını açıq yazır.

`ADR-0036` manifest schema-sında `signature` field-i elan edir, amma imza formatı və verification axını burada bağlanmır. `ADR-0039` vendor master public key-in compile-time resource kimi embed olunmasını tələb edir və `plugin signature trust model ilə ortaq master key-dir` deyərək bu ADR-ə söykənir.

Bu ADR imza formatını, verification axınını və trust anchor qaydasını rəsmiləşdirir.

## Decision

Phase 1 plugin signature trust modeli:

### 1. İmza alqoritmi

- ECDSA P-256 üzərində imzalama.
- İmza canonical JSON serialization-ın SHA-256 hash-i üzərindən hesablanır.
- İmzalanan payload shape, plugin manifest `signature` block placement-i,
  deterministic `files` section serialization və package fingerprint semantics-i
  `ADR-0045` tərəfindən canonical idarə olunur.
Phase 1 signature placement is canonical in the plugin manifest `signature` block as defined by ADR-0045. Standalone `signature.bin` P1 canonical package formatında yoxdur; it may only be reconsidered by a future compatibility/debug ADR.

Bu alqoritm `ADR-0039 § 4` recovery artefakt imzası ilə eyni kriptoqrafik primitive-ləri işlədir ki, host-da iki ayrı imza verification path saxlanılmasın.

### 2. Trust anchor

- Host binary build zamanı vendor master **public key** compile-time resource kimi embed olunur.
- Host runtime-da heç bir online trust anchor yoxlaması yoxdur; bütün verification offline-first işləyir.
- Trust anchor key material `Dobby.Security / Plugin Trust Verification` namespace-inin internal resource-udur.
- Vendor master private key `ADR-0033`-ün öhdəliyindədir və bu ADR-dən kənar saxlanır.

### 3. Verification axını

Plugin install və activation zamanı aşağıdakı ardıcıllıq məcburidir:

1. Plugin package oxunur və manifest parse olunur (`ADR-0036` schema uyğunluğu əvvəlcə yoxlanır).
2. Manifest-də `signature` field-i və `vendorId` field-i mövcudluğu yoxlanır; yoxdursa paket rədd olunur.
3. Paket daxilindəki bütün faylların SHA-256 hash-i yenidən hesablanır və ADR-0045
   deterministic `files` section-u ilə müqayisə olunur; uyğunsuzluq varsa paket
   rədd olunur (tamper detected).
4. ADR-0045 canonical signed payload üzərindəki imza vendor master public key ilə
   ECDSA P-256 verify olunur; uyğunsuzluqda paket rədd olunur.
5. `vendorId` field host tərəfindən tanınan vendor-dursa (Phase 1-də yalnız master vendor qəbul olunur) paket trusted hesab olunur; əks halda rədd olunur.

Hər addımın nəticəsi `Dobby.Audit / Audit Trail` (Row 70) vasitəsilə qeyd olunur (`plugin.install.attempted`, `plugin.install.signature-failed`, `plugin.install.trust-failed`, `plugin.install.verified`).

### 4. Rejected alternatives

- **X.509 certificate chain:** Phase 1-də rədd edilir — offline-first sisteme CRL/OCSP yoxlaması uyğunlaşmır; trust store management skeleton Phase 1-in scope-undan kənardır.
- **RSA-2048 əvəzinə ECDSA:** ECDSA P-256 daha kiçik imza ölçüsü verir (~72 byte vs 256 byte) və `ADR-0039` ilə ortaq primitive-dir.
- **Self-signed per-customer trust:** Phase 1-də rədd edilir — single master vendor trust anchor deployment sadəliyi üçün seçilir; per-customer trust P2-də açıla bilər.

## Consequences

- Plugin install və activation signature + trust verify addımlarını məcburi keçir; unsigned və ya untrusted plugin activation-a buraxılmır.
- `Dobby.Security / Plugin Trust Verification` namespace-i (Row 38) bu ADR-i canonical qaydaları kimi götürür.
- `Dobby.PluginFramework / Plugin Packaging` namespace-i (Row 27) signature placement-i paket formatına daxil edir.
- `ADR-0036` manifest `signature` field-inin trust verification semantic-i bu ADR-də,
  canonical payload/package placement semantic-i isə `ADR-0045`-də dəqiqləşdirilir.
- Vendor master public key host binary-dan ayrıla bilmir; rotation üçün yeni host build lazımdır (Phase 1-də rotation protocol yoxdur; P2).
- `ADR-0033` vendor private key ownership-i bağlayır; bu ADR yalnız public side verification-ı sahib edir.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — `## Plugins və Licensing`
- `docs/02_ARCHITECTURE_MAP.md` — Row 27 `Plugin Packaging`, Row 38 `Plugin Trust Verification`, Cədvəl 3 `Plugin Signature Trust Model` risk sətri
- `docs/adr/ADR-0033-signing-key-ownership.md` — vendor private key mülkiyyəti və provisioning
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — `signature` və `vendorId` field-ləri
- `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md` — canonical signed payload, files section və package fingerprint
- `docs/adr/ADR-0039-owner-password-recovery.md` — ortaq ECDSA P-256 primitive-i (superseded by `ADR-0058`; primitive seçimi qalır, key ring-lər `ADR-0059` ilə purpose-separated-dir)
