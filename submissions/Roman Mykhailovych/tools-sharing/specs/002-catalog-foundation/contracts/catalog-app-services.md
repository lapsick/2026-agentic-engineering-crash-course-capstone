# Catalog Application Services (Tier 2 — module-internal)

**Requirements**: FR-001 … FR-010 | **Consumed by**: `ToolShare.Catalog.Blazor` only

Assembly: `ToolShare.Catalog.Application.Contracts`. These interfaces are the UI's contract with the module. **No other module may reference them** — cross-module consumers use [catalog-public-contracts.md](./catalog-public-contracts.md) instead.

Common behavior for every operation below:
- Authorization per [catalog-permissions.md](./catalog-permissions.md); unauthenticated callers are rejected before any handler runs (FR-011).
- Validation failures raise `AbpValidationException`; rule violations raise `BusinessException` with a localized `Catalog:*` code (FR-009).
- Update DTOs carry `ConcurrencyStamp`; a stale stamp raises `AbpDbConcurrencyException`, surfaced by the UI as a reload-and-retry conflict message (FR-010).
- All list results are `PagedResultDto<T>` with default `MaxResultCount` 10 and hard cap 100.

---

## `ICategoryAppService`

```csharp
public interface ICategoryAppService : IApplicationService
{
    Task<CategoryDto> GetAsync(Guid id);
    Task<PagedResultDto<CategoryDto>> GetListAsync(GetCategoryListInput input);
    Task<ListResultDto<CategoryLookupDto>> GetLookupAsync();       // for filter/select controls
    Task<CategoryDto> CreateAsync(CreateCategoryDto input);
    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryDto input);
    Task DeleteAsync(Guid id);
}
```

| DTO | Members |
|---|---|
| `CategoryDto : AuditedEntityDto<Guid>` | `Name`, `Description`, `ToolCount`, `ConcurrencyStamp` |
| `CategoryLookupDto : EntityDto<Guid>` | `Name` |
| `GetCategoryListInput : PagedAndSortedResultRequestDto` | `Filter` (name contains, normalized) |
| `CreateCategoryDto` | `Name` (required, 2–128), `Description` (≤ 512) |
| `UpdateCategoryDto` | `Name`, `Description`, `ConcurrencyStamp` (required) |

**Rules** — `CreateAsync`/`UpdateAsync` enforce `CR-01`–`CR-03`; a duplicate name raises `Catalog:CategoryNameAlreadyExists`. `DeleteAsync` enforces `CR-04` and raises `Catalog:CategoryHasTools` when tools are still assigned (FR-001). `ToolCount` lets the UI disable Delete before the user tries.

---

## `IToolAppService`

```csharp
public interface IToolAppService : IApplicationService
{
    Task<ToolDetailDto> GetAsync(Guid id);
    Task<PagedResultDto<ToolListItemDto>> GetListAsync(GetToolListInput input);
    Task<ToolDto> CreateAsync(CreateToolDto input);
    Task<ToolDto> UpdateAsync(Guid id, UpdateToolDto input);
    Task DeleteAsync(Guid id);
}
```

| DTO | Members |
|---|---|
| `ToolDto : AuditedEntityDto<Guid>` | `Name`, `Description`, `CategoryId`, `CategoryName`, `ConcurrencyStamp` |
| `ToolListItemDto : EntityDto<Guid>` | `Name`, `CategoryId`, `CategoryName`, `InstanceCount`, `AvailableInstanceCount`, `PrimaryPhotoUrl?` |
| `ToolDetailDto : ToolDto` | `Instances` → `List<ToolInstanceDto>` |
| `CreateToolDto` | `Name` (required, 2–256), `Description` (≤ 2048), `CategoryId` (required) |
| `UpdateToolDto` | same + `ConcurrencyStamp` (required) |

### `GetToolListInput : PagedAndSortedResultRequestDto` (FR-007)

| Field | Effect |
|---|---|
| `Filter` | Case-insensitive, accent-tolerant **contains** match on the tool's normalized name (SC-008) |
| `CategoryId` | Restrict to one category |
| `OnlyAvailable` | Only tools having ≥ 1 available instance |
| `IncludeRetiredInstances` | Default `false` — retired instances are excluded from the two count columns (FR-006) |

`Sorting` accepts `Name`, `CategoryName`, `CreationTime`; default `Name ASC`. An unrecognized value falls back to the default. An empty result is a normal `PagedResultDto` with `TotalCount == 0` — the UI renders an empty state, never an error (FR-008).

**Rules** — `CategoryId` must exist (`TR-02`). `DeleteAsync` enforces `TR-04` and raises `Catalog:ToolHasInstances`.

---

## `IToolInstanceAppService`

