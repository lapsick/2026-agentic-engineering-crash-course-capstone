# Feature Specification: Shared Tool Library

**Feature Branch**: `001-tool-library`

**Created**: 2026-07-27

**Status**: Draft

**Input**: User description: "Build a management system for a shared tool library for a local community (e.g., an HOA or a garage cooperative), where members borrow tools from a shared pool."

## Clarifications

### Session 2026-07-27

- Q: When a freed instance has several members waiting, how is the next claimant chosen? → A: FIFO — first to join the waitlist gets the first offer (pure time order).
- Q: How is the reliability rating modeled (scale, event costs, recovery)? → A: Points 0–100 starting at 100; overdue and damage subtract configurable points; each on-time, undamaged return adds a small configurable amount back, capped at 100.
- Q: Through which channels are reminders and overdue notices delivered? → A: In-app notifications plus email (SMS/messenger may be added later as pluggable channels).
- Q: What condition scale determines "worse than before"? → A: Fixed ordered 4-level scale: New → Good → Worn → Damaged; any drop to a lower level on return triggers a maintenance request.
- Q: When a freed instance reaches the next waitlist member, how is the slot handed over? → A: Time-boxed offer — the member must confirm a reservation within a configurable window; on expiry the offer rolls to the next member in FIFO order.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Catalog and Availability Visibility (Priority: P1)

A member opens the catalog, searches for a tool by name or category, and immediately sees whether an instance is free or, if it is taken, until what date and by whom (visible to the librarian). The librarian adds and edits catalog entries and registers individual physical instances with unique inventory numbers.

**Why this priority**: This is the core value — the community stops "losing" tools and abandons record-keeping in messengers and Excel. Even without reservations or reliability ratings, a transparent catalog with a current status of "who has the rotary hammer right now" already removes the main pain point. This is a self-sufficient MVP.

**Independent Test**: Populate the catalog with several tools and instances, search by name and category, and confirm that each instance shows its status (free / taken until a date) and the current loan holder.

**Acceptance Scenarios**:

1. **Given** the catalog contains a "Rotary Hammer" tool with two instances, one of which is loaned out until 30 Jul, **When** a member searches for "rotary hammer", **Then** they see both instances with their statuses: one free, the other taken until 30 Jul.
2. **Given** a member is in the catalog, **When** they filter by the "Power Tools" category, **Then** only tools in that category are shown.
3. **Given** the librarian adds a new tool and registers two identical instances, **When** they save them, **Then** each instance receives its own unique inventory number and becomes visible in the catalog as a separate unit.
4. **Given** a tool is under maintenance or has been retired, **When** a member views the catalog, **Then** that instance is shown as unavailable and is not offered for reservation.

---

### User Story 2 - Reserving a Tool with a Waitlist (Priority: P2)

A member reserves a free instance for a date range. If the instance is taken for that period, the member can join a waitlist. A reservation can be cancelled up until the moment of checkout.

**Why this priority**: Reservations turn a passive catalog into a managed flow of usage and prevent "I came and it was already taken" conflicts. A waitlist distributes scarce tools fairly.

**Independent Test**: Reserve a free instance for a date range; attempt to reserve an already-taken instance and confirm that joining a waitlist is offered; cancel your own reservation before checkout.

**Acceptance Scenarios**:

1. **Given** an instance is free for 01 Aug–03 Aug, **When** a member reserves it for those dates, **Then** an active reservation is created and the instance is marked as taken for that period.
2. **Given** an instance is already reserved for 01 Aug–03 Aug, **When** another member tries to reserve it for overlapping dates, **Then** the system offers to join the waitlist instead of creating a conflicting reservation.
3. **Given** a member has an active reservation for which the tool has not yet been checked out, **When** they cancel the reservation, **Then** the reservation is cancelled and the instance is freed for others (the next person on the waitlist gets the opportunity to reserve).
4. **Given** a member has an overdue loan, **When** they try to create a new reservation, **Then** the system blocks the action and states the reason.
5. **Given** a tool is under maintenance or retired, **When** a member tries to reserve it, **Then** the system rejects the attempt.

---

