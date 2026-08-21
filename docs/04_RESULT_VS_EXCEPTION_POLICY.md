# Result vs Exception Policy

**Tarix:** 2026-04-30
**Status:** P1 canonical policy — applies from skeleton commit 1
**Scope:** `Result<T>` vs exception boundary, cancellation mapping, plugin exception sealing və logging/correlation expectations

---

## Məqsəd

Dobby codebase-ində hansı failure-ın `Result<T>` ilə, hansının exception ilə ifadə olunacağı və hər sərhəddə hansı davranışın gözləniləcəyi bir yerdə təsbit olunur. Policy skeleton implementation-dan əvvəl bağlanır ki, `Result<T>` və exception stili ilk commit-dən qarışmasın və plugin boundary sızmasının qarşısı öncədən alınsın.

Policy arxitektura xəritəsinin `Core Public Surface Stability` convention-ı ilə uyğundur: `Result<T>` plugin-facing stabil tipdir və həmin stabillik hər sərhəddə qorunmalıdır.

---

## Əsas prinsip — Semantic

Qərar kriteriyası: **failure-in mahiyyəti** nə tip ifadə istifadə olunacağına qərar verir. Layer və ya accessibility modifier deyil. Bu yanaşma .NET idiom-larına (`Try*` pattern, `ArgumentException`, `OperationCanceledException`), Rust `Result<T, E>`-ə və Go error return-lərinə uyğundur.

---

## Qaydalar

