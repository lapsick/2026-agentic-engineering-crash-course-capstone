# Feature Specification: Out-of-Band Maintenance

**Feature Branch**: `008-out-of-band-maintenance`

**Created**: 2026-09-27

**Status**: Draft

**Input**: User description: "Feature: out-of-band maintenance — a Librarian can send a tool instance to maintenance when damage or a defect is discovered outside of a normal return. […] This feature closes that gap within the existing Lending module's maintenance capability; it builds on the maintenance, reservation, and notification behavior already delivered in 004 and 005 rather than replacing it. […] Out of scope: members reporting damage themselves, photos attached to the report, charging members for damage, and changing the existing return-triggered flow."

## Context

[004-lending](../004-lending/spec.md) delivered maintenance requests, but opens one **only** as a consequence of a worse condition recorded at return (004 FR-014). It explicitly deferred "damage discovered outside of a normal return" (004 Edge Cases and Assumptions). Today, a Librarian who finds a cracked blade on the shelf, or a defect while preparing a tool for pickup, has no way to take that instance out of circulation. It stays reservable and can be checked out to the next member.

This feature adds a second way into the **same** maintenance capability: a Librarian reports the problem directly, and the instance goes under maintenance exactly as if a damaged return had put it there. Closing the request, the one-open-request-per-instance rule, waitlist handling on close, and the maintenance-cost report all keep working as they do today. The only differences are that each request now records **how it originated**, and the cost report shows that origin.

Shared concepts are governed by [specs/001-tool-library/spec.md](../001-tool-library/spec.md) and are not restated here: the fixed 4-level condition scale (New → Good → Worn → Damaged), the three roles, and mandatory authentication. [006-librarian-reports](../006-librarian-reports/spec.md) defines the maintenance-cost report this feature extends.

## Clarifications

### Session 2026-09-27