### User Story 3 - Checkout, Return, and Condition Recording (Priority: P2)

Against a reservation, the librarian checks out the tool, recording the checkout event. During return, the librarian records the instance's condition. If the condition is worse than before, a maintenance request is created automatically and the instance becomes unavailable until the request is closed.

**Why this priority**: This closes the loan lifecycle and solves the second key problem — "broken things sit unrepaired for years." Without condition recording it is impossible to maintain the quality of the pool or to compute reliability ratings.

**Independent Test**: Check out an instance against an existing reservation; return it in a condition worse than before; confirm that a maintenance request was created and the instance became unavailable for reservations.

**Acceptance Scenarios**:

1. **Given** there is an active reservation for an instance, **When** the librarian checks out the tool, **Then** the checkout event and time are recorded (the loan is active) and the instance is marked as "on loan".
2. **Given** an instance is on loan, **When** the librarian accepts a return in a condition no worse than before, **Then** the loan is closed, the instance becomes free, and the next person on the waitlist gets the opportunity to reserve.
3. **Given** an instance is on loan, **When** the librarian accepts a return in a condition worse than before, **Then** a maintenance request is created, the instance is marked unavailable until the request is closed, and the damage event affects the member's rating.
4. **Given** an instance with an open maintenance request, **When** the librarian closes the request, **Then** the instance becomes available for reservation again, and the maintenance cost is retained in the history.
5. **Given** a tool has been checked out to a member, **When** the member returns it earlier than the planned date, **Then** the early return is accepted and the instance is freed.

---

### User Story 4 - Return Reminders and Overdue Notices (Priority: P3)

The system reminds a member when a return date is approaching and, in case of an overdue loan, notifies them of the overdue status. A member with an overdue loan cannot create new reservations.

**Why this priority**: Reminders reduce the number of overdue loans without manual oversight by the librarian and keep the pool circulating.

**Independent Test**: Create a loan with a return date in the near future and confirm a "return date approaching" reminder is generated; wait for/simulate an overdue state and confirm an overdue notice is generated and new reservations are blocked.

**Acceptance Scenarios**:

1. **Given** a loan whose return date is approaching, **When** the reminder moment arrives, **Then** a reminder to return the tool is generated for the member.
2. **Given** a loan whose return date has passed, **When** the tool has not been returned, **Then** an overdue notice is generated for the member and the loan is marked overdue.
3. **Given** a member has at least one overdue loan, **When** they try to create a reservation, **Then** the action is blocked until the overdue tool is returned.

---

### User Story 5 - Reliability Rating and Limits (Priority: P3)

Overdue loans and damage lower a member's reliability rating. With a low rating, the system limits the number of concurrent loans. A member can view their own rating and loan history.

**Why this priority**: The rating creates a soft self-regulation mechanism for the community, encouraging careful and timely usage without monetary deposits (which are out of scope).

**Independent Test**: Record an overdue loan and damage for a member and confirm the rating dropped; with a rating below the threshold, confirm the concurrent-loan limit is reduced and an additional reservation is blocked.

**Acceptance Scenarios**:

1. **Given** a member incurred an overdue loan or returned a damaged tool, **When** the event is recorded, **Then** the member's reliability rating decreases.
2. **Given** a member's rating is below the configured threshold, **When** they try to take more loans than the reduced limit allows, **Then** the system blocks the additional reservation/checkout.
3. **Given** a member opens their profile, **When** they view it, **Then** they see their own current rating and the full history of their loans.

---

### User Story 6 - Retiring a Tool While Preserving History (Priority: P4)

The librarian can retire an instance (write-off, sale). The instance stops being available for reservations and checkout, but its entire history of loans, maintenance, and state changes is preserved for auditing.

**Why this priority**: Needed for correct management of the pool over the years, but it does not block day-to-day operations; hence the lower priority.

**Independent Test**: Retire an instance; confirm it cannot be reserved or checked out, but its history remains available to view.

**Acceptance Scenarios**:

1. **Given** an instance without an active loan, **When** the librarian retires it with a stated reason (write-off/sale), **Then** the instance is marked retired and disappears from those available for reservation.
2. **Given** a retired instance, **When** anyone views its history, **Then** the entire history of loans, maintenance, and state changes remains available and unchanged.