```csharp
public interface IToolInstanceAppService : IApplicationService
{
    Task<ToolInstanceDetailDto> GetAsync(Guid id);
    Task<ListResultDto<ToolInstanceDto>> GetListByToolAsync(Guid toolId, bool includeRetired = false);

    Task<ToolInstanceDto> CreateAsync(CreateToolInstanceDto input);
    Task<ToolInstanceDto> UpdateAsync(Guid id, UpdateToolInstanceDto input);

    Task<ToolInstanceDto> ChangeConditionAsync(Guid id, ChangeToolInstanceConditionDto input);
    Task<ToolInstanceDto> RetireAsync(Guid id, RetireToolInstanceDto input);

    Task<ListResultDto<ToolInstanceStateChangeDto>> GetHistoryAsync(Guid id);

    Task<ToolInstancePhotoDto> AddPhotoAsync(Guid id, AddToolInstancePhotoDto input);
    Task DeletePhotoAsync(Guid id, Guid photoId);
    Task SetPrimaryPhotoAsync(Guid id, Guid photoId);
    Task<RemoteStreamContent> GetPhotoAsync(Guid id, Guid photoId);
}
```

| DTO | Members |
|---|---|
| `ToolInstanceDto : AuditedEntityDto<Guid>` | `ToolId`, `SerialNumber`, `Condition`, `CirculationState`, `IsAvailable`, `RetirementReason?`, `RetiredAt?`, `Notes?`, `PrimaryPhotoId?`, `ConcurrencyStamp` |
| `ToolInstanceDetailDto : ToolInstanceDto` | `ToolName`, `CategoryId`, `CategoryName`, `Photos` → `List<ToolInstancePhotoDto>`, `History` → `List<ToolInstanceStateChangeDto>` |
| `ToolInstancePhotoDto : EntityDto<Guid>` | `FileName`, `ContentType`, `SizeBytes`, `DisplayOrder`, `IsPrimary` |
| `ToolInstanceStateChangeDto : EntityDto<Guid>` | `PreviousCondition?`, `NewCondition`, `PreviousCirculationState?`, `NewCirculationState`, `Reason?`, `ChangedAt`, `ChangedByUserId?`, `ChangedByUserName?` |
| `CreateToolInstanceDto` | `ToolId` (required), `SerialNumber` (required, 1–64, `IR-01` pattern), `Condition` (required), `Notes?` |
| `UpdateToolInstanceDto` | `SerialNumber`, `Notes?`, `ConcurrencyStamp` (required) — **condition is not editable here**; use `ChangeConditionAsync` so history is always written |
| `ChangeToolInstanceConditionDto` | `Condition` (required), `Reason?` (≤ 512), `ConcurrencyStamp` (required) |
| `RetireToolInstanceDto` | `Reason` (**required**, 1–512), `ConcurrencyStamp` (required) |
| `AddToolInstancePhotoDto` | `File` → `IRemoteStreamContent` (`FileName`, `ContentType`, `Length`) |

**Rules**
- `CreateAsync` enforces `IR-01`–`IR-03`; duplicate serial → `Catalog:SerialNumberAlreadyExists` (FR-003, SC-004). The database unique index is the authority under concurrent registration.
- `ChangeConditionAsync` enforces `IR-04` (`Catalog:InstanceIsRetired`, `Catalog:ConditionUnchanged`).
- `RetireAsync` enforces `IR-05` (`Catalog:InstanceAlreadyRetired`); it never deletes (`IR-06`, FR-006).
- Each of the three mutating operations appends one history row and raises one `ToolInstanceStateChangedEto` (`IR-07`).
- `GetHistoryAsync` returns rows ascending by `ChangedAt` (`HR-03`); there is **no** update or delete operation for history (`HR-01`, Constitution IV).
- Photo operations enforce `PR-01`, `PR-02`, `IR-08` → `Catalog:UnsupportedPhotoFormat`, `Catalog:PhotoTooLarge`, `Catalog:TooManyPhotos` (FR-005, FR-009). `GetPhotoAsync` streams from BlobStoring and is available to any authenticated user.

---

## Error codes

All codes are localized in `ToolShare.Catalog.Domain.Shared/Localization/Catalog/en.json`.

| Code | Raised by | Meaning |
|---|---|---|
| `Catalog:CategoryNameAlreadyExists` | `CR-03` | Another category already uses this name |
| `Catalog:CategoryHasTools` | `CR-04` | Cannot delete — tools are still assigned |
| `Catalog:ToolHasInstances` | `TR-04` | Cannot delete — instances still exist |
| `Catalog:InvalidSerialNumber` | `IR-01` | Serial number is empty, too long, or has disallowed characters |
| `Catalog:SerialNumberAlreadyExists` | `IR-02` | Serial number is already used in the catalog |
| `Catalog:InstanceIsRetired` | `IR-04` | Cannot change the condition of a retired instance |
| `Catalog:ConditionUnchanged` | `IR-04` | The new condition equals the current one |
| `Catalog:InstanceAlreadyRetired` | `IR-05` | Instance is already retired |
| `Catalog:RetirementReasonRequired` | `IR-05` | Retirement requires a stated reason |
| `Catalog:UnsupportedPhotoFormat` | `PR-01` | Content type is not in the allowed list |
| `Catalog:PhotoTooLarge` | `PR-02` | Photo exceeds `MaxSizeBytes` |
| `Catalog:TooManyPhotos` | `IR-08` | Instance already holds `MaxPerInstance` photos |
