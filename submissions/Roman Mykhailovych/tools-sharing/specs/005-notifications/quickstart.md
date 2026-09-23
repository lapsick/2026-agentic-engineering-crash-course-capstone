# Quickstart: Validate the Notifications Slice

**Feature**: `005-notifications` | **Date**: 2026-08-04 | **Plan**: [plan.md](./plan.md)

How to run and prove the Notifications module end-to-end from a clean checkout. Implementation detail
lives in [data-model.md](./data-model.md) and [contracts/](./contracts/); this file is the run-and-verify
guide.

---

## Prerequisites

Unchanged from 002/003/004 — this feature adds no new tooling and no new PostgreSQL extension:

| Requirement | Check |
|---|---|
| .NET SDK 10.0.3xx (pinned by `global.json`) | `dotnet --version` |
| `dotnet-ef` 10.x global tool | `dotnet ef --version` → must report **10.x** |
| Docker running | `docker ps` — required by Testcontainers |

No SMTP server is required to validate this feature — tests use a fake `IEmailSender`, and by default
ABP's own `IEmailSender` implementation logs instead of sending when no mail transport is configured
(research R2), so `dotnet run` works out of the box for manual walkthroughs too.

---

## 1. Build and run the full test suite

```bash
dotnet build ToolShare.slnx
dotnet test ToolShare.slnx
```

Expected: 0 errors, all tests green — including the two **new** projects
`ToolShare.Notifications.Domain.Tests` and `ToolShare.Notifications.Application.Tests`, and the
**existing** Membership suite, which this feature extends with one new field on `MemberStandingDto`
(no existing Membership behavior changes — see
[membership-extension.md](./contracts/membership-extension.md)).

Run just this feature's tests:

```bash
dotnet test test/ToolShare.Notifications.Domain.Tests/ToolShare.Notifications.Domain.Tests.csproj
dotnet test test/ToolShare.Notifications.Application.Tests/ToolShare.Notifications.Application.Tests.csproj
```

Integration tests spin up their own PostgreSQL 16 container through the shared
`PostgreSqlContainerFixture` — `docker compose up` is **not** needed for tests.

---

## 2. Create/migrate the database

```bash
docker compose up -d                       # postgres for local dev
cd src/ToolShare.DbMigrator && dotnet run   # must run from its own directory
```

Expected on a fresh database:

- schema `notifications` created with `Notifications` and `NotificationDeliveryRecords`
- Membership's `MemberStandingDto`-backing query paths compile and run unchanged (no schema change in
  the `membership` schema — `Member.Email` already existed)
- ABP role `Administrator` additionally holds `Notifications.Audit`

---

## 3. Run the application

```bash
cd src/ToolShare.Blazor && dotnet run
```

Sign in as `admin` / `1q2w3E*`, or as a member enrolled per 003's quickstart, with an open loan created
per 004's quickstart (US1–US2) to have something for a reminder/overdue notification to concern.

---

## 4. Walk the user stories

### US1 — See loan deadlines in an in-app inbox

1. With a member holding a loan, wait for (or simulate, via a shortened
   `ReminderLeadTimeDays` community rule and a near-future `PlannedReturnDate`) Lending's
   `ReturnReminderWorker` sweep. → the member's Notifications inbox shows a new, unread entry naming the
   tool and planned return date (**FR-001**).
2. Let the loan's planned return date pass without a return (or simulate via `OverdueMarkingWorker`).
   → a second, distinct notification appears marking it overdue (**FR-002**).
3. Confirm re-running the same worker sweep does not produce a duplicate — Lending's own
   `ReminderSentAt`/`OverdueNoticeSentAt` guards prevent the source event from firing twice, and
   Notifications' own unique index (`NOTIF-01`) would refuse a duplicate insert even if it did
   (**FR-005**).

### US2 — Receive the same loan deadlines by email

1. Enrol (or use) a member with an email address on file. Repeat US1 step 1. → an email is enqueued
   (check application logs for the background job, or a configured mail-catcher/SMTP dev server) in
   addition to the in-app notification (**FR-007**, **FR-008**).
2. Enrol a member and, if your test data allows it, clear their email — or use an account whose email
   lookup you simulate as absent. → the in-app notification still appears; no email attempt is made,
   and the notification's audit trail shows the email channel as `Skipped`, not `Failed` (**FR-009**).
