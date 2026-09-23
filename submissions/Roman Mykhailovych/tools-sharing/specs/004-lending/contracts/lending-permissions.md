# Lending Permission Boundary

**Requirements**: FR-018, FR-019, FR-029, FR-030 | **Verified by**: SC-006
**Defined in**: `ToolShare.Lending.Application.Contracts` (`LendingPermissions`, `LendingPermissionDefinitionProvider`)

Lending introduces **no new enrolment-gate mechanism** — 003's
`MembershipMethodInvocationAuthorizationService` already refuses every application-service call,
across every module, unless the caller is an enrolled, Active member (FR-018, FR-019). This document
covers only the permission tree layered on top of that gate.

---

## Permission tree

```text
Lending                                  (group)
├── Lending.Reservations                 (implicit — no permission constant; see below)
└── Lending.Loans                        view any member's reservations/loans
    ├── Lending.Loans.Checkout
    ├── Lending.Loans.Return
    └── Lending.Maintenance.Close
```

```csharp
public static class LendingPermissions
{
    public const string GroupName = "Lending";

    public static class Loans
    {
        public const string Default   = GroupName + ".Loans";
        public const string Checkout  = Default + ".Checkout";
        public const string Return    = Default + ".Return";
    }

    public static class Maintenance
    {
        public const string Close = GroupName + ".Maintenance.Close";
    }
}
```

**Creating, cancelling, and joining a waitlist for one's own reservations carries no permission
constant** — like Catalog's browsing (002) and Membership's self-service (003), it is gated by the
enrolment gate alone (any active member), the same reasoning both prior features used: requiring a
permission for a member's own basic use of the system would make every freshly enrolled member unable
to do anything until an Administrator granted it.

## Operation-to-permission map

| Operation | Requirement |
|---|---|
| Create/cancel own reservation, join/view own waitlist entries | `[Authorize]` — any active member (the enrolment gate alone) |
| View own reservations and loans | `[Authorize]` — any active member, self only (mirrors Membership's `IMyMembershipAppService` self-only-by-construction design, FR-030) |
| View **any** member's reservations/loans | `Lending.Loans` |
| Check out a tool against a reservation | `Lending.Loans.Checkout` |
| Record a return | `Lending.Loans.Return` |
| Close a maintenance request | `Lending.Maintenance.Close` |

### Self-only access (FR-030)

The member-facing "my reservations and loans" surface takes no member id — it resolves the caller's
own reservations/loans from `CurrentUser.Id`, exactly as `IMyMembershipAppService` (003) does for
standing. A member who wants to browse the catalog's availability already can (Catalog's own
published lookup, unaffected by this feature); Lending's addition here is only "which of these have I
personally reserved or borrowed."

## Roles and grants

| ABP role | Grants |
|---|---|
| `Member` | None — reservation creation/cancellation and self-service views are enrolment-gated only |
| `Librarian` | `Lending.Loans`, `Lending.Loans.Checkout`, `Lending.Loans.Return`, `Lending.Maintenance.Close`, plus (from `catalog-extension.md`) `Catalog.ToolInstances.ReportLendingState` |
| `Administrator` | Everything `Librarian` has |

Seeding lives in the **host** (`ToolShare.Application/Identity/`), extending
`LibrarianRoleDataSeedContributor` and `MembershipRoleDataSeedContributor` exactly as 003 extended
002's — a business module must not depend on the Identity module, so granting permissions stays a
host-only concern (research R11).

## Verification

| Criterion | Test |
|---|---|
| SC-006 (non-member/inactive blocked) | Every Lending operation executed as (a) an authenticated non-member and (b) a deactivated member, asserting refusal in both cases — reusing 003's own `EnrolmentGateTests` pattern rather than re-testing the gate's mechanics |
| Checkout/return/maintenance-closing restricted to Librarian+ | Executed as a plain `Member` and asserted denied, then as a `Librarian` and asserted allowed |
| Self-only reservation/loan view (FR-030) | A member's own view succeeds; the equivalent id-taking operation for another member's data is denied or does not exist on the interface |