---

### User Story 7 - Reports for the Librarian (Priority: P4)

The librarian views reports: most popular tools, current overdue loans, and maintenance cost over a selected period.

**Why this priority**: Analytics helps plan pool replenishment and repairs, but it is derived from accumulated operational data.

**Independent Test**: With historical data present, open a report and confirm it shows the top tools by number of loans, the list of current overdue loans, and the total maintenance cost for a given period.

**Acceptance Scenarios**:

1. **Given** accumulated loan history, **When** the librarian opens the popularity report, **Then** a list of tools ordered by number of loans is shown.
2. **Given** there are active overdue loans, **When** the librarian opens the overdue report, **Then** a list of current overdue loans with their timeframes is shown.
3. **Given** there were maintenance requests with costs during a period, **When** the librarian sets a date range, **Then** the total maintenance cost for that period is shown.

---

### User Story 8 - Managing Members and Rules (Priority: P4)

The administrator manages members and their membership, and configures system rules: maximum loan term, concurrent-loan limit, and rating thresholds.

**Why this priority**: Configuration is needed to adapt the system to a specific community, but reasonable default values can work for the first launch.

**Independent Test**: Add/deactivate a member; change the maximum loan term and the concurrent-loan limit and confirm the new rules apply to subsequent reservations and checkouts.

**Acceptance Scenarios**:

1. **Given** the administrator is in the management section, **When** they add a new member or deactivate an existing one, **Then** access and the ability to take loans change accordingly.
2. **Given** the administrator changes the maximum loan term, **When** a member creates a new reservation, **Then** the system limits the date range to the new maximum term.
3. **Given** the administrator changes the concurrent-loan limit or the rating threshold, **When** members try to exceed the limit, **Then** the new rule is applied.

---

### Edge Cases

- What happens when a member is on a waitlist and the reservation ahead of them is cancelled or completed early — the earliest-joined waitlist member (FIFO) receives a time-boxed offer; if it expires unconfirmed, the offer rolls to the next member.
- What happens when the return date has arrived but there is already a waitlist for the instance — is the next person on the waitlist notified of the delay?
- How does the system behave if the librarian tries to check out an instance that was in fact not returned by the previous member (overdue and still on loan)?
- What happens to active reservations and the waitlist if an instance suddenly moves to a maintenance state (damage discovered outside of a return)?
- How is an attempt handled to retire an instance that is currently on loan or has active future reservations?
- What happens when a return is recorded in a condition better than before (e.g., after repair) — does this affect the rating?
- How are simultaneous attempts by two members to reserve the last free instance at the same moment resolved?
- What happens to ratings and limits when the administrator changes the thresholds while a member is already near the boundary?

## Requirements *(mandatory)*

### Functional Requirements

**Catalog and Instances**

- **FR-001**: The system MUST allow the librarian to create, edit, and organize catalog entries for tools with a name and a category.
- **FR-002**: The system MUST treat each physical instance as a separate unit with a unique inventory number; identical models are separate instances.
- **FR-003**: The system MUST allow members to search and filter tools by name and category.
- **FR-004**: The system MUST show, for each instance, its current status (free, taken until a date, on loan, under maintenance, retired).
- **FR-005**: The system MUST show until what date an instance is taken; the current loan holder MUST be visible to the librarian (visibility of the holder to other members is hidden by default, see Assumptions).

**Reservations and Waitlist**

- **FR-006**: The system MUST allow a member to reserve a free instance for a date range.
- **FR-007**: The system MUST prevent a single physical instance from being checked out to two members for overlapping periods.
- **FR-008**: The system MUST offer a member to join a waitlist if an instance is taken for the desired period.
- **FR-009**: The system MUST prevent creating a reservation for an instance that is under maintenance or retired.
- **FR-010**: The system MUST prevent creating new reservations for a member who has at least one overdue loan.
- **FR-011**: The system MUST allow cancelling a reservation up until the moment of checkout; after checkout, the only change possible is an early return.
- **FR-012**: The system MUST limit the reservation date range to the maximum loan term configured by the administrator.
- **FR-013**: The system MUST limit the number of a member's active reservations/loans according to the concurrent-loan limit (accounting for the reduced limit under a low rating).
- **FR-014**: The system MUST determine the next claimant from the waitlist when an instance is freed using FIFO order (the member who joined the waitlist earliest gets the first offer); ties are impossible since join time is unique per waitlist.

