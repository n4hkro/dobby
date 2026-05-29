# ADR-0016: Logging Strategy

**Status:** Accepted
**Date:** 2026-04-30

## Context

Stack selection `Microsoft.Extensions.Logging` və Serilog local sinks seçir, amma
structured field-lər, correlation id, audit/diagnostics ayrımı və redaction floor-u
vahid ADR-də bağlanmamışdı. Result/exception policy və release diagnostics bu
qərara istinad edir.

## Decision

### 1. Logging destination

Phase 1 technical logging local-only diagnostics surface-dir:

- logging abstraction: `Microsoft.Extensions.Logging`;
- sink direction: Serilog local rolling file, console only for dev/operator runs;
- default vendor upload, telemetry, crash report və remote log shipping yoxdur.

Runtime logs `%LocalAppData%/Dobby/logs/` altında saxlanır. Customer production
artifact user data/log file daşımır.

### 2. Structured fields

Every structured log event should include the fields that are known at that
boundary:

- timestamp UTC;
- level;
- source context;
- event/message template;
- `correlationId`;
- `sessionId` or `actor` when authenticated and safe;
- `pluginId` when plugin-related;
- `operationId` when background operation-related;
- typed failure/error code when available.

Raw local path, signature/key detail, raw license payload, raw recovery material
və password/session/remember-me secrets log field-i kimi yazılmır.

### 3. Audit vs diagnostics

Logging diagnostics-dir, audit deyil. User/security/compliance yönümlü hadisələr
`Dobby.Audit` event taxonomy-sinə də yazılmalıdır, amma technical log audit trail-in
əvəzi deyil. Audit action id-ləri və minimum payload `docs/specs/audit-and-diagnostics.md`
tərəfindən idarə olunur.

### 4. Exception flow

Infrastructure exception-lar ilk Dobby boundary-də structured warning/error kimi
loglanır və `Result<T>`-ə çevrilir. Plugin boundary exception-ları
`docs/04_RESULT_VS_EXCEPTION_POLICY.md` mapping-i ilə typed result-a çevrilir.
Fatal/corrupted-state exception-lar tutulmur.

### 5. Redaction floor

Write-time redaction mandatory-dir. Diagnostics export redaction-u ikinci müdafiə
qatıdır, birinci deyil. Logging layer raw password, password hash, token, private
key, session material, remember-me secret, recovery secret, raw recovery nonce,
raw `.dobbylicense` payload və raw license binding detail qəbul etmir.

### 6. Configuration

Log level config-dən rebuild-siz dəyişdirilə bilər, amma security floor-lar config
ilə zəiflədilmir. Config verbose logging-i aktiv etsə belə secret/raw payload
redaction boundary dəyişmir.

## Consequences

- Result/exception policy-dəki `_logger.LogWarning` / `_logger.LogError` nümunələri
  canonical logging strategy-yə bağlanır.
- No-telemetry default saxlanılır.
- AC-033 diagnostics export və privacy gate-ləri local logging boundary ilə
  test edilə bilər.

## Cross-references

- `docs/03_TECH_STACK_AND_VERSIONS.md` — logging abstraction və Serilog sinks
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md`
- `docs/specs/audit-and-diagnostics.md`
- `docs/adr/ADR-0019-audit-vs-diagnostics-boundary.md`
- `docs/adr/ADR-0029-correlation-id-ownership.md`
