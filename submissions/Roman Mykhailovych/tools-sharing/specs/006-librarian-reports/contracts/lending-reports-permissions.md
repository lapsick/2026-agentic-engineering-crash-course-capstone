# Contract: Reports Permission Boundary

**Feature**: `006-librarian-reports` | **Tier**: 2 (module-internal) | **Date**: 2026-08-05

Satisfies **FR-012** ("Access to all three reports MUST be restricted to the Librarian and Administrator
roles; a Member without one of those roles MUST be refused") and **SC-005**.

---

## The one new permission

```csharp
// ToolShare.Lending.Application.Contracts/Permissions/LendingPermissions.cs
public static class Reports
{
    public const string Default = GroupName + ".Reports";   // "Lending.Reports"
}
```

Defined under Lending's existing permission group, alongside its four shipped permissions:

```csharp
// LendingPermissionDefinitionProvider.Define
lendingGroup.AddPermission(LendingPermissions.Reports.Default, L("Permission:Lending.Reports"));
```

A top-level permission in the group rather than a child of `Lending.Loans`, because report access is a
capability separable from operating the loan desk — see [../research.md](../research.md) R8.

**Localization** — one key added to
`ToolShare.Lending.Domain.Shared/Localization/Lending/en.json`:

```json
"Permission:Lending.Reports": "View reports"
```

Lending's existing `LendingPermissionLocalizationTests` asserts that every defined permission has a
localized display name, so this key is not optional — the shipped test fails without it.

---

## Grants

| Role | Holds `Lending.Reports`? | How |
|---|---|---|
| Administrator | **Yes** | Automatically — `MembershipRoleDataSeedContributor` composes `AdministratorPermissions` from `LibrarianRoleDataSeedContributor.LibrarianLendingPermissions`, so the single edit below covers both roles |
| Librarian | **Yes** | One entry appended to `LibrarianLendingPermissions` |
| Member | No | Granted nothing; refused with `AbpAuthorizationException` (SC-005) |
| Not enrolled / deactivated | No | Refused earlier still, by the enrolment gate |

The single required edit:

```csharp
// ToolShare.Application/Identity/LibrarianRoleDataSeedContributor.cs
public static readonly string[] LibrarianLendingPermissions =
{
    LendingPermissions.Loans.Default,
    LendingPermissions.Loans.Checkout,
    LendingPermissions.Loans.Return,
    LendingPermissions.Maintenance.Close,
    LendingPermissions.Reports.Default        // 006-librarian-reports
};
```

This is exactly the mechanism 003 built so that "Administrator ⊇ Librarian ⊇ Member" stays true without
being restated per feature — ABP models no role hierarchy of its own, so the union is materialized at
seed time. `IPermissionDataSeeder` is idempotent, so re-running `DbMigrator` against an existing
database grants the new permission to both roles without disturbing anything else.

The seeder stays in the **host** (`ToolShare.Application`) rather than in Lending, for the reason its
own doc comment gives: granting permissions requires the Identity module, and a business module must not
take that dependency.

---

## Enforcement

| Layer | Mechanism |
|---|---|
| Application service | `[Authorize(LendingPermissions.Reports.Default)]` on `ReportAppService` (class-level — all three methods) |
| UI page | `@attribute [Authorize(LendingPermissions.Reports.Default)]` on `Reports.razor` |
| UI menu | `requiredPermissionName: LendingPermissions.Reports.Default` on the menu item, so it is hidden rather than shown-then-refused |
| Enrolment | Unchanged and underneath all of the above — `MembershipMethodInvocationAuthorizationService` refuses any caller who is not an enrolled, Active member, exactly as for every other application-service call |

The class-level attribute is what SC-005 is tested against; the page and menu guards are defense in
depth and usability, not the security boundary.

---

## Why not the alternatives

| Option | Why not |
|---|---|
| Reuse `Lending.Loans.Default` | Conflates "may operate the loan desk" with "may review community-wide analytics". Collapsing two permissions later is easy; splitting one that has already been granted means migrating role assignments |
| One permission per report | Granularity nothing asks for — the spec treats the three reports as a single librarian capability (product spec US7 is one story). Unused permissions still have to be seeded, localized, and tested |
| No permission, enrolment gate only | That pattern fits *self-service* views of one's own data (Catalog browsing, `MyLending`, `MyMembership`). These reports expose community-wide information including who is currently holding which tool — which the product spec's privacy assumption keeps visible to Librarians and hidden from ordinary members |
