# ADR-0003: Plugin Storage Boundary and Access Model

**Status:** Accepted
**Date:** 2026-04-30

## Context

ADR-0030 storage taxonomy-ni müəyyən edir və plugin-visible iki compartment-i adlandırır: `SharedDomainStorage` və `UserScopedStorage`. `Dobby.Contracts` və `Dobby.SDK` təhlükəsiz yaradılsın deyə contract method surface, async davranış, key/value forması, atomicity və key listing semantics ayrıca bağlanmalıdır.

## Decision

Phase 1 plugin storage host-controlled async key/value contract-ları ilə açılır. Plugin-lər backing file path, database connection, SQL handle, migration handle, host persistence entity, provider details və key material almır.

Plugin storage contract tiplərinin canonical namespace-i
`Dobby.Contracts.PluginStorage`-dır.

### 1. Compartments

Plugin-visible storage service yalnız iki compartment təqdim edir:

- `SharedDomainStorage` — customer/deployment scoped plugin domain data üçün.
- `UserScopedStorage` — current-user scoped drafts, preferences və UI state üçün.

Default compartment yoxdur. Hər write iki compartment-dən birini explicit seçməlidir.

### 2. Contract shape

Phase 1 contract surface `Result<T>`/`Result` formasındadır:

```csharp
public interface IPluginStorage
{
    IPluginStorageCompartment SharedDomainStorage { get; }
    IPluginStorageCompartment UserScopedStorage { get; }
}

public interface IPluginStorageCompartment
{
    ValueTask<Result<PluginStorageValue?>> GetAsync(PluginStorageKey key, CancellationToken cancellationToken = default);
    ValueTask<Result> SetAsync(PluginStorageKey key, PluginStorageValue value, CancellationToken cancellationToken = default);
    ValueTask<Result<bool>> DeleteAsync(PluginStorageKey key, CancellationToken cancellationToken = default);
    ValueTask<Result<bool>> ExistsAsync(PluginStorageKey key, CancellationToken cancellationToken = default);
    ValueTask<Result<IReadOnlyList<PluginStorageKey>>> ListKeysAsync(PluginStorageKeyPrefix prefix, CancellationToken cancellationToken = default);
}
```

Plugin-facing storage metodları `Result<T>`/`Result` qaytarır; expected failure-lar, denied access, infrastructure failures və cancellation typed result kimi ifadə olunur. Cancellation bu boundary-də `ErrorCode.Cancelled`-ə map edilir və raw cancellation exception plugin boundary-dən çölə çıxmamalıdır.

`ListKeysAsync` Phase 1 üçün finite key list qaytarır. Streaming və paging lazım olarsa gələcək scope kimi ayrıca qərarla bağlana bilər.

`PluginStorageKey`, `PluginStorageKeyPrefix` və `PluginStorageValue` `Dobby.Contracts` daxilində contract value object-lərdir. SDK typed JSON helper-ləri bu metodları wrap edə bilər, amma SDK ayrıca storage capability yaratmamalıdır.

### 3. Key rules

- Key-lər path deyil, plugin-local logical key-dir.
- Key-lər validation və normalization-dan sonra stable UTF-8 string olmalıdır.
- Empty key, absolute path, relative path traversal və host-reserved prefix-lər rədd edilir.
- `ListKeysAsync` yalnız calling plugin və compartment-ə aid key-ləri qaytarır.
- `ListKeysAsync` Phase 1 üçün key-ləri deterministic ordinal key order ilə qaytarır; testlər və SDK helper-ləri provider/insertion order-dən asılı qalmamalıdır.
- Plugin-lər `pluginId`, `userId`, `customerId` və ya `deploymentId` seçə və spoof edə bilməz; bu dəyərlər host context tərəfindən bind edilir.

### 4. Value rules

- Value-lar opaque serialized payload-lardır; host onları plugin contract səviyyəsində domain object kimi interpret etmir.
- `PluginStorageValue` payload bytes, optional content type və optional payload schema version daşıyır; bu schema version not a concurrency token.
- Host size metadata-nı daxildə compute/store edə bilər; size metadata plugin-facing contract capability deyil.
- SDK JSON helper-ləri eyni value object üzərində `System.Text.Json` istifadə edir.
- Secret material yenə ADR-0055 protected-storage scope qaydalarına tabedir.

### 5. Async and atomicity

- Bütün storage metodları async-dir.
- `SetAsync` single key üçün atomic-dir: crash və ya cancellation sonrası stored value ya əvvəlki complete value, ya da yeni complete value olmalıdır.
- `DeleteAsync` idempotent-dir və deletion-dan əvvəl value-nun mövcud olub-olmadığını qaytarır.
- Multi-key transaction API-ləri Phase 1 plugin storage scope-una daxil deyil.
- Cancellation commit-dən əvvəl işi dayandıra bilər, amma partial payload visible qalmamalıdır.

### 6. Authorization and audit boundary

Host compartment metodu persistence-ə toxunmazdan əvvəl access-i gate edir. Denied storage access göstərilən `Result<T>`/`Result` contract-ı üzrə typed failure qaytarır və owning feature spec tələb edirsə relevant audit event emit edir. Technical storage failure-lar diagnostics-ə gedir, audit-ə yox; yalnız security-relevant denial olduqda audit boundary-yə düşür.

## Consequences

- `Dobby.Contracts` plugin storage contract-larını method name təxmin etmədən declare edə bilər.
- `Dobby.SDK` yeni capability owner olmadan convenience wrapper-lər əlavə edə bilər.
- Host persistence `Dobby.Infrastructure` daxilində internal qalır.
- Plugin storage contract testləri compartment isolation, key validation, async cancellation və single-key atomicity davranışını yoxlaya bilər.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `Plugin Storage Contracts` row
- `docs/02_ARCHITECTURE_MAP.md` — `Plugin Storage Contracts` risk row
- `docs/02_ARCHITECTURE_MAP.md` — `Storage Sərhədi` convention
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md`
- `docs/specs/storage-and-data-protection.md`
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md`
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md`

## Mode-Aware Storage Amendment

`ADR-0056` does not change plugin-facing storage contracts. Branch Shared mode
still exposes only host-owned plugin storage compartments; plugin packages do
not carry host persistence migrations and plugin manifests do not receive a
`StorageScope` field.

Branch Shared plugin shared key/value data may be physically stored in
PostgreSQL by the host, but the plugin contract remains single-key atomic
key/value semantics. Plugin-owned relational schemas, plugin migrations and
CAS/versioned write semantics remain deferred.