**Checkout, Return, Maintenance**

- **FR-015**: The system MUST allow the librarian to check out an instance against an existing reservation, recording the fact and time of checkout.
- **FR-016**: The system MUST allow the librarian to record the condition of an instance upon return using a fixed ordered scale with four levels: New → Good → Worn → Damaged (New is best, Damaged is worst).
- **FR-017**: The system MUST automatically create a maintenance request if the recorded return condition is a lower level than the instance's prior condition, and make the instance unavailable until the request is closed. A return at the same or a higher level does not trigger maintenance.
- **FR-018**: The system MUST allow the librarian to close a maintenance request with a stated maintenance cost, after which the instance becomes available again.
- **FR-019**: The system MUST support early return of a tool that is already checked out.
- **FR-020**: The system MUST free an instance after return and trigger a review of the waitlist.
- **FR-020a**: When a freed instance reaches the next waitlist member (in FIFO order), the system MUST issue a time-boxed offer: the member is notified and must confirm a reservation within a configurable offer window. If the member does not confirm before the window expires, the offer MUST automatically roll to the next member in FIFO order. The offer window duration is configurable by the administrator as part of CommunityRules.

**Reminders and Rating**

- **FR-021**: The system MUST generate reminders about an approaching loan return date.
- **FR-022**: The system MUST generate notices about an overdue loan and mark such a loan as overdue.
- **FR-023**: The system MUST deliver notifications to members through two channels: in-app notifications within the system and email. The delivery mechanism MUST be extensible so that additional channels (e.g., SMS, messenger) can be added later without changing the notification-generating logic.
- **FR-024**: The system MUST maintain a reliability rating as an integer score from 0 to 100, initialized to 100 for a new member. An overdue loan and a return with a worsened condition each subtract a configurable number of points; each on-time return with no worsened condition adds a smaller configurable number of points. The score MUST be clamped to the 0–100 range (never exceeding 100 or dropping below 0). The point values (overdue penalty, damage penalty, clean-return reward) are configurable by the administrator as part of CommunityRules.
- **FR-025**: The system MUST limit the number of concurrent loans when a member's rating is below the configured threshold.
- **FR-026**: A member MUST be able to view their own current rating and the history of their loans.

**Retirement and History**

- **FR-027**: The system MUST allow the librarian to retire an instance (write-off, sale) with a stated reason.
- **FR-028**: The system MUST prevent reservation and checkout of a retired instance while preserving its history.
- **FR-029**: The system MUST maintain an immutable (append-only) history of loans, maintenance, and state changes for auditing purposes; existing records are not edited or deleted.

**Reports**

- **FR-030**: The system MUST provide the librarian with a report on the most popular tools by number of loans.
- **FR-031**: The system MUST provide the librarian with a report on current overdue loans.
- **FR-032**: The system MUST provide the librarian with a report on the total maintenance cost over a selected period.

**Roles and Administration**

- **FR-033**: The system MUST distinguish three roles — Member, Librarian, Administrator — and restrict actions according to role.
- **FR-034**: The administrator MUST be able to manage members and their membership (adding, deactivating).
- **FR-035**: The administrator MUST be able to configure rules: maximum loan term, concurrent-loan limit, rating thresholds.
- **FR-036**: The system MUST require authentication; public access without registration is prohibited.

### Key Entities *(include if feature involves data)*

