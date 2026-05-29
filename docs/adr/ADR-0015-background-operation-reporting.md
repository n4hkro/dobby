# ADR-0015: Background Operation Reporting

**Status:** Accepted
**Date:** 2026-04-30

## Context

Dobby shell bottom status/progress sətri və work-session davranışı long-running
host/plugin əməliyyatlarının UI thread-i bloklamadan görünməsini tələb edir.
Architecture Map Cədvəl 3 `Background Tasks / Process Reporting` sətri bu modelin
açıq qaldığını göstərirdi.

## Decision

### 1. Host-owned operation contract

Phase 1 long-running işlər host-owned background operation contract-ından keçir.
Plugin-facing operation snapshot, progress və cancellation descriptor tipləri
`Dobby.Contracts` altında saxlanır. Host implementation, queue/orchestration və
state management `Dobby.Application / Background Tasks / Process Reporting`
altında qalır. `Dobby.SDK` yalnız bu `Dobby.Contracts` surface-i üçün helper və
adapter verir; yeni operation capability yaratmır.

ViewModel-lər və plugin-lər UI thread-də ad-hoc blocking work və ya sərbəst
`Task.Run` orchestration yaratmır.

Minimum operation snapshot:

- `operationId`
- `ownerKind` (`host`, `plugin`)
- `ownerId` (plugin id varsa)
- `displayNameKey` və optional short detail key
- `status` (`queued`, `running`, `cancelling`, `completed`, `failed`)
- `progress` (`0..100` və ya indeterminate)
- `canCancel`
- `correlationId`
- redacted typed failure reason

### 1a. P1 public operation contract shape

Background operation reporting contract `Dobby.Contracts` altında aşağıdakı public
names ilə bağlanır. Host implementation queue/orchestration `Dobby.Application`
altında qalır; plugin yalnız snapshot/progress/cancellation contract-larını görür.

```csharp
public enum BackgroundOperationOwnerKind
{
    Host,
    Plugin
}

public enum BackgroundOperationStatus
{
    Queued,
    Running,
    Cancelling,
    Completed,
    Failed,
    Cancelled
}

public sealed record BackgroundOperationProgress(
    int? Percent,
    bool IsIndeterminate);

public sealed record BackgroundOperationSnapshot(
    Guid OperationId,
    BackgroundOperationOwnerKind OwnerKind,
    string? OwnerId,
    string DisplayNameKey,
    string? ShortDetailKey,
    BackgroundOperationStatus Status,
    BackgroundOperationProgress Progress,
    bool CanCancel,
    Guid CorrelationId,
    ErrorCode? FailureReason);

public interface IBackgroundOperationReporter
{
    IProgress<BackgroundOperationSnapshot> Progress { get; }
}

public interface IBackgroundOperationCancellation
{
    CancellationToken CancellationToken { get; }
}
```

`Percent` yalnız `0..100` aralığında ola bilər; indeterminate progress üçün
`Percent = null` və `IsIndeterminate = true` istifadə olunur. `OwnerId` plugin
operation-larında plugin id, host operation-larında `null` ola bilər. `FailureReason`
raw exception deyil, `04_RESULT_VS_EXCEPTION_POLICY.md` içindəki `ErrorCode`
vocabulary-sinə bağlı typed reason-dur.

### 2. Shell rendering

Shell bottom status/progress sətri operation snapshot-larını render edir. Plugin
arbitrary UI yerləşdirmir; yalnız contract üzərindən progress/status təklif edir.
Notification taxonomy (`toast`, `banner`, `modal`) `docs/specs/shell-ui.md`-də qalır.

### 3. Cancellation və failure

Cancellation `CancellationToken` ilə cooperative olur. `cancelling` state user-a
görünə bilər, amma operation owner nəticəni `completed`, `failed` və ya
cancelled typed result kimi bağlamalıdır. Raw exception stack trace UI-a çıxmır;
diagnostics correlation id ilə saxlayır.

### 4. Startup və login-first boundary

Pre-login heavy background work başlamır. `IHostedService` və channel/queue
orchestration post-login açılır və `ADR-0041` login-first policy-ni pozmur.
Plugin activation, plugin install, license import və storage operation-ları login
screen first paint-i bloklamamalıdır.

### 5. Work sessions ilə sərhəd

Background operation progress work-session state-inin əvəzi deyil. Work session
explicit minimize/restore UI state-i `ADR-0050` və `docs/specs/work-sessions.md`
ilə idarə olunur. Background operation snapshot yalnız status/progress səthidir.

## Consequences

- Shell UI skeleton bottom progress/status modelini deterministic yarada bilər.
- Skeleton plugin və SDK layihələri operation/progress tiplərini `Dobby.Contracts`
  üzərindən görür; `Dobby.Application` implementation project-i plugin-facing
  reference olmur.
- AC-046 UI responsiveness gate-i operation contract və dispatcher heartbeat ilə
  test edilə bilər.
- Plugin-lər host measurement və diagnostics boundary-sini bypass edən ayrı
  telemetry/log/progress channel yaratmır.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `Background Operation Sərhədi`
- `docs/specs/shell-ui.md` — bottom status/progress və notification taxonomy
- `docs/specs/work-sessions.md`
- `docs/adr/ADR-0041-login-first-startup-policy.md`
- `docs/adr/ADR-0050-in-memory-work-sessions.md`
- `docs/specs/audit-and-diagnostics.md`