- Q: When a reservation is cancelled because its instance went under maintenance, should the member be notified? (The description says "notified as today", but in the delivered system maintenance-driven cancellations of either origin generate no notification. The member sees only the cancelled reservation and its reason in their own reservations, even though 004's edge cases promised a notice. 005 delivers only return reminders, overdue notices, and standing changes.) → A: No notification. Match today's behaviour exactly: the member sees the cancellation and its reason in their own reservations. Notifying on maintenance cancellations, for either origin, is left to a later feature. (FR-009, FR-021)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Send a damaged instance to maintenance (Priority: P1)

A Librarian inspecting an instance that is in circulation and not on loan finds damage or a defect. From that instance's management page, they report it by giving a short reason and the condition they observed on the 4-level scale. The instance immediately leaves circulation and shows as under maintenance. From then on it behaves exactly like an instance a damaged return put under maintenance: it cannot be reserved or checked out, and the existing close action (with a mandatory cost, zero allowed) returns it to circulation.

**Why this priority**: This is the whole point of the feature. Without it, a known-damaged tool stays reservable, and the next member receives a broken or unsafe tool. The value is fully delivered by this story alone.

**Independent Test**: With an in-circulation, not-on-loan instance, report it with a reason and a worse condition. Confirm it shows as under maintenance, a reservation attempt for it is refused, it appears in the open maintenance queue, and closing the request with a cost returns it to circulation in the observed condition.

**Acceptance Scenarios**:

1. **Given** an instance in circulation, not on loan, with no open maintenance request, **When** a Librarian reports it with a reason and an observed condition no better than its current one, **Then** an open maintenance request is created for it and the instance shows as under maintenance in every availability view members and Librarians use.
2. **Given** an instance put under maintenance by an out-of-band report, **When** any member attempts to reserve it, or a Librarian attempts to check it out, **Then** the attempt is refused because the instance is unavailable.
3. **Given** an open out-of-band maintenance request, **When** a Librarian closes it with a stated cost (zero allowed), **Then** the instance returns to circulation, in the same way and with the same waitlist handling as when a return-triggered request is closed.
4. **Given** an open out-of-band maintenance request, **When** a Librarian attempts to close it without a cost, **Then** the attempt is refused, exactly as for a return-triggered request.
5. **Given** a Librarian reports an instance with an observed condition worse than its current one, **When** the report is accepted, **Then** the instance's current condition becomes the observed one, and a new entry recording the change is added to its condition history; no earlier entry is altered.
6. **Given** a Librarian reports an instance with an observed condition equal to its current one (a defect that does not change its grade), **When** the report is accepted, **Then** the instance goes under maintenance and its condition and condition history are unchanged.

---

### User Story 2 - Clear the instance's reservations (Priority: P1)

When an out-of-band report takes an instance out of circulation, every reservation on it that has not yet been checked out is cancelled with a stated reason. The members holding those reservations can see that they were cancelled and why. No reservation is left pointing at an instance that cannot be handed over.

**Why this priority**: Equal to User Story 1. A reservation left on an instance under maintenance means a member turns up to collect a tool that cannot be lent, which is the situation this feature exists to prevent.

**Independent Test**: Give an instance two reservations that have not been checked out: one starting next week and one whose range began today but has not been picked up. Report the instance out-of-band. Confirm both reservations are cancelled with a reason visible to each holder, and that no not-checked-out reservation remains on that instance.

**Acceptance Scenarios**:

1. **Given** an instance with reservations that have not yet been checked out, **When** it is sent to maintenance by an out-of-band report, **Then** every one of those reservations is cancelled, each recording the moment and a reason stating the instance was taken out of circulation for maintenance.
2. **Given** a reservation whose date range has already begun but which has not been checked out, **When** its instance is sent to maintenance by an out-of-band report, **Then** that reservation is cancelled as well.
3. **Given** a member whose reservation was cancelled this way, **When** they view their own reservations, **Then** they see it as cancelled along with the stated reason.
4. **Given** an instance with a waitlist, **When** it is sent to maintenance by an out-of-band report, **Then** every waiting member keeps their place in line, and the waitlist is handled on closing exactly as it is today for a return-triggered request.
5. **Given** a member holding a waitlist offer for the instance that they have not yet confirmed, **When** the instance is sent to maintenance, **Then** confirming that offer is refused while the instance is under maintenance.

---

### User Story 3 - Refuse a report that does not fit the instance's state (Priority: P1)

The out-of-band report is refused, with a message that tells the Librarian why and what to do instead, when the instance cannot sensibly be sent to maintenance this way. Refused cases: it is on loan, it is retired, it already has an open maintenance request, or the observed condition is better than its current one.

**Why this priority**: Without these refusals the feature can corrupt state that 004 relies on: two open requests for one instance, a loaned tool whose damage bypasses the return-time record, or a retired tool coming back into the maintenance cycle. It is part of the minimal viable slice, not a follow-up.

**Independent Test**: Attempt an out-of-band report against, in turn, an on-loan instance, a retired instance, an instance with an open maintenance request, and an instance with an observed condition better than its current one. Confirm each is refused with a distinct, understandable message and that nothing about the instance, its history, or its reservations changed.

**Acceptance Scenarios**:

1. **Given** an instance currently on loan, **When** a Librarian attempts an out-of-band report, **Then** it is refused with a message explaining that damage on a loaned instance is recorded when it is returned.
2. **Given** a retired instance, **When** a Librarian attempts an out-of-band report, **Then** it is refused with a message that the instance is retired.
3. **Given** an instance that already has an open maintenance request (from either origin), **When** a Librarian attempts an out-of-band report, **Then** it is refused with a message that a maintenance request is already open for it.
4. **Given** an instance in Good condition, **When** a Librarian reports it with an observed condition of New, **Then** it is refused with a message that the observed condition cannot be better than the current one.
5. **Given** a Librarian submits a report with no reason, or a reason made only of whitespace, **When** they submit it, **Then** it is refused, asking for a reason.
6. **Given** any refused report, **When** the refusal is returned, **Then** no maintenance request, condition-history entry, reservation cancellation, or circulation change has been recorded.

---

### User Story 4 - Every maintenance request shows how it started (Priority: P2)

Every maintenance request, open or closed, identifies its origin. It was either **triggered by a return**, linked to the loan whose return caused it, or **reported out-of-band**, showing who reported it, when, why, and the condition they observed. The open maintenance queue and the maintenance-cost report both show this origin. Requests created before this feature are all return-triggered and keep that meaning.

**Why this priority**: A Librarian closing a request needs to know what they are fixing and why it is there. A cost report that mixes two kinds of request without saying which is which is harder to reason about. This is P2 because User Stories 1–3 already take the damaged tool out of circulation safely; this story makes the resulting records legible.

**Independent Test**: Create one return-triggered and one out-of-band request, and close both with costs inside one date range. Confirm the open queue labelled each by origin before closing. Confirm the maintenance-cost report for that range includes both costs in its total, lists each request with its origin, and shows who, when, and why for the out-of-band one and the triggering loan for the return-triggered one. Confirm a request that existed before this feature is shown as return-triggered and linked to its loan.

**Acceptance Scenarios**:

1. **Given** a maintenance request opened by a worsened return, **When** a Librarian views it in the open queue or in the cost report, **Then** it is labelled as return-triggered and identifies the loan whose return caused it.
2. **Given** a maintenance request opened by an out-of-band report, **When** a Librarian views it in the open queue or in the cost report, **Then** it is labelled as reported out-of-band and shows the reporting Librarian, the moment of the report, the stated reason, and the observed condition.
3. **Given** maintenance requests that existed before this feature was delivered, **When** they are viewed after it is delivered, **Then** each is shown as return-triggered and linked to its original loan, with its cost and dates unchanged.
4. **Given** out-of-band and return-triggered requests closed within a selected date range, **When** a Librarian runs the maintenance-cost report for that range, **Then** the total includes the costs of both kinds, and the report lists each closed request in the range with its origin, instance, closure date, and cost, together with the subtotal for each origin.
5. **Given** an open out-of-band request, **When** the cost report is run for any range, **Then** it contributes nothing to any total, exactly as an open return-triggered request does today.

---

### Edge Cases

- **What if the report races with a checkout of the same instance?** Exactly one succeeds. Either the checkout completes and the report is refused because the instance is now on loan, or the report completes and the checkout is refused because the instance is under maintenance. The instance never ends up both on loan and under maintenance.
- **What if two Librarians report the same instance at the same moment?** Exactly one report is accepted. The other is refused because a maintenance request is already open, so there are never two open requests for one instance.
- **What if a reservation is created for the instance at the same moment it is reported?** Either the reservation is created first and then cancelled by the report, or the report is accepted first and the reservation is refused as unavailable. No not-checked-out reservation survives on an instance under maintenance.
- **What if the instance is already in Damaged condition (the worst on the scale)?** The only allowed observed condition is Damaged, so the report is accepted with no condition-history entry, as in User Story 1, scenario 6.
- **Is the last borrower charged or penalized?** No. An out-of-band report attributes the damage to no member and reports no outcome to Membership. Who caused the damage cannot be known once the tool has been handed back and passed inspection. Charging members is out of scope.
- **Can the reporting Librarian correct a mistaken report?** Not by editing it. A mistaken report is resolved by closing the request, with a cost of zero if no work was needed, which returns the instance to circulation. The report stays in history, consistent with append-only history.
- **What if the reporting Librarian later loses the Librarian role or is deactivated?** The request keeps recording them as the reporter. The record describes who reported it at the time, not their current standing.
- **What if the instance's tool is later retired?** The maintenance request and its origin stay in history and stay in the cost report for the period in which it was closed, like any other maintenance request.

## Requirements *(mandatory)*

### Functional Requirements — Reporting

- **FR-001**: The system MUST allow a Librarian or Administrator to open a maintenance request for an instance that is in circulation, not on loan, and has no open maintenance request, by stating a reason and the condition they observed on the fixed 4-level condition scale.
- **FR-002**: The reason MUST be required: trimmed of surrounding whitespace, non-empty, and at most 500 characters. A missing or whitespace-only reason MUST be refused.
- **FR-003**: The observed condition MUST be the same as, or worse than, the instance's current condition; a better observed condition MUST be refused.
- **FR-004**: The system MUST refuse an out-of-band report, with a message specific to the cause, when the instance is on loan, when it is retired, or when it already has an open maintenance request of either origin.
- **FR-005**: A refused report MUST leave no trace in the instance's state: no maintenance request, condition change, circulation change, or reservation cancellation.
- **FR-006**: The out-of-band report MUST be started from the instance's management page, the page where a Librarian already manages that instance.

### Functional Requirements — Consequences

- **FR-007**: An accepted out-of-band report MUST put the instance under maintenance, making it unavailable for reservation and checkout in every availability view for as long as the request is open. These are the same consequences as a return-triggered request (004 FR-014), with at most one open request per instance (004 FR-015).
- **FR-008**: An accepted out-of-band report MUST cancel every reservation on the instance that has not been checked out, including one whose date range has already begun, recording the moment and a stated reason on each. The cancelled reservation and its reason MUST be visible to its holder in their own reservations.
- **FR-009**: When a reservation is cancelled by an out-of-band report, no notification MUST be generated for the affected member. They learn of the cancellation through their own reservations, which show it as cancelled with its reason (FR-008). This is exactly how maintenance-driven cancellations from a return behave today.
- **FR-010**: An accepted out-of-band report MUST preserve every waiting member's place on the instance's waitlist. An outstanding, unconfirmed waitlist offer MUST NOT be confirmable while the instance is under maintenance. When the request closes, the waitlist MUST be handled exactly as it is today when a return-triggered request closes.
- **FR-011**: When the observed condition is worse than the current condition, the system MUST make it the instance's current condition and append a new entry to the instance's condition history. When it is equal, the condition and its history MUST be left unchanged.
- **FR-012**: An out-of-band maintenance request MUST be closed exactly as a return-triggered one is: by a Librarian or Administrator, with a required cost where zero is valid and blank is not (004 FR-016). Closing MUST return the instance to circulation.
- **FR-013**: An out-of-band report MUST NOT attribute the damage to any member and MUST NOT report any reliability outcome to Membership.
- **FR-014**: Concurrent attempts to report the same instance, or to report and check out or reserve it, MUST resolve so that the instance is never both on loan and under maintenance, never has two open requests, and never keeps a not-checked-out reservation while under maintenance.

### Functional Requirements — Origin & Reporting Visibility

- **FR-015**: Every maintenance request MUST record its origin. A return-triggered request is linked to the loan whose return triggered it. An out-of-band request records the reporting person, the moment of the report, the stated reason, and the observed condition.
- **FR-016**: Every maintenance request that existed before this feature MUST be treated as return-triggered and remain linked to its original loan, with its dates, status, and cost unchanged.
- **FR-017**: The open maintenance queue MUST show each request's origin. For out-of-band requests it MUST also show the reason and the observed condition.
- **FR-018**: The maintenance-cost report MUST include closed out-of-band requests in its period total on the same basis as return-triggered ones: attributed by closure date, with open requests contributing nothing (006 FR-007, FR-008). It MUST also list every request closed in the range with its origin, instance, closure date, and cost, and show a subtotal for each origin.

### Functional Requirements — History, Access & Non-Regression

- **FR-019**: Every out-of-band report and everything it causes (the request, the condition change, the circulation change, and the reservation cancellations) MUST be recorded as append-only history. No existing record is edited or deleted.
- **FR-020**: Only Librarians and Administrators who are enrolled, active members MUST be able to make an out-of-band report. Any other caller, including a member with no Librarian or Administrator role, MUST be refused.
- **FR-021**: Members MUST see nothing new from this feature beyond the reservation cancellations of FR-008, shown in their own reservations with a reason. In particular, no member-facing way to report damage is added, and no new notification is generated (FR-009).
- **FR-022**: The return-triggered maintenance flow MUST behave exactly as before this feature. That covers when a request opens, which reservations it cancels, how it closes, how waitlists are handled, and what is reported to Membership. The only addition is that it records its origin (FR-015).

### Key Entities *(include if feature involves data)*

- **Maintenance Request** *(extended)*: the existing record of an instance needing repair (instance, status, opened and closed moments, cost), now with an **origin**. A return-triggered request carries the triggering loan. An out-of-band request carries the reporter, the report moment, the reason, and the observed condition. A request has exactly one origin.
- **Out-of-Band Report details**: the reporter, the moment of the report, the reason, and the observed condition. They exist only as the origin of an out-of-band maintenance request, never on their own.
- **Reservation** *(consumed)*: cancelled with a stated reason when its instance goes under maintenance (FR-008). No new reservation state is introduced.
- **Instance Condition & Circulation** *(consumed, owned by Catalog)*: the instance's current condition, its append-only condition history, and its circulation state. This feature moves the instance to under maintenance and records a worse observed condition, without owning either record.
- **Maintenance Cost Report** *(extended)*: the existing period total, now with a per-origin subtotal and an itemized list of the closed requests in the range, each with its origin.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Librarian can take a damaged, in-circulation instance out of circulation, from opening its management page to seeing it shown as under maintenance, in under one minute.
- **SC-002**: After an accepted out-of-band report, 0 reservations that have not been checked out remain on that instance, and 100% of attempts to reserve or check it out are refused until the request is closed.
- **SC-003**: 100% of maintenance requests, including those that existed before this feature, show their origin in the open queue and in the maintenance-cost report.
- **SC-004**: 100% of out-of-band reports against an instance that is on loan, retired, already under an open request, or given a better observed condition are refused, with a cause-specific message and no change to the instance.
- **SC-005**: The maintenance-cost report's total for any period equals the sum of the costs of every request of either origin closed in that period, and the per-origin subtotals add up to that total.
- **SC-006**: Every acceptance scenario of 004's return-triggered maintenance (004 User Story 2, scenario 3 and User Story 3) and of 006's maintenance-cost report still passes unchanged after this feature is delivered.
- **SC-007**: Under simultaneous out-of-band reports, checkouts, and reservation attempts on one instance, the instance is never both on loan and under maintenance, and never has two open maintenance requests.

## Assumptions

- **Same module, same capability.** Out-of-band maintenance extends Lending's existing maintenance capability; it is not a new module or a parallel repair track. The one-open-request rule, closing, and cost reporting are shared by both origins. Following the constitution, Lending asks Catalog to change the instance's condition and circulation state through Catalog's published contract, never through Catalog's data directly. How that contract is extended is a planning decision.
- **"Future reservations" means every reservation not yet checked out**, including one whose range has begun but has not been collected. Otherwise a reservation starting today could survive on an instance under maintenance, which contradicts the goal that no reservation is left pointing at such an instance. The return-triggered flow keeps its existing rule unchanged (FR-022), because changing it is out of scope.
- **The reason is short free text of at most 500 characters.** This is enough to describe a defect while keeping the queue and report readable.
- **The observed condition may equal the current one.** A safety defect such as a loose guard or a frayed cord may not move a tool down the 4-level scale but still makes it unsafe to lend. Only a worse observed condition changes the recorded condition.
- **Closing restores circulation, not condition.** Closing a request returns the instance to circulation in its current, possibly worsened, condition, exactly as closing a return-triggered request does today. Upgrading a repaired instance's condition is outside this feature.
- **The cost report is itemized as well as totalled.** The description asks that the report "shows each request's origin"; 006 delivered a period total only. This feature adds a per-request list and per-origin subtotals alongside the existing total and count, which leaves the existing total's meaning unchanged.
- **The management page is the Librarian's existing per-instance management view.** No new navigation entry is added.
- **No reliability consequence.** Out-of-band damage cannot be attributed to a borrower, and charging members is out of scope, so nothing is reported to Membership.

## Dependencies

- [specs/001-tool-library/spec.md](../001-tool-library/spec.md): the 4-level condition scale, roles, and authentication.
- [specs/002-catalog-foundation/spec.md](../002-catalog-foundation/spec.md): the instance, its condition, its append-only condition and circulation history, and the instance management page. Catalog's published contract for reporting circulation and condition changes, first extended by 004, may need a further additive extension so that an instance that is not on loan can be moved to under maintenance with an observed condition.
- [specs/003-membership-rules/spec.md](../003-membership-rules/spec.md): the enrolled, active member gate and the Librarian and Administrator roles.
- [specs/004-lending/spec.md](../004-lending/spec.md): maintenance requests, the one-open-request rule, closing with a cost, maintenance-driven reservation cancellation, and waitlist handling. This feature resolves 004's deferred edge case "damage discovered outside of a normal return".
- [specs/005-notifications/spec.md](../005-notifications/spec.md): not affected. This feature adds no notification (FR-009). Notifying members about maintenance-driven cancellations, for either origin, remains an open gap for a later feature.
- [specs/006-librarian-reports/spec.md](../006-librarian-reports/spec.md): the maintenance-cost report this feature extends (FR-018).
