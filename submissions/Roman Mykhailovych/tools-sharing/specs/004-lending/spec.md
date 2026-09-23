# Feature Specification: Lending — Reservations, Checkout, Return & Maintenance

**Feature Branch**: `004-lending`

**Created**: 2026-08-03

**Status**: Draft

**Input**: User description: "next feature" — resolved to the next feature in the roadmap recorded by [specs/003-membership-rules/spec.md](../003-membership-rules/spec.md): **Lending** (reservations and a FIFO waitlist, checkout and return with condition recording, the maintenance requests a worsened return triggers, and reporting reliability outcomes to Membership), which the roadmap names as "the immediate next feature" once Membership's published boundary shipped.

## Context

The product specification lives in [specs/001-tool-library/spec.md](../001-tool-library/spec.md) and governs shared concepts referenced here without being restated: the fixed 4-level condition scale (New → Good → Worn → Damaged); FIFO waitlist ordering with a time-boxed offer window; the reliability rating and its configurable point values; the three roles Member / Librarian / Administrator; mandatory authentication with no public access.

[002-catalog-foundation](../002-catalog-foundation/spec.md) delivered the Catalog module: tools, categories, and instances with a condition and a circulation state (`InCirculation` / `Retired`), plus a read-only public lookup (`IToolInstanceLookupAppService`) that this feature depends on. Catalog's own circulation-state enum already anticipates this feature by name in its source comment: "`OnLoan` and `UnderMaintenance` are introduced by features 003+" — this is the feature that introduces them.

[003-membership-rules](../003-membership-rules/spec.md) delivered the Membership module: a governed roster, one authoritative set of community rules, and a reliability rating recorded as append-only history. It explicitly deferred every borrowing decision to this feature and published exactly the surface this feature needs: a standing lookup (is this person an enrolled, active member, and what is their effective concurrent-loan limit), a read-only rules lookup, an inbound contract to report a rating-affecting outcome, and an event announcing standing changes.

