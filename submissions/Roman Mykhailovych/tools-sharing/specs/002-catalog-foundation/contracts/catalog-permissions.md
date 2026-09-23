# Catalog Permission Boundary

**Requirements**: FR-011, FR-012, FR-013 | **Verified by**: SC-003, SC-009
**Defined in**: `ToolShare.Catalog.Application.Contracts` (`CatalogPermissions`, `CatalogPermissionDefinitionProvider`)

Authentication and authorization use ABP's built-in `IdentityModule` and permission system — no hand-rolled auth (Constitution, Technology & Architecture Constraints).

---

## Permission tree

```text
Catalog                                  (group)
├── Catalog.Categories                    view/manage categories page
│   ├── Catalog.Categories.Create
│   ├── Catalog.Categories.Edit
│   └── Catalog.Categories.Delete
├── Catalog.Tools                         manage tools
│   ├── Catalog.Tools.Create
│   ├── Catalog.Tools.Edit
│   └── Catalog.Tools.Delete
└── Catalog.ToolInstances                 manage instances
    ├── Catalog.ToolInstances.Create
    ├── Catalog.ToolInstances.Edit
    ├── Catalog.ToolInstances.ChangeCondition
    ├── Catalog.ToolInstances.Retire
    └── Catalog.ToolInstances.ManagePhotos
```

```csharp
public static class CatalogPermissions
{
    public const string GroupName = "Catalog";

    public static class Categories
    {
        public const string Default = GroupName + ".Categories";
        public const string Create  = Default + ".Create";
        public const string Edit    = Default + ".Edit";
        public const string Delete  = Default + ".Delete";
    }
    // Tools, ToolInstances follow the same shape.
}
```

Constants are `public const string` in `Catalog.Application.Contracts` so the host's role seeder — and any future admin UI — can grant them without magic strings.

---

## Browsing vs. managing

**Browsing is not permission-gated; it is authentication-gated.** Any signed-in user may read the catalog, so read operations carry a bare `[Authorize]`. This is deliberate: the spec defers member management to the Membership feature, and requiring a "browse" permission would make every seeded user unable to see anything until an admin granted it.

> **Superseded by 003 (Membership & Community Rules).** The statement above was
> correct only until the Membership feature existed. From 003 onward, browsing —
> like every other application-service call — is additionally refused unless the
> caller is an **enrolled, Active member**, regardless of authentication or
> permissions (FR-006, FR-006a; see
> [003's permission contract](../../003-membership-rules/contracts/membership-permissions.md#the-enrolment-gate-fr-006-fr-006a)).
> This is enforced by a single cross-cutting decorator
> (`MembershipMethodInvocationAuthorizationService`) registered once for the
> whole application, not by any change to the `[Authorize]` attributes in the
> table below — so the table's *permission* requirements below are still
> accurate, it is just no longer sufficient on its own to be merely
> authenticated. The replacement rule: **browsing is not permission-gated; it is
> membership-gated.** 002's own authorization suite
> (`test/ToolShare.Catalog.Application.Tests/Authorization/`) was extended with
> the "authenticated but not enrolled" case once the gate shipped.

| Operation | Requirement |
|---|---|
| `IToolAppService.GetListAsync` / `GetAsync` | `[Authorize]` — any authenticated user |
| `ICategoryAppService.GetListAsync` / `GetAsync` / `GetLookupAsync` | `[Authorize]` |
| `IToolInstanceAppService.GetAsync` / `GetListByToolAsync` / `GetHistoryAsync` / `GetPhotoAsync` | `[Authorize]` |
| `IToolInstanceLookupAppService.*` | `[Authorize]` — the public contract is read-only |
| `ICategoryAppService.CreateAsync` | `Catalog.Categories.Create` |
| `ICategoryAppService.UpdateAsync` | `Catalog.Categories.Edit` |
| `ICategoryAppService.DeleteAsync` | `Catalog.Categories.Delete` |
| `IToolAppService.CreateAsync` | `Catalog.Tools.Create` |
| `IToolAppService.UpdateAsync` | `Catalog.Tools.Edit` |
| `IToolAppService.DeleteAsync` | `Catalog.Tools.Delete` |
| `IToolInstanceAppService.CreateAsync` | `Catalog.ToolInstances.Create` |
| `IToolInstanceAppService.UpdateAsync` | `Catalog.ToolInstances.Edit` |
| `IToolInstanceAppService.ChangeConditionAsync` | `Catalog.ToolInstances.ChangeCondition` |
| `IToolInstanceAppService.RetireAsync` | `Catalog.ToolInstances.Retire` |
| `IToolInstanceAppService.AddPhotoAsync` / `DeletePhotoAsync` / `SetPrimaryPhotoAsync` | `Catalog.ToolInstances.ManagePhotos` |

Attributes go on the **implementation class/methods** in `ToolShare.Catalog.Application`. `ToolShare.Catalog.Blazor` additionally hides unavailable actions via `IAuthorizationService`/`AuthorizeView` — but hiding is a UX affordance, never the enforcement: SC-003 is satisfied by the server-side attributes alone.

---

## Roles and bootstrap (FR-013, SC-009)

| Principal | Source | Catalog grants |
|---|---|---|
| `admin` user | ABP template `IdentityDataSeedContributor` (existing) | Member of `admin` |
| `admin` role | ABP template (existing) | **All** permissions, including every `Catalog.*` |
| `Librarian` role | `LibrarianRoleDataSeedContributor` (new, host `ToolShare.Application`) | All `Catalog.*` management permissions |

The `Librarian` seeder lives in the **host**, not in Catalog: granting permissions to roles requires the Identity module, and a business module must not take that dependency. The host may reference `Catalog.Application.Contracts` for the permission-name constants, which is exactly the permitted coupling direction.

Seeding is idempotent (FR-016): the role is created only if absent, and grants are applied through `IPermissionDataSeeder`, which is itself idempotent.

Default admin credentials come from the ABP template (`admin` / `1q2w3E*`) and are overridable through configuration — see [../quickstart.md](../quickstart.md).

---

## Unauthenticated access (FR-011)

The Blazor host's fallback authorization policy requires an authenticated user, so every Catalog route redirects an anonymous visitor to the ABP Account login page. There is **no** anonymous catalog page and no `[AllowAnonymous]` anywhere in the Catalog module.

---

## Verification

| Criterion | Test |
|---|---|
| SC-003 (unauthorized blocked) | Integration tests execute every management operation as (a) an authenticated user with no Catalog grants and (b) an anonymous principal, asserting `AbpAuthorizationException` in both cases |
| SC-003 (authorized allowed) | The same operations run as a user in the `Librarian` role and succeed |
| SC-009 (fresh install) | A DbMigrator run against an empty database yields an `admin` user able to perform every management operation |
| FR-011 (redirect) | Requesting `/catalog` unauthenticated returns a redirect to the login endpoint |
