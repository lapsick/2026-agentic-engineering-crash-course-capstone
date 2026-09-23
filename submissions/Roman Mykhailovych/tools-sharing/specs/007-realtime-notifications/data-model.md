# Phase 1 Data Model: Real-Time In-App Notifications

## No new persisted data

This feature introduces **no entity, no table, no column, no schema, no index, and no EF migration.** It
is a delivery-timeliness change over the in-app channel; the data it surfaces already exists and is
wholly owned by [005-notifications](../005-notifications/data-model.md):

- **`Notification`** (aggregate root, schema `notifications`) — unchanged. Created once by a generator,
  gains a `ReadAt` timestamp on first mark-read. Remains the authoritative record of what to show.
- **`NotificationDeliveryRecord`** (child entity) — unchanged. Real-time is **not** a new channel and
  therefore adds **no** new `DeliveryChannel` value and **no** new delivery record (FR-010); the in-app
  channel's record is still written by 005 exactly as today. Real-time is a faster *transport* for that
  same in-app channel, so the audit trail (005 FR-015/FR-016) is untouched.

Because nothing is persisted, Constitution III (schema isolation) and IV (append-only history) are
satisfied trivially — there is no write to mutate, delete, or foreign-key.

## Transient runtime element (not persisted)

The only new stateful element is an **in-memory, per-process** subscription registry inside the singleton
broadcaster. It is runtime state, not domain data — empty at startup, rebuilt as circuits connect, gone at
process exit.

### `IMemberNotificationBroadcaster` — the in-process signal (see [contracts/realtime-inapp.md](./contracts/realtime-inapp.md))

| Element | Shape | Meaning |
|---|---|---|
| `Subscribe(Guid memberId, Func<Task> onChanged)` | returns `IDisposable` | A live circuit registers interest in **its own** member's notification changes. Disposing (on circuit teardown) removes it. |
| `NotifyAsync(Guid memberId)` | `Task` | Publishes a **contentless** "your notifications changed" signal to every current subscriber for that member. Exception-isolated (a failing callback never surfaces). |

- **Key**: `MemberId` (the same identifier `Notification.MemberId` uses). A subscriber only ever registers
  the member id resolved from its own `CurrentUser` (as `MyNotificationsAppService.GetOwnMemberIdOrThrowAsync`
  does), so it can only ever be signalled about itself → FR-002.
- **Payload**: none. The signal says *that* something changed, not *what*. Subscribers re-query the
  authoritative store through `IMyNotificationsAppService`, keeping one source of truth (FR-003/FR-004) and
  putting no member's content on the shared channel (FR-002).
- **Cardinality**: many subscribers per member (one per open circuit/tab) → all are signalled (FR-006).
- **Lifetime**: a subscription lives exactly as long as its Blazor component/circuit; `Dispose` guarantees
  no leak.

## State & transitions

No new state machine. The only state transitions in play are the **existing** ones (005): a `Notification`
is `Unread → Read` (write-once `ReadAt`); a `NotificationDeliveryRecord` moves to its terminal `Status`
once. This feature changes **when the UI observes** those states, not the states themselves.

## Validation & invariants

- The broadcaster performs **no** validation of notification content (it has none) and touches **no**
  domain invariant. Its only invariants are structural: a disposed subscription receives no further
  signals; a signal for member A reaches only member A's subscribers; a throwing subscriber is isolated
  (FR-009). These are covered by the unit tests in [research.md](./research.md) R6.
- Correctness of *what* the member sees remains enforced entirely by 005's `IMyNotificationsAppService`
  (self-scoped to `CurrentUser`, FR-014 of 005) — the re-query path this feature reuses, not replaces.