This feature closes the loop those two opened. It turns "the catalog shows what exists and who may use it" into "members can actually borrow, return, and be held to a standard while doing so." It is the first feature to depend on **two** published module boundaries at once, and the first to need Catalog to publish a second, narrower capability (a way to move an instance's circulation state and record its condition on return) beyond what 002 shipped — because Lending, not Catalog, owns the fact that an instance is on loan or awaiting repair, and Catalog's own schema-isolation rule (Constitution III) means Lending cannot reach into Catalog's tables to record that itself.

## Clarifications

None raised for this feature — every scope boundary below follows directly from a decision already recorded in the product spec or in 002/003 (cited inline), so no [NEEDS CLARIFICATION] marker was needed. The Assumptions section documents each of these inherited defaults explicitly, in case a reviewer wants to challenge one before planning begins.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reserve a tool, or join the waitlist (Priority: P1)

A member finds an available instance in the catalog and reserves it for a date range up to the configured maximum loan term. If every instance of that tool is taken for the wanted dates, the member is offered a waitlist spot instead of a dead end. A member can cancel their own reservation any time before checkout. A member with an overdue loan cannot create a new reservation.

**Why this priority**: This is the entry point of the entire feature — without a way to claim a tool, nothing else here has anything to act on. It is the direct sequel to 002's "the catalog shows what's free," turning visibility into an actual borrowing commitment. Even alone, it already replaces "reserve tools by messaging the librarian."

**Independent Test**: As a member, reserve a free instance for a date range and confirm it is no longer offered to others for that range; as a second member, attempt to reserve the same instance for overlapping dates and confirm a waitlist join is offered instead of an error; cancel the first reservation before checkout and confirm the instance becomes reservable again.

**Acceptance Scenarios**:

1. **Given** a member browsing an available instance, **When** they reserve it for a date range within the configured maximum loan term, **Then** an active reservation is created and the instance shows as taken for that range to every other member.
2. **Given** an instance already reserved for a date range, **When** another member requests it for an overlapping range, **Then** they are offered a waitlist spot instead of a conflicting reservation, and joining records their place by the order they asked.
3. **Given** a member requests a date range longer than the configured maximum loan term, **When** they submit the reservation, **Then** it is rejected with a message stating the allowed maximum.
4. **Given** a member who already holds as many active reservations and loans as their effective concurrent-loan limit allows, **When** they attempt one more, **Then** it is rejected explaining the limit (and, if their rating is below the low-rating threshold, that the limit is currently the reduced one).
5. **Given** a member with at least one overdue loan, **When** they attempt to create a reservation, **Then** it is rejected until the overdue item is returned.
6. **Given** a member with an active reservation that has not yet been checked out, **When** they cancel it, **Then** the reservation ends immediately, the instance becomes reservable by others for that range, and if a waitlist exists for it, the first member on it receives a time-boxed offer.
7. **Given** an instance under maintenance or retired, **When** a member attempts to reserve it, **Then** the attempt is rejected because it is not available.
8. **Given** an instance freed by a cancellation, an early return, or a normal return, **When** a waitlist exists for it, **Then** the earliest-joined waiting member receives an offer that must be confirmed within the configured offer window; if it lapses unconfirmed, the offer passes to the next member in join order.

---

### User Story 2 - Check out and return a tool, recording its condition (Priority: P1)

Against an active reservation, a Librarian checks the tool out to the member, and the instance is marked on loan. When it comes back — on time, early, or late — the Librarian records its returned condition. A return in the same condition or better simply closes the loan and frees the instance. A return in a worse condition also closes the loan but immediately opens a maintenance request and keeps the instance unavailable until that request is closed.

**Why this priority**: This closes the loan lifecycle that User Story 1 opens. Without checkout there is no way to move from "reserved" to "in the member's hands," and without return there is no way to get it back into circulation, know its condition, or eventually compute a reliability outcome. It is equal in priority to User Story 1 because half a lending flow is not a usable one.

**Independent Test**: Check out an instance against an existing reservation and confirm it shows as on loan; return it in the same condition and confirm the loan closes and the instance becomes available; separately, check out and return another instance in a worse condition and confirm the loan still closes but the instance stays unavailable, referencing an open maintenance request.

**Acceptance Scenarios**:

1. **Given** an active reservation whose date range has arrived, **When** a Librarian checks out the tool, **Then** the checkout moment is recorded, the loan becomes active, and the instance shows as on loan rather than free.
2. **Given** a tool on loan, **When** the Librarian records a return in the same or a better condition than at checkout, **Then** the loan closes, the return moment is recorded, and the instance becomes available again (subject to any waitlist, see User Story 1).
3. **Given** a tool on loan, **When** the Librarian records a return in a worse condition than at checkout, **Then** the loan closes, a maintenance request is opened for the instance, and the instance stays unavailable for reservation or checkout until that request is closed.
4. **Given** a member wants to return a tool before its planned return date, **When** the Librarian records that early return, **Then** it is accepted the same way an on-time return is, and the instance is freed immediately.
5. **Given** an instance still on loan and overdue, **When** the Librarian attempts to check out that same physical instance to a different reservation, **Then** the attempt is rejected because it has not been returned.
6. **Given** a return's condition is recorded, **When** the loan closes, **Then** the fact and moment of checkout and of return, and both recorded condition values, remain part of the instance's permanent history.

---

### User Story 3 - Close a maintenance request and restore the instance (Priority: P2)

A Librarian reviews the open maintenance requests, and for each, records what it cost to address and closes it. Once closed, the instance is available again exactly as any other instance in its (now current) condition.

**Why this priority**: A worsened-condition return that could never be worked off would make User Story 2 a one-way trip to permanent unavailability, which defeats the purpose of tracking condition at all. It follows US2 because a request only exists once a worsened return has created one.

**Independent Test**: With an instance held unavailable by an open maintenance request, close the request with a stated cost and confirm the instance becomes reservable again and the request's cost is retained in the instance's history.

**Acceptance Scenarios**:

1. **Given** an instance with an open maintenance request, **When** a Librarian closes it with a stated maintenance cost, **Then** the instance becomes available for reservation and checkout again.
2. **Given** a Librarian attempts to close a maintenance request without stating a cost, **When** they submit it, **Then** the attempt is rejected requiring a cost value (zero is an acceptable cost; blank is not).
3. **Given** a maintenance request has been closed, **When** anyone inspects the instance's history, **Then** the request, its cost, and when it was opened and closed remain visible, unchanged, alongside the return that triggered it.
4. **Given** an instance already has an open maintenance request, **When** an attempt is made to open a second one for it before the first is closed, **Then** the attempt is rejected — one open request per instance at a time.

---

### User Story 4 - Reliability outcomes flow back to Membership (Priority: P2)

When a loan closes, this feature tells Membership what happened to the member's reliability standing: an overdue return, a worsened-condition return, or a clean on-time return — using only the contract Membership already publishes. A member's effective borrowing limit (User Story 1, scenario 4) and the block on borrowing while overdue (User Story 1, scenario 5) are decided using what Membership reports back, not by this feature's own guess at the member's standing.

**Why this priority**: This is what makes User Story 1's eligibility checks and User Story 2's condition recording actually mean something beyond this module — without it, a member's rating would never move and the low-rating limit would never engage, silently breaking the product's core self-regulation mechanism (product spec US5). It is P2 because it has nothing to report until Users Stories 1 and 2 exist and produce real loans and returns.

**Independent Test**: Close a loan with a worsened-condition return and confirm the member's reliability rating (visible on their Membership profile) has decreased by the configured damage penalty; separately, close an on-time, undamaged loan and confirm the rating increases by the configured clean-return reward (or stays at 100 if already there); confirm reporting the same loan's outcome twice does not double the effect.

**Acceptance Scenarios**:

1. **Given** a loan closes with a return that is late, **When** the closing is processed, **Then** Membership is told about an overdue outcome for that member, referencing the loan as the originating occurrence.
2. **Given** a loan closes with a worsened-condition return, **When** the closing is processed, **Then** Membership is told about a damage outcome for that member, in addition to an overdue outcome if the return was also late.
3. **Given** a loan closes on time with no worsened condition, **When** the closing is processed, **Then** Membership is told about a clean-return outcome for that member.
4. **Given** the same loan's outcome has already been reported to Membership, **When** this feature attempts to report it again (for example, after a retried operation), **Then** Membership's own safeguard against double-reporting is relied upon, and the member's rating is not affected a second time.
5. **Given** a member's effective concurrent-loan limit or overdue status, **When** this feature checks whether they may create a reservation or checkout, **Then** it asks Membership for that member's current standing rather than computing a rating of its own.
6. **Given** Membership reports that a person is not an enrolled, active member, **When** this feature is asked to let them reserve or check out a tool, **Then** the request is refused, consistent with Membership's own access rule.

---

### User Story 5 - Reminders and overdue tracking (Priority: P3)

As a return date approaches, the member holding the loan is reminded ahead of time. Once a return date has passed without a return, the loan is marked overdue, which is what makes User Story 1's overdue block take effect and lets a Librarian see, at a glance, what is currently late.

**Why this priority**: Reminders reduce overdue loans without a Librarian having to track dates by hand, but the feature is fully usable without them — a Librarian can already see loan due dates and manually follow up. It is lower priority than the borrowing lifecycle itself and is the first appearance of a cross-feature concern (actually delivering a reminder as a message) that a later feature will own in full.

**Independent Test**: Create a loan with a return date in the near future and confirm a reminder is generated ahead of that date by the configured lead time; let (or simulate) a loan's return date pass without a return and confirm it is marked overdue and that a new reservation attempt by that member is blocked.

**Acceptance Scenarios**:

1. **Given** a loan whose planned return date is approaching, **When** the configured reminder lead time is reached, **Then** a reminder referencing that loan is generated for the member holding it.
2. **Given** a loan's planned return date has passed without a recorded return, **When** that moment is reached, **Then** the loan is marked overdue and a notice referencing it is generated for the member.
3. **Given** a loan is marked overdue, **When** it is later returned, **Then** the overdue marking is retained as part of that loan's permanent record even though the loan itself is now closed.
4. **Given** a Librarian reviews current loans, **When** they look at one that is overdue, **Then** it is clearly distinguished from loans that are still within their term.

---

### Edge Cases

- What happens when two members try to reserve the last free instance of a tool at the same moment? Exactly one reservation is created; the other member's request is rejected as no-longer-available and they are offered the waitlist instead, with no scenario in which the same instance ends up double-booked for overlapping dates.
- What happens when a Librarian tries to check out an instance that a previous member has not actually returned yet? The attempt is rejected, stating that the instance is still on loan (and overdue, if applicable).
- What happens if damage to an instance is discovered outside of a normal return (for example, reported separately)? Out of scope for this feature — see Assumptions; only condition recorded at return time drives a maintenance request here.
- What happens to a reservation or a waitlist spot if the instance is moved to maintenance for a reason unrelated to that reservation (e.g., discovered damage between checkout and the planned start)? Any reservation depending on that instance is cancelled and its holder is notified via the same reminder mechanism as User Story 5; a waitlist, if any, is preserved and re-offered once the instance is available again.
- What happens when a time-boxed waitlist offer expires with nobody left in the queue? The instance simply remains free and reservable by any member, exactly as if no waitlist had existed.
- What happens when the Administrator lowers the concurrent-loan limit or raises the low-rating threshold while a member is already holding loans at or near the old limit? Existing loans and reservations are untouched; only the member's next new reservation attempt is evaluated against the new rule.
- What happens when a return is recorded in a *better* condition than at checkout (for example, after an unrelated repair happened to have occurred)? It is treated the same as "no worse" — the loan closes cleanly, a clean-return outcome is reported, and no maintenance request is created.
- What happens when the member checking out a tool is the same one who reserved it, versus someone else presenting the reservation? Out of scope for this feature to distinguish — checkout is a Librarian-mediated, in-person action against the reservation record; identity verification at the point of physical handover is a process concern, not a system one.
- What happens if a loan closes with both a late return and a worsened condition? Both an overdue outcome and a damage outcome are reported to Membership for that same loan (Membership's own contract is explicit that one occurrence may carry both).

## Requirements *(mandatory)*

### Functional Requirements — Reservations & Waitlist

- **FR-001**: The system MUST allow an enrolled, active member to reserve a specific available instance for a date range not exceeding the community's configured maximum loan term.
- **FR-002**: The system MUST prevent a single physical instance from having two active reservations, or a reservation and a loan, for overlapping date ranges.
- **FR-003**: The system MUST offer a member a waitlist spot, ordered by the moment they asked (FIFO), when the instance they want is unavailable for their desired range, rather than simply rejecting the request.
- **FR-004**: The system MUST allow a member to cancel their own active reservation at any point before checkout; cancelling after checkout is not offered — an early return (FR-011) is used instead.
- **FR-005**: The system MUST, when an instance becomes free (by cancellation, early return, or normal return) and a waitlist exists for it, offer the reservation to the earliest-joined waiting member within a time-boxed window configured by the Administrator; if the window lapses unconfirmed, the system MUST roll the offer to the next member in join order, and repeat until either an offer is confirmed or the waitlist is exhausted.
- **FR-006**: The system MUST reject a reservation attempt for an instance that is under maintenance or retired.
- **FR-007**: The system MUST reject a reservation attempt from a member who currently holds an overdue loan, until that loan is returned.
- **FR-008**: The system MUST reject a reservation attempt that would give a member more concurrent active reservations and loans than their effective concurrent-loan limit allows.
- **FR-009**: The system MUST resolve simultaneous reservation attempts for the same instance and overlapping dates so that exactly one succeeds and any other is refused as no-longer-available (offering the waitlist per FR-003), never creating two overlapping commitments for one instance.

### Functional Requirements — Checkout, Return & Condition

- **FR-010**: The system MUST allow a Librarian to check out an instance against an existing, not-yet-checked-out reservation, recording the moment of checkout and marking the instance on loan.
- **FR-011**: The system MUST allow a Librarian to record a return for an instance on loan — at, before, or after its planned return date — capturing the moment of return and the instance's condition at that moment on the same fixed 4-level scale used by the catalog.
- **FR-012**: The system MUST close the loan on any recorded return, regardless of whether the returned condition is the same, better, or worse than at checkout, and MUST make the instance immediately unavailable to reserve or check out if the condition worsened, or available again (subject to FR-005) if it did not.
- **FR-013**: The system MUST reject a checkout attempt for an instance that is currently on loan or otherwise unavailable.

### Functional Requirements — Maintenance

- **FR-014**: The system MUST automatically open a maintenance request for an instance whenever a return records a condition worse than the condition at that instance's checkout, and MUST keep the instance unavailable for reservation and checkout for as long as that request remains open.
- **FR-015**: The system MUST allow at most one open maintenance request per instance at a time.
- **FR-016**: The system MUST allow a Librarian to close an open maintenance request with a stated maintenance cost (zero is a valid cost), after which the instance becomes available again.
- **FR-017**: The system MUST retain a closed maintenance request — its cost, and when it was opened and closed — as part of the instance's permanent history, even after the instance's condition or availability later changes again.

### Functional Requirements — Membership Integration

- **FR-018**: The system MUST determine whether a person may reserve or check out a tool, and their effective concurrent-loan limit, using Membership's published standing lookup rather than any independent record of membership or rating.
- **FR-019**: The system MUST refuse a reservation or checkout for a person Membership reports as not an enrolled, active member.
- **FR-020**: The system MUST report to Membership, through its published outcome-reporting contract, an overdue outcome for any loan that closes after its planned return date, a damage outcome for any loan that closes with a worsened condition, and a clean-return outcome for any loan that closes on time with no worsened condition — a single loan closing both late and damaged MUST result in both an overdue and a damage outcome being reported.
- **FR-021**: The system MUST supply, with every outcome it reports to Membership, an identifier of the loan that caused it, so that a repeated report of the same loan's outcome is not applied twice by Membership.

### Functional Requirements — Reminders & Overdue Tracking

- **FR-022**: The system MUST generate a reminder for the member holding a loan when the community's configured reminder lead time before its planned return date is reached.
- **FR-023**: The system MUST mark a loan overdue when its planned return date passes without a recorded return, and MUST generate a notice of this for the member holding it.
- **FR-024**: The system MUST allow a Librarian to distinguish, when reviewing current loans, which ones are currently overdue.
- **FR-025**: The system MUST retain a loan's overdue marking as part of its permanent record after it is eventually returned and closed.

### Functional Requirements — History & Catalog Integration

- **FR-026**: The system MUST maintain an immutable, append-only record of every reservation, loan, and maintenance request and every change to their state, for auditing; existing records are never edited or deleted.
- **FR-027**: The system MUST reflect an instance's lending-driven state — on loan, or under maintenance — in the same catalog availability view members already use to browse and search, without duplicating the catalog's own record of the instance's identity, tool, category, or serial number.
- **FR-028**: The system MUST NOT record or derive any of Membership's data (roles, membership status, reliability rating) independently; it MUST obtain every such fact through Membership's published contracts.

### Functional Requirements — Roles

- **FR-029**: The system MUST restrict checkout, return recording, and maintenance-request closing to Librarians and Administrators, while allowing any enrolled, active member to create their own reservations, join waitlists, and cancel their own reservations.
- **FR-030**: The system MUST allow a member to view their own current and past reservations and loans, and MUST allow a Librarian or Administrator to view any member's.

### Key Entities *(include if feature involves data)*

- **Reservation**: a member's claim on a specific instance for a date range — holds the member, the instance, the range, and a status (active, cancelled, or realized as a loan via checkout).
- **WaitlistEntry**: a member's place in line for an instance that was unavailable when they wanted it — holds the member, the instance, the moment they joined (which fixes their FIFO order), and the state of any current time-boxed offer (offered, confirmed, or expired-and-passed-on).
- **Loan**: the fact of an instance being checked out to a member — holds the member, the instance, the reservation it came from, the checkout moment, the planned return date, the actual return moment once closed, the condition recorded at each end, and whether it became overdue.
- **MaintenanceRequest**: the record of an instance needing repair after a worsened-condition return — holds the instance, the triggering loan, a status (open or closed), and, once closed, the recorded cost.
- **Reminder / OverdueNotice**: a generated message tied to a loan, produced when the reminder lead time or the planned return date is reached — the actual delivery mechanism (in-app, email) is out of scope for this feature (see Assumptions).
- **Membership Standing** *(consumed, not owned)*: whether a person is an enrolled, active member and their effective concurrent-loan limit, obtained from Membership's published contract on every eligibility decision.
- **Instance Availability** *(consumed and extended)*: Catalog's own record of an instance's identity and condition, extended by this feature with the on-loan and under-maintenance states this feature is responsible for setting and clearing.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A member reserves an available instance, and the reservation is reflected in the catalog's availability for every other member, within the same interaction — 0 cases of two members successfully reserving the same instance for overlapping dates.
- **SC-002**: 100% of reservation attempts for an instance that is unavailable for the requested range are offered a waitlist spot rather than a dead-end rejection.
- **SC-003**: 100% of checkouts occur against an existing reservation, and 0 physical instances are ever checked out to two members for overlapping periods.
- **SC-004**: 100% of returns recording a worsened condition automatically produce an open maintenance request, and that instance is unavailable for reservation or checkout in 100% of attempts until the request is closed.
- **SC-005**: 100% of closed loans result in exactly one reliability outcome report to Membership per applicable outcome type (overdue, damage, clean-return), and reporting the same loan's outcome a second time changes the member's rating in 0% of cases.
- **SC-006**: 100% of reservation and checkout attempts by a person Membership reports as not an enrolled, active member are refused.
- **SC-007**: 100% of reservation and checkout attempts that would exceed a member's effective concurrent-loan limit, or that are made while the member holds an overdue loan, are refused.
- **SC-008**: Members holding an active loan receive a reminder ahead of 100% of approaching return dates, and 100% of loans not returned by their planned date are marked overdue.
- **SC-009**: 100% of reservation, loan, and maintenance-request records, and every state change to them, are preserved as immutable history and remain available for auditing, including for instances later retired.
- **SC-010**: A Librarian can tell, for any instance, whether it is free, reserved, on loan (and to whom), or under maintenance, without consulting any record outside the catalog and lending views.

## Assumptions

- **The technology stack and module shape are constraints inherited from the constitution and prior features, not choices made by this spec**: Lending follows the same modular-monolith pattern as Catalog and Membership — its own module projects, its own database schema, references to Catalog's and Membership's data by identifier only, and consumption of their published contracts rather than their internals. These are recorded here as inputs; the requirements above stay capability-focused.
- **Catalog's public contract needs a second, narrower capability this feature adds**: Catalog's published instance lookup (002) is read-only. This feature needs a way to move an instance's circulation state to on-loan/under-maintenance and back, and to record its condition on return, without Lending reaching into Catalog's schema (Constitution III forbids that). Exactly how Catalog exposes this (a new published write operation, or an inbound reporting contract mirroring the pattern Membership itself uses for reliability outcomes) is a planning decision for this feature, not a product requirement — this spec only requires that the catalog's availability view reflects Lending's facts (FR-027).
- **Maintenance-request handling (User Story 3) is part of this feature, not a separate later one**: the product spec's own User Story 3 bundles checkout, return, and the maintenance request a worsened return triggers into one story with one acceptance flow (closing the request and freeing the instance again). Splitting maintenance into its own feature would leave this feature unable to ever recover an instance it had marked unavailable, which is not an independently valuable, shippable slice.
- **Notification *delivery* (in-app and email) is out of scope for this feature**: User Story 5 only requires that reminders and overdue notices are *generated*, referencing the loan they concern. Actual delivery channels and formatting belong to a future Notifications feature, exactly as 003 deferred "notifying members about standing changes" the same way — this feature's reminders and Membership's standing-change announcements are both inputs that feature will eventually consume.
- **Librarian reports (most-borrowed tools, current overdue loans, maintenance cost over a period — product spec User Story 7) are out of scope for this feature.** They are read-only projections over the history this feature already commits to keeping immutable (FR-026), and can be built later without changing anything specified here.
- **Damage discovered outside of a normal return is out of scope.** A maintenance request in this feature is created only as a direct consequence of a recorded return condition (FR-014); a Librarian noticing damage between checkouts, or reporting it out-of-band, is not modeled here.
- **Checkout and return are Librarian-mediated, in-person actions**, consistent with the product spec's framing (the librarian checks out / accepts the return); this feature does not model self-service checkout or return by the member themselves, nor identity verification at the point of physical handover.
- **One installation, one waitlist per instance, one open maintenance request per instance at a time** — no per-community partitioning and no concurrent, independent repair tracks for the same physical instance (FR-015).
- **Reliability outcomes are reported, never computed locally**: this feature holds no reliability-rating logic of its own (product spec FR-024's point values, clamping, and history all remain exclusively Membership's, per 003). This feature's only responsibility toward the rating is telling Membership what happened and when (FR-020, FR-021).
- **The reservation-to-loan transition is 1:1 and one-directional**: a loan always originates from exactly one prior reservation (checkout requires one, FR-010); there is no walk-up checkout without a reservation in this feature.

## Dependencies

- Depends on and must remain consistent with the product specification: [specs/001-tool-library/spec.md](../001-tool-library/spec.md) (condition scale, FIFO waitlist and offer-window mechanics, reliability rating model, roles, authentication requirement).
- Depends on [specs/002-catalog-foundation/spec.md](../002-catalog-foundation/spec.md) for the catalog of tools and instances, their condition and circulation-state model, and the read-only public lookup this feature queries for availability and identity. This feature is expected to extend Catalog's public contract with the narrower write capability described in Assumptions, and to append the on-loan/under-maintenance circulation states Catalog's own source already anticipates.
- Depends on [specs/003-membership-rules/spec.md](../003-membership-rules/spec.md) for every fact about people: enrolled/active standing (FR-024), effective concurrent-loan limit (FR-023), and community rules (FR-026) — maximum loan term, concurrent-loan limit, low-rating threshold, offer window, reminder lead time — consumed exclusively through Membership's published contracts, including the outcome-reporting contract (FR-027) and the standing-changed event (FR-028).
- Unblocks the later Notifications feature (delivery of the reminders and overdue notices this feature generates, and reaction to Membership's standing-changed event) and the later Librarian-reports feature (built over the immutable history this feature commits to keeping, FR-026).