3. Point the mail transport at an unreachable host and repeat step 1. → the in-app notification still
   appears immediately; the email delivery record shows `Failed` once the background job runs
   (**FR-010**).

### US3 — Learn about changes to your own standing

1. As an Administrator, deactivate a member. → that member's inbox shows a new notification describing
   the deactivation, in-app and (if they have an email) by email (**FR-003**).
2. Reactivate them. → a second, distinct notification appears.
3. Change their role. → a role-change notification appears.
4. Record enough overdue/damage outcomes (or a manual adjustment, via 003's `AdjustMemberRatingDto`) to
   move their rating across the community's low-rating threshold. → a threshold-crossing notification
   appears (**FR-004**). Record an outcome that does *not* cross the threshold. → no notification is
   generated for it.
5. Confirm no *other* member's inbox shows any of the above (**FR-014**, SC-004).

### US4 — Tell new notifications from ones already seen

1. With several unread notifications present, open the inbox. → each is visually distinguishable as
   unread, and an unread count is shown (**FR-013**).
2. Mark one as read. → only that one changes state; the unread count decreases by exactly one.
3. Mark all as read. → the unread count reaches zero; a subsequently generated notification (repeat any
   step above) appears unread again.

---

## 5. Prove the module boundary

```bash
dotnet test test/ToolShare.Notifications.Application.Tests/ToolShare.Notifications.Application.Tests.csproj \
  --filter "FullyQualifiedName~PublicContract"
```

This raises a `LendingNotificationDueEto` and a `MemberStandingChangedEto` directly and asserts the
expected `Notification` rows and delivery records appear — while importing **no**
`ToolShare.Lending.Domain…`/`…EntityFrameworkCore…`, `ToolShare.Membership.Domain…`/
`…EntityFrameworkCore…`, or `ToolShare.Catalog.Domain…`/`…EntityFrameworkCore…` namespace anywhere in
the test file. The absent import is the assertion.

Verify by hand too:

```bash
grep -rn "Lending.Domain\.\|Lending.EntityFrameworkCore\|Membership.Domain\.\|Membership.EntityFrameworkCore\|Catalog.Domain\.\|Catalog.EntityFrameworkCore" \
  test/ToolShare.Notifications.Application.Tests/PublicContract/
# expected: no matches
```

---

## 6. Confirm no regression in Membership

```bash
dotnet test test/ToolShare.Membership.Application.Tests/ToolShare.Membership.Application.Tests.csproj
dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj
```

Expected after this feature's changes:

- Both suites remain fully green — this feature adds one field to Membership's `MemberStandingDto`
  (`membership-extension.md`) but changes no existing Membership behavior, and only *subscribes to*
  Lending's already-published event with zero changes required there.
- [003's contracts](../003-membership-rules/contracts/) note the new `Email` field on
  `MemberStandingDto`.

---

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| No notification appears after a loan-deadline event | Confirm the local event handler is actually registered — `LoanNotificationGenerator`/`StandingChangeNotificationGenerator` must be resolvable as `ITransientDependency` implementations of `ILocalEventHandler<T>`; check the notification's unique index (`NOTIF-01`) isn't silently absorbing it as a false-positive duplicate |
| In-app notification appears but no email is ever attempted, even with an address on file | Confirm `IMemberStandingAppService.GetAsync` (not `GetByIdentityUserIdAsync`) actually returns the new `Email` field for that member — verify `MemberStandingAppService.GetAsync`/`GetByIdsAsync` were updated, not only `MemberStandingDto`'s shape |
| Email delivery record stays `Pending` forever | Confirm `IBackgroundJobManager.EnqueueAsync` is actually being called, and that the host's background job execution is enabled (`AbpBackgroundJobOptions.IsJobExecutionEnabled`, on by default) — not disabled in configuration |
| A member sees another member's notification | A self-service query is missing its `MemberId` filter — every `IMyNotificationsAppService` method must scope by the caller's own resolved `MemberId`, never trust a client-supplied id |
| `Notifications.Audit`-gated method is reachable by a non-Administrator | Confirm the permission is actually declared on `INotificationAuditAppService`'s implementation and granted only to `Administrator` in the host role seeder, not accidentally left ungated |