| # | Qayda | Kateqoriya |
|---|---|---|
| 1 | Expected / domain / business / validation failure → `Result<T>` | Semantic |
| 2 | Programmer / invariant / contract violation → exception | Semantic |
| 3 | Infrastructure failure (IO, HTTP, DB, serialization) → ilk Dobby boundary-də log + `Result<T>` | Boundary |
| 4 | Plugin boundary → raw exception keçmir; typed error code-a çevrilir | Boundary |
| 5 | `Result<T>` qaytaran public metod unexpected exception sızdırmır (istisnalar: #6 cancellation, #7 fatal) | Contract |
| 6 | Cancellation: internal flow-da throw/bubble OK; public/plugin boundary-də `ErrorCode.Cancelled` ilə `Result<T>` | Boundary |
| 7 | Fatal / corrupted-state exception (`OutOfMemoryException`, `StackOverflowException`) tutulmur | Hard rule |
| 8 | Host UI/page boundary non-fatal exception-ları process crash-a çevirmir; user safe display message görür, raw stack trace UI-a sızmır və detail diagnostics/audit boundary-də qalır | Boundary |

---

Host UI/page boundary-də non-fatal exception raw UI detail deyil. Shell və built-in page-lər expected failure-ları `Result<T>`/typed state ilə göstərir; unexpected non-fatal exception halında user non-technical error message görür, raw stack trace və payload detail yalnız diagnostics/audit boundary daxilində qalır. Fatal/corrupted-state exception-lar qayda #7-yə uyğun tutulmur.

## "Public metod" tərifi

Qayda #5-dəki "public metod" — **cross-project API contract**. Yəni `Dobby.X` project-indən `Dobby.Y` project-inə çağrılan metod. Eyni project daxilindəki `private` / `internal` `Result<T>`-qaytaran helper-lər də policy-ə tabedir, amma pozuntu local olaraq aşkarlanır və fix sərhədi bir project ilə məhdudlaşır. Cross-project sərhəddə policy pozuntusu contract regressiondur.

---

## Cancellation modeli — canonical vs helper

Canonical model `Result<T>`-nin mövcud iki halı üzərində qalır (`Success | Failure`). Cancellation ayrıca variant deyil, well-known error code-dur:

```csharp
// Canonical
Result.Failure<T>(ErrorCode.Cancelled)

// Optional ergonomic helper
Result.Cancelled<T>()  // sadəcə Failure(Cancelled) yaradır
```

Three-way discriminated union (`Success | Failure | Cancelled`) **rədd olunur**:

- Hər consumer-də üçüncü `match` branch-i yaradar
- `Dobby.Core/Error Codes` registry-si artıq bu məqsəd üçün var
- UI/page-state mapping `ErrorCode.Cancelled` ilə daha sadədir
- Plugin boundary-də cancellation digər typed failure-lar kimi standart axına düşür

---

## Plugin exception → ErrorCode mapping

Plugin-in host-a throw etdiyi exception-lar boundary wrapper tərəfindən aşağıdakı cədvələ görə typed error code-lara çevrilir:

| Exception tipi | ErrorCode |
|---|---|
| `OperationCanceledException` | `Cancelled` |
| `ArgumentException`, `ArgumentNullException` | `InvalidInput` |
| `NotSupportedException` | `UnsupportedOperation` |
| (digər `Exception`) | `PluginInternalError` |

Bu **lenient** mapping-dir: exception tipi semantic niyyəti qoruyur. Strict alternative (hər şeyi `PluginInternalError` etmək) rədd olunur — çünki plugin failure-in səbəbini host observability-dən gizlədir və root-cause analysis-i çətinləşdirir.

**Qeyd:** Fatal exception (`OutOfMemoryException`, `StackOverflowException`) bu cədvələ düşmür — wrapper onları tutmur, proses dayanır (qayda #7).

Plugin boundary-də `PluginInternalError`, `InvalidInput`, `UnsupportedOperation` və
`Cancelled` kimi nəticələr host process failure-a çevrilmir. `ADR-0012`-yə uyğun
olaraq host həmin plugin instance/screen/task sərhədini `Faulted` və ya uyğun
typed state kimi işarələyir, user-facing mesajı non-technical saxlayır və raw
exception detail-i yalnız diagnostics/audit boundary daxilində saxlayır.

---

## Infrastructure boundary pattern

IO/DB/HTTP/serialization edən **ilk Dobby metodu** `try/catch` ilə sərhəd qurur və exception-ı `Result<T>`-ə çevirir. Downstream Dobby metodları artıq wrapped Result consume edir, təkrar `try/catch` yazmır.

```csharp
public async Task<Result<User>> GetUserAsync(Guid id, CancellationToken ct)
{
    try
    {
        var user = await _db.QueryAsync<User>("...", new { id }, ct);
        return user is null
            ? Result.Failure<User>(ErrorCode.NotFound)
            : Result.Success(user);
    }
    catch (OperationCanceledException)
    {
        throw; // internal bubble — qayda #6
    }
    catch (SqlException ex)
    {
        _logger.LogWarning(ex, "DB unavailable {CorrelationId}", _correlation.Id);
        return Result.Failure<User>(ErrorCode.DatabaseUnavailable);
    }
}
```

Diqqət: `OperationCanceledException` bu daxili metod səviyyəsində bubble olunur (caller-a çatır). Public/plugin boundary-yə çatanda wrapper onu `ErrorCode.Cancelled`-a çevirir.

---

## Nümunələr

### Expected failure → Result

```csharp
public Result<Order> Submit(Order order)
{
    if (_cart.IsEmpty)
        return Result.Failure<Order>(ErrorCode.CartEmpty);

    if (!_inventory.HasStock(order))
        return Result.Failure<Order>(ErrorCode.OutOfStock);

    return Result.Success(_repo.Save(order));
}
```

### Programmer error → exception

```csharp
public User Resolve(Guid id)
{
    if (id == Guid.Empty)
        throw new ArgumentException("User id is required", nameof(id));

    // ...
}
```

### Plugin boundary wrapper

```csharp
public async Task<Result<T>> InvokePluginAsync<T>(
    Func<Task<Result<T>>> pluginCall,
    string pluginId,
    CancellationToken ct)
{
    try
    {
        return await pluginCall();
    }
    catch (OperationCanceledException)
    {
        return Result.Failure<T>(ErrorCode.Cancelled);
    }
    catch (ArgumentException ex)
    {
        _logger.LogWarning(ex, "Plugin argument error {PluginId} {CorrelationId}",
            pluginId, _correlation.Id);
        return Result.Failure<T>(ErrorCode.InvalidInput);
    }
    catch (NotSupportedException ex)
    {
        _logger.LogWarning(ex, "Plugin unsupported operation {PluginId} {CorrelationId}",
            pluginId, _correlation.Id);
        return Result.Failure<T>(ErrorCode.UnsupportedOperation);
    }
    catch (Exception ex) when (!IsFatal(ex))
    {
        _logger.LogError(ex, "Plugin threw unexpectedly {PluginId} {CorrelationId}",
            pluginId, _correlation.Id);
        return Result.Failure<T>(ErrorCode.PluginInternalError);
    }
    // Fatal exception (OutOfMemoryException, StackOverflowException) `when` filter ilə
    // catch-ə düşmür; filter false qaytarır, exception bubble-up davam edir, proses dayanır.
}

private static bool IsFatal(Exception ex) =>
    ex is OutOfMemoryException or StackOverflowException;
```

---

## Phase 1 public Result contract and well-known error codes

P1 skeleton-dan əvvəl `Result<T>` və `ErrorCode` public surface-i aşağıdakı kimi
freeze edilir. Implementation daha ergonomic factory/helper-lər əlavə edə bilər,
amma bu public names və semantic shape-i dəyişdirə bilməz.

| Contract item | Canonical shape |
|---|---|
| Namespace ownership | `Dobby.Core / Result Standardization` və `Dobby.Core / Error Codes` |
| Generic result type | `Result<T>` |
| Success state | `IsSuccess = true`, `Value` mövcuddur, `ErrorCode` yoxdur |
| Failure state | `IsSuccess = false`, `ErrorCode` mövcuddur, `Value` istifadə olunmur |
| Error code type | `ErrorCode` stable string value daşıyan typed code-dur; UI display localization key-lərindən gəlir |
| Factory names | `Result.Success<T>(T value)` və `Result.Failure<T>(ErrorCode code)` canonical helper adlarıdır |
| Cancellation | `Result.Failure<T>(ErrorCode.Cancelled)`, ayrıca third state yoxdur |

`Dobby.Core/ErrorCodes` baseline registry-si:

**Control flow**
- `Cancelled` — cooperative cancellation (internal bubble; boundary-də Result)

**Plugin boundary**
- `PluginInternalError` — plugin unhandled exception (fallback)
- `InvalidInput` — invalid caller argument at plugin/host boundary
- `UnsupportedOperation` — requested operation not supported by implementation

**Infrastructure**
- `DatabaseUnavailable` — DB connection/query failure
- `NetworkUnavailable` — HTTP/network call failure
- `SerializationFailed` — JSON/binary parse or write failure
- `IoFailed` — file system / stream IO failure

**Domain**
- `NotFound` — referenced entity does not exist
- `AlreadyExists` — uniqueness conflict
- `ValidationFailed` — input did not pass validation rules
- `Unauthorized` — caller is not authenticated
- `Forbidden` — caller is authenticated but lacks capability

**Licensing / authorization**
- `LicenseDenied` — plugin license runtime access-ə icazə vermir
- `LicenseReadOnly` — əməliyyat expired plugin read-only mode-da olduğu üçün deny edilir
- `LicensePending` — license artefaktı valid-dir, amma `notBefore > now`
- `LicenseBindingMismatch` — customer, deployment, device və ya package binding uyğun gəlmir

---

## Cross-references

- **Architecture map:**
  - Row 4 `Result Standardization` (`Dobby.Core`)
  - Row 5 `Error Codes` (`Dobby.Core`)
  - Row 66 `Logging` (`Dobby.Diagnostics`)
  - Engineering Conventions: `Error Handling Contract`, `Plugin Boundary Exception Seal`, `Cancellation and Corrupted-State`, `Core Public Surface Stability`
- **ADRs:**
  - ADR-0016 Logging strategy (exception flow + correlation id)
- **Stack selection:**
  - Section 7 Dobby-owned primitives: `Result<T>` in `Dobby.Core/ResultStandardization`
- **Acceptance / requirement sənədləri:**
  - `docs/05_FUNCTIONAL_REQUIREMENTS.md` — plugin-facing və host-facing failure davranışının hansı use-case-lərdə göründüyü
  - `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — boundary, isolation və reliability tələbləri
  - `docs/07_ACCEPTANCE_CRITERIA.md` — Phase 1 boundary acceptance yoxlamaları

## Branch Shared Failure Vocabulary Addendum

`ADR-0056` və `ADR-0057` exact result code spelling-i bu sənədə qaytarır.
Branch Shared draft-ları ən azı bu failure category-ləri ayırmalıdır:

- Branch Shared server unavailable;
- server identity / TLS pin mismatch;
- wrong `branchDeploymentId`;
- schema or grant mismatch;
- device not provisioned / registration missing;
- runtime adapter missing in a configuration that asks for BranchShared;
- setup credential unavailable or forbidden in normal `Dobby.Host` runtime;
- PostgreSQL unreachable before canonical pre-auth audit INSERT.

Bu addendum category ownership-i saxlayır. Exact code identifier-ları `ErrorCode`
registry edit-i ilə eyni public-contract freeze pass-da bağlanmalıdır; skeleton
agent category-lərdən fərqli ad-hoc Branch Shared error code-ları yaratmamalıdır.

Initial P1 skeleton `Dobby.Core/ErrorCodes` registry-si yuxarıdakı Branch Shared
category-lər üçün exact identifier rezerv etmir. Branch Shared error identifiers
future Tier B/runtime pass-da, Branch Shared adapter/runtime scope explicit
açılanda public-contract freeze ilə birlikdə əlavə olunur. Bu deferral
`docs/09_BUILD_RUN_AND_RELEASE.md` `Initial P1 Skeleton Exclusions` siyahısı ilə
birlikdə oxunmalıdır.

## 2026-08 Error and Atomicity Amendment (Controlling)

- Expected validation, authorization, compatibility, lifecycle, cancellation
  and user-correctable package failures return typed `Result` failures. They do
  not increment plugin runtime-fault counters or trigger quarantine unless the
  failure is an actual plugin defect/fault.
- Persistence and infrastructure exceptions are translated at the owning
  boundary into stable typed codes plus a correlation ID. UI, audit and exported
  support material do not expose raw database paths, SQL, secrets, stack traces
  or exception messages.
- Security/domain state and the required audit/outbox record commit in one unit
  of work. A failed commit returns failure and leaves neither side logically
  applied. Post-commit external delivery is retryable and idempotent.
- Cancellation remains cancellation; it is not converted to an error or runtime
  fault. Cleanup failures may be logged under a separate correlation ID without
  masking the original outcome.
- Cryptographic, archive-limit, containment, malformed-persisted-state and
  overflow failures are typed fail-closed results at trust boundaries.