- **Member**: a person from the community who borrows tools; has a role, membership status, a current reliability rating (integer 0–100, default 100), and a loan history.
- **Role**: a set of permitted actions — Member, Librarian, Administrator.
- **Tool / Catalog Entry**: the logical model of a tool with a name and category; groups one or more instances.
- **ToolInstance**: a specific physical unit with a unique inventory number, a current condition, and a status (free / on loan / under maintenance / retired).
- **Category**: a classification of tools for search and filtering.
- **Reservation**: a member's intent to use an instance for a date range; has a status (active, cancelled, realized as a loan).
- **WaitlistEntry**: a record of a member waiting for a taken instance to be freed, with the join time that establishes FIFO order and the state of any active time-boxed offer (offered / confirmed / expired).
- **Loan**: the fact of an instance being checked out to a member, with checkout and planned/actual return dates and an overdue flag.
- **MaintenanceRequest**: a record of the need to repair an instance, with a status (open/closed) and a maintenance cost.
- **StateChange / AuditEvent**: an immutable append-only record of instance state changes and key events for auditing.
- **Condition**: the recorded condition level of an instance on a fixed ordered 4-level scale (New → Good → Worn → Damaged), compared between checkout and return to detect worsening.
- **CommunityRules**: settings — maximum loan term, concurrent-loan limit, rating thresholds, reliability point values (overdue penalty, damage penalty, clean-return reward), and the waitlist offer-window duration.
- **Notification**: a message to a member about an approaching deadline, an overdue loan, etc.; delivered via in-app and email channels, with a delivery mechanism designed to accept additional channels later.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A member finds the needed tool and determines its availability (free / taken until a date) in no more than 30 seconds from opening the catalog.
- **SC-002**: At any moment 100% of instances have an unambiguous, up-to-date status, and the system shows the current holder for every checked-out instance (eliminating the "nobody knows who has the rotary hammer" situation).
- **SC-003**: No physical instance is ever checked out to two members for overlapping periods (0 double-checkout conflicts).
- **SC-004**: 100% of returns with a worsened condition automatically generate a maintenance request, and no such instance is available for reservation until the request is closed.
- **SC-005**: The share of tools sitting idle in a broken state beyond a community-set timeframe drops by at least 70% within three months of rollout (eliminating "broken things sit unrepaired for years").
- **SC-006**: Members receive return-deadline reminders in advance for 100% of active loans; the share of overdue loans drops by at least 50% compared to the previous messenger/Excel record-keeping.
- **SC-007**: A member with an overdue loan is, in 100% of cases, unable to create a new reservation until they return the overdue tool.
- **SC-008**: The librarian produces any of the reports (popularity, overdue, maintenance cost over a period) in no more than 1 minute without manual data consolidation.
- **SC-009**: 100% of loan, maintenance, and state-change operations are preserved in an immutable history and available for auditing, including retired instances.
- **SC-010**: The community fully abandons parallel record-keeping in messengers and Excel within one month of rollout.

## Assumptions

- One installation serves exactly one community; support for multiple independent communities within one installation is out of scope.
- The system is a web application with mandatory authentication; a separate mobile application and public (unregistered) access are out of scope.
- Payments, deposits, and any monetary transactions are out of scope; "maintenance cost" is recorded only as an accounting value for reports, without payment processing.
- Roles are hierarchical by capability: Administrator ⊇ Librarian ⊇ Member unless stated otherwise; a single person may hold a combination of roles.
- "Condition worse than before" is determined by a fixed ordered 4-level condition scale (New → Good → Worn → Damaged) recorded by the librarian; the scale is not configurable in v1.
- Visibility of the current loan holder to other ordinary members is hidden by default for privacy reasons; the librarian and administrator always see the holder.
- Default rule values (maximum loan term, concurrent-loan limit, rating thresholds) are set at rollout and further adjusted by the administrator.
- Reminders are generated in advance a fixed interval before the return date; the specific interval is configurable (a reasonable default value).
- Cancelling a reservation after checkout is impossible; an early return is used instead.

## Outstanding Clarifications

All previously flagged open questions have been resolved — see the [Clarifications](#clarifications) section (Session 2026-07-27):

- **Waitlist priority mechanics** (FR-014) — resolved: FIFO by join time.
- **Reliability rating formula** (FR-024) — resolved: 0–100 points, start at 100, configurable penalties/reward, clamped.
- **Notification delivery channels** (FR-023) — resolved: in-app + email, extensible to more channels.
