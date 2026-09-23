# Feature Specification: Membership & Community Rules

**Feature Branch**: `003-membership-rules`

**Created**: 2026-07-30

**Status**: Draft

**Input**: User description: "Prepare specification for next features" — resolved to the next feature in the roadmap recorded by [specs/002-catalog-foundation/spec.md](../002-catalog-foundation/spec.md): the **Membership** module (members, roles, membership status, reliability rating, and community rules), which Lending, Maintenance and Notifications (004+) all depend on.

## Context

The product specification lives in [specs/001-tool-library/spec.md](../001-tool-library/spec.md) and governs shared concepts referenced here without being restated: the three roles Member / Librarian / Administrator; the reliability rating as an integer 0–100 starting at 100 with configurable penalties and reward, clamped; the configurable rules (maximum loan term, concurrent-loan limit, rating thresholds, waitlist offer window); mandatory authentication with no public access.

[002-catalog-foundation](../002-catalog-foundation/spec.md) delivered the modular-monolith foundation and the Catalog module, deliberately deferring member management: it seeds a single bootstrap administrator and a `Librarian` role, and gates catalog browsing on *authentication* rather than on a permission precisely because member management was out of scope until the Membership feature.

This feature closes that gap. It turns "whoever can sign in" into a governed community of members with a status, a role, a reliability standing, and one authoritative set of community rules. It is the last enabling slice before Lending: every borrowing decision in 004+ — may this person borrow, how many things at once, for how long, what does a late or damaged return cost them — is answered from data this feature owns.

Like Catalog, Membership publishes a stable public boundary that downstream modules consume without reaching into its internals. Unlike Catalog, that boundary is not read-only: Lending must be able to *report* rating-affecting outcomes (an overdue return, a damaged return, a clean return) back to Membership, because the rating is Membership's data to change.

## Clarifications

### Session 2026-07-30

- Q: Does a member hold exactly one role, or a combination of roles? → A: Exactly one, hierarchical — each member holds one of Member / Librarian / Administrator, and each level implicitly carries every capability of the levels below it (Administrator ⊇ Librarian ⊇ Member).
- Q: What access does an authenticated user get if they are not an enrolled active member? → A: None — enrolment is the gate. Any authenticated user who is not an enrolled member with Active status is refused all application access and shown an explanatory page. Enforcement is an application-level check on every request; the member's sign-in credential itself is never disabled, so deactivation and reactivation stay a single-system change.
- Q: Which membership facts are recorded as append-only history? → A: All of them, in one unified stream per member — enrolment, status change, role change, and rating outcome are entries in a single append-only standing history, each typed by what changed. Mirrors Catalog's `ToolInstanceStateChange` and keeps history entries one-to-one with the standing-changed event (FR-028).
- Q: How are concurrent conflicting writes handled? → A: Split by audience — human-facing edits (member record, community rules) use optimistic concurrency and reject the second writer with a conflict message, matching 002's FR-010; rating-outcome reports from other modules are serialized per member and retried internally so the caller never sees a conflict and no outcome is lost.
- Q: Does enrolling a member also create their sign-in account? → A: Yes — one Administrator action takes a display name, email, and an initial password and produces both the sign-in account and the member record; the member must change the password on first sign-in. There is no separate account-creation step and no email-based invitation. The one account that predates this feature (the 002 bootstrap administrator) is handled by FR-010 at seed time.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Administer the community roster (Priority: P1)

An Administrator maintains the list of people who may use the tool library: they enrol a person as a member — one action that creates both the person's sign-in account and their member record — assign their role (Member, Librarian, or Administrator), and deactivate a member who leaves the community, later reactivating them if they return. Deactivated members, and anyone who can authenticate but was never enrolled, can no longer use the system, but every member record and its history are preserved.

**Why this priority**: This is the core value of the slice and the prerequisite for everything else in it. Without a roster there is no subject for a rating, no owner for a loan, and no way for Lending to ask "is this person allowed to borrow?". It also removes the 002 stopgap in which anyone who could authenticate was implicitly a full user.

**Independent Test**: Sign in as an Administrator, enrol two people as members, give one of them the Librarian role, deactivate the other, confirm the deactivated one is refused access while their record remains visible to the Administrator, then reactivate them and confirm access is restored.

**Acceptance Scenarios**:

1. **Given** a signed-in Administrator, **When** they enrol a new person with a display name, email, and initial password, **Then** a sign-in account and a member record are created together, with Active status, the Member role, and a starting reliability rating of 100.
2. **Given** a newly enrolled member, **When** they sign in for the first time, **Then** they are required to change the Administrator-set password before they can use anything else.
3. **Given** an existing member, **When** the Administrator assigns them the Librarian role, **Then** they gain the capabilities that role carries and the change is visible on the member record.
4. **Given** an Active member, **When** the Administrator deactivates them with a stated reason, **Then** the member is marked Deactivated, is refused access to the system, and their record, rating, and history remain retrievable by the Administrator.
5. **Given** a Deactivated member, **When** the Administrator reactivates them, **Then** the member regains access with their previous rating unchanged (deactivation does not reset standing).
6. **Given** the Administrator attempts to enrol someone whose email or sign-in name already belongs to an existing account or member record, **Then** the system rejects it and states which value is already in use.
7. **Given** the Administrator is the only remaining active Administrator, **When** they attempt to deactivate themselves or remove their own Administrator role, **Then** the system refuses and explains that at least one active Administrator must remain.

---

### User Story 2 - Configure the community's rules (Priority: P1)

An Administrator opens a single "community rules" settings area and adjusts the values that govern borrowing: the maximum loan term, the normal concurrent-loan limit, the low-rating threshold and the reduced limit that applies below it, the point values for an overdue return, a damaged return, and a clean return, the waitlist offer window, and the return-reminder lead time. A fresh installation already carries sensible defaults, so the system is usable before anyone touches this screen.

**Why this priority**: These values are inputs to every eligibility decision Lending will make, and to every rating change this module records. They must exist, be editable, and be readable by other modules from day one; hard-coding them and "making them configurable later" would mean rewriting the eligibility contract.

**Independent Test**: On a fresh installation, confirm every rule has a documented default; change the maximum loan term and the concurrent-loan limit, confirm the new values are persisted and reported, and confirm an out-of-range value (e.g. a negative limit, or a low-rating threshold above 100) is rejected with a clear message.

**Acceptance Scenarios**:

1. **Given** a freshly installed system, **When** anyone reads the community rules, **Then** every rule has a defined default value and none is empty or undefined.
2. **Given** a signed-in Administrator, **When** they change the maximum loan term and save, **Then** the new value is persisted and is what other parts of the system read from that moment on.
3. **Given** a signed-in user who is not an Administrator, **When** they attempt to change the community rules, **Then** the action is denied.
4. **Given** the Administrator enters an invalid value (a non-positive loan term or concurrent-loan limit, a rating threshold outside 0–100, a negative point value, or a reduced limit greater than the normal limit), **When** they save, **Then** the change is rejected with a message naming the offending rule.
5. **Given** a rule is changed, **When** anyone inspects the rules, **Then** they can see when it was last changed and by whom.
6. **Given** rules are changed while members are already near a limit, **When** the change is saved, **Then** it applies only to decisions made from that point forward and never retroactively alters recorded history.
7. **Given** two Administrators opened the rules screen at the same time, **When** the second one saves after the first, **Then** their save is rejected as a conflict with a message telling them to reload and reapply, and the first Administrator's change is preserved intact.

---

### User Story 3 - See my standing as a member (Priority: P2)

A signed-in member opens their own profile and sees their membership status, their role, their current reliability rating, and the dated list of events that moved it — "returned late, −10" — so the score is explainable rather than a mystery number. A Librarian or Administrator can view the same information for any member.

**Why this priority**: A rating that a member cannot see or account for cannot change behaviour, which is the entire purpose of the mechanism (product spec US5). It is P2 rather than P1 because the roster and the rules must exist before there is anything to display.

**Independent Test**: Record a few rating events for a member, sign in as that member, open the profile, and confirm the current score matches the clamped running total of the listed events and that each entry shows what happened, when, and how many points it moved. Confirm a member cannot open another member's profile, while a Librarian can.

**Acceptance Scenarios**:

1. **Given** a signed-in member, **When** they open their profile, **Then** they see their status, role, current rating, and their chronological standing history — rating outcomes with the points each contributed, alongside the enrolment, status, and role changes on their record.
2. **Given** a newly enrolled member with no rating outcomes yet, **When** they open their profile, **Then** they see the starting rating of 100 and a clear empty state for rating movement rather than an error.
3. **Given** a signed-in member, **When** they attempt to view another member's profile or standing history, **Then** access is denied.
4. **Given** a signed-in Librarian or Administrator, **When** they open any member's profile, **Then** they see that member's status, role, rating, and standing history.
5. **Given** a member is viewing their profile, **When** they attempt to change their own rating, status, or role, **Then** no such action is offered and any direct attempt is denied.

---

### User Story 4 - Record standing changes as append-only history (Priority: P2)

The system records every change to a member's standing — enrolment, deactivation and reactivation, role change, and each rating-affecting outcome (an overdue return, a return in worsened condition, a clean on-time return) — as a new, immutable entry in one history stream per member. Rating entries carry the points applied and the resulting score, so the member's current rating is the running result of that history, always within 0–100. An Administrator who needs to correct a mistake records a compensating adjustment with a reason; nothing is ever edited or deleted.

**Why this priority**: Constitution Principle IV (Append-Only History) makes this non-negotiable for auditability, and the rating is worthless if it can be quietly rewritten. It follows US3 because it is the mechanism behind what US3 displays, and it is exercised in earnest only once Lending reports real outcomes.

**Independent Test**: Apply an overdue outcome and a damage outcome to a member with the default point values, confirm the score dropped by exactly the configured amounts and that two history entries exist; apply clean returns until the score would exceed 100 and confirm it stops at 100; apply penalties until it would fall below 0 and confirm it stops at 0; attempt to modify or delete a history entry and confirm it is impossible.

**Acceptance Scenarios**:

1. **Given** a member with rating 100 and an overdue penalty of 10, **When** an overdue outcome is recorded, **Then** a new history entry is appended with −10 and the member's current rating becomes 90.
2. **Given** a member at rating 100, **When** a clean-return outcome is recorded, **Then** the rating stays at 100 (clamped) and the history entry records that 0 effective points were applied.
3. **Given** a member at rating 5 and a damage penalty of 20, **When** a damage outcome is recorded, **Then** the rating becomes 0 rather than negative, and the entry records the effective points applied.
4. **Given** an outcome has already been recorded for a specific originating occurrence, **When** the same occurrence is reported again, **Then** the system does not apply the points a second time.
5. **Given** an Administrator, **When** they record a manual adjustment with a reason, **Then** a new entry is appended attributing the adjustment to them, and the previous entries are untouched.
6. **Given** any existing standing-history entry of any kind, **When** anyone attempts to alter or remove it, **Then** the attempt fails and the entry remains as originally written.
7. **Given** a member's rating crosses the low-rating threshold in either direction, **When** the entry is recorded, **Then** the change in the member's borrowing standing is announced so other parts of the system can react.
8. **Given** an Administrator deactivates a member and later changes their role, **When** the member's history is inspected, **Then** both transitions appear as their own entries in the same stream as the rating entries, each showing the previous and new value, the moment, and who made the change.
9. **Given** any single standing transition, **When** it is recorded, **Then** exactly one history entry and exactly one published event result from it — never two of either, never one without the other.

---

### User Story 5 - Publish a stable Membership boundary for Lending (Priority: P2)

A downstream module author (Lending, Maintenance, Notifications — 004+) can, using only Membership's published contracts: ask whether a given person is an enrolled, active member and what their current borrowing allowance is; read the community rules; report a rating-affecting outcome back to Membership; and subscribe to a Membership event to react when a member's standing changes. None of this requires referencing Membership's internal implementation.

**Why this priority**: Lending is the immediate next feature and is blocked on exactly this surface. Defining it here — with the same discipline Catalog used — is what prevents Lending from reaching into Membership's internals and collapsing the module boundary.

**Independent Test**: From a separate module or test that references only Membership's published contracts, look up a member's standing, read the community rules, report an overdue outcome, and receive the standing-changed event triggered by that report — all without referencing any Membership-internal type.

**Acceptance Scenarios**:

1. **Given** the published Membership contracts, **When** another module asks for a person's membership standing by identity, **Then** it receives whether they are an enrolled active member, their current rating, and their effective concurrent-loan limit — without referencing Membership's internal types.
2. **Given** a person who is not enrolled, or is enrolled but Deactivated, **When** another module asks for their standing, **Then** the answer clearly distinguishes "not a member" from "member, but not active" rather than failing.
3. **Given** the published contracts, **When** another module needs the community rules, **Then** it can read every rule value through the contract.
4. **Given** the published contracts, **When** another module reports an overdue, damage, or clean-return outcome for a member together with the identifier of the occurrence it came from, **Then** Membership records it and the member's rating reflects it.
5. **Given** a subscribed handler for the Membership standing-changed event, **When** a member is deactivated, reactivated, or their rating crosses the low-rating threshold, **Then** the handler receives the event with the identifiers needed to react.
6. **Given** Membership's internal implementation changes without changing the published contract, **When** dependent modules are rebuilt, **Then** they continue to work unchanged.

---

### Edge Cases

- What happens when an Administrator deactivates a member who currently holds borrowed tools? Membership cannot see loans; deactivation succeeds, blocks all *future* borrowing immediately, and announces the standing change so a later module can surface the outstanding items. Existing loans are not cancelled and must still be returned.
- What happens when a member's role is changed while they are signed in? The new role governs their next action; capabilities are evaluated per action rather than fixed for the lifetime of a session.
- What happens when an Administrator lowers the concurrent-loan limit below the number of items a member already holds? The rule applies only to new borrowing decisions; nothing already recorded is invalidated or retroactively made illegal.
- What happens when an outcome is reported for an identity that has no member record? It is rejected with a clear "not an enrolled member" answer rather than silently creating a member.
- What happens when two rating outcomes for the same member are reported at the same instant? They are serialized, both are appended in a deterministic order, and both are reflected in the resulting score; neither is lost and neither caller sees a contention error (FR-032).
- What happens when two Administrators save the community rules, or the same member record, at the same time? The second save is rejected as a conflict and the Administrator is told to reload and reapply their change, rather than silently overwriting the first (FR-031).
- What happens when the low-rating threshold is changed such that many members cross it at once? Their effective limits change immediately for future decisions, and the crossings are announced without rewriting any history.
- What happens when a member's sign-in identity is removed at the identity level while a member record still points at it? The member record remains and is reported as unusable rather than causing an unhandled failure.
- What happens when a member is deactivated while they have a session already open? Their credential still authenticates, but the per-request enrolment check refuses their very next action and shows the explanatory page (FR-006, FR-006a).
- What happens when someone can authenticate but was never enrolled — for example an identity account created directly by an operator? They reach only the explanatory page; no catalog, no profile, no borrowing (FR-006a).
- What happens if the bootstrap administrator from 002 has no member record when this feature is installed? The installation step creates one, so the system is never in a state where the only usable account is not a member.
- What happens when a manual rating adjustment would push the score outside 0–100? It is clamped exactly as automatic outcomes are, and the entry records the effective points applied.

## Requirements *(mandatory)*

### Functional Requirements — Roster & Roles

- **FR-001**: The system MUST allow an Administrator to enrol a person as a member in a single action that captures a display name, a contact email, and an initial password, and that creates both the person's sign-in account and their member record together. No separate account-creation step and no email-based invitation is required.
- **FR-001a**: The system MUST require a member to change the Administrator-set initial password on their first sign-in, before they can use any other part of the application.
- **FR-002**: The system MUST guarantee that a person is enrolled at most once, rejecting an enrolment whose email or sign-in name already belongs to an existing account or member record, with a clear message naming the conflict.
- **FR-003**: The system MUST assign every newly enrolled member the Member role, Active status, an enrolment date, and a starting reliability rating of 100.
- **FR-004**: The system MUST give every member exactly one role — Member, Librarian, or Administrator — and MUST allow an Administrator to change it, applying the capabilities of the new role to the member's subsequent actions. The roles are hierarchical: each level carries every capability of the levels below it (Administrator ⊇ Librarian ⊇ Member), so a Librarian needs no separate Member role to browse and borrow.
- **FR-005**: The system MUST allow an Administrator to deactivate a member with a stated reason and to reactivate a deactivated member; reactivation MUST restore the member's previous rating unchanged.
- **FR-006**: The system MUST deny application access to a deactivated member while preserving their record, rating, and history for Administrators and Librarians to view. The denial MUST be enforced as a check on every request rather than by disabling the member's sign-in credential, so that deactivation and reactivation are a single-system change and a member with a session already open is refused on their very next action.
- **FR-006a**: The system MUST refuse all application functionality — including the catalog browsing that 002 gated on authentication alone — to any authenticated user who is not an enrolled member with Active status, instead presenting a page that explains the situation and directs them to an Administrator. This requirement supersedes the interim "any authenticated user may browse" rule established in [002](../002-catalog-foundation/contracts/catalog-permissions.md).
- **FR-007**: The system MUST prevent the last active Administrator from being deactivated or from losing the Administrator role, so the community can never be left unadministrable.
- **FR-008**: The system MUST allow an Administrator or Librarian to list and search members by name, email, status, and role.
- **FR-009**: The system MUST never hard-delete a member record; ending a membership is expressed as deactivation.
- **FR-010**: The system MUST create a member record for the bootstrap administrator on installation, so no installation exists in which the only usable account is not an enrolled member.

### Functional Requirements — Community Rules

- **FR-011**: The system MUST maintain exactly one authoritative set of community rules for the installation, comprising: maximum loan term, normal concurrent-loan limit, low-rating threshold, reduced concurrent-loan limit applied below that threshold, overdue penalty points, damage penalty points, clean-return reward points, waitlist offer window, and return-reminder lead time.
- **FR-012**: The system MUST supply a defined default value for every rule on a fresh installation, so the system is fully usable before any rule is edited.
- **FR-013**: The system MUST restrict changing community rules to Administrators, while allowing any active member to read the values that affect them.
- **FR-014**: The system MUST validate rule values on save and reject, with a message naming the offending rule: a non-positive maximum loan term, a non-positive concurrent-loan limit, a low-rating threshold outside 0–100, a negative point value, a reduced limit greater than the normal limit, or a non-positive offer window or reminder lead time.
- **FR-015**: The system MUST record when each rule change was made and by whom, and MUST apply rule changes only to decisions made after the change — never retroactively to recorded history.

### Functional Requirements — Reliability Rating

- **FR-016**: The system MUST express a member's reliability rating as an integer from 0 to 100, computed as the running result of the rating-outcome entries in that member's standing history and always clamped to that range.
- **FR-017**: The system MUST record every change to a member's standing as a new, immutable entry in a **single append-only history stream per member**, covering enrolment, status change, role change, and rating outcome. Every entry MUST capture the kind of change, the previous and new values it affects, the moment it occurred, and who caused it. Rating entries MUST additionally capture the outcome type, the points applied after clamping, the resulting score, and the originating occurrence they refer to.
- **FR-017a**: Each standing transition MUST produce exactly one history entry and exactly one published event (FR-028), so the audit trail and what other modules observe can never diverge.
- **FR-018**: The system MUST support at least these outcome types: overdue return (subtracts the configured overdue penalty), return in worsened condition (subtracts the configured damage penalty), clean on-time return (adds the configured reward), and manual administrative adjustment (applies a stated signed value with a mandatory reason).
- **FR-019**: The system MUST prevent an outcome from being applied twice for the same originating occurrence, so a repeated report is recorded as already-handled rather than double-penalising the member.
- **FR-020**: The system MUST prevent any existing standing-history entry — of any kind — from being edited or deleted; corrections are expressed as new compensating entries.
- **FR-021**: The system MUST restrict recording a manual adjustment to Administrators and MUST require a reason for it.
- **FR-022**: The system MUST allow a member to view their own current rating and their complete standing history, and MUST prevent a member from viewing another member's rating or history; Librarians and Administrators MUST be able to view any member's.
- **FR-023**: The system MUST derive a member's effective concurrent-loan limit from the rules: the normal limit when the rating is at or above the low-rating threshold, and the reduced limit when it is below.

### Functional Requirements — Published Boundary

- **FR-024**: The system MUST publish a stable public contract through which another module can obtain a member's standing by sign-in identity or member identifier, returning at minimum: whether the person is an enrolled member, whether they are active, their current rating, and their effective concurrent-loan limit.
- **FR-025**: The standing lookup MUST distinguish "not an enrolled member" from "enrolled but not active", and MUST answer for an unknown identity without failing.
- **FR-026**: The system MUST publish a stable public contract through which another module can read all community rule values.
- **FR-027**: The system MUST publish a stable public contract through which another module can report a rating-affecting outcome for a member, supplying the outcome type and the identifier of the originating occurrence, and MUST reject a report for a person who is not an enrolled member.
- **FR-028**: The system MUST publish an event, subscribable by other modules, announcing a change in a member's standing — enrolment, deactivation, reactivation, role change, and a rating change that crosses the low-rating threshold — carrying the identifiers needed to react.
- **FR-029**: The system MUST keep Membership's internal implementation changeable without breaking dependent modules so long as the published contract is unchanged, and MUST NOT require Membership to know which modules consume it.
- **FR-030**: The system MUST define and enforce a Membership permission boundary consistent with the one Catalog established: roster and rules administration restricted to Administrators, member browsing available to Librarians and Administrators, and self-service profile access available to the member themselves.

### Functional Requirements — Concurrency

- **FR-031**: The system MUST detect concurrent conflicting edits to the same member record or to the community rules and reject the later write with a clear conflict message rather than silently overwriting, consistent with the rule 002 established for catalog records.
- **FR-032**: The system MUST NOT surface a conflict to a module reporting a rating outcome: concurrent reports for the same member MUST be serialized and retried internally so that every reported outcome is applied exactly once and the resulting rating equals the clamped total of all of them. A caller therefore sees either success or a genuine rejection (unknown member, duplicate occurrence), never a contention failure.

### Key Entities *(include if feature involves data)*

- **Member**: a person enrolled in the community — display name, contact email, the sign-in identity they authenticate as (referenced by identifier only), enrolment date, membership status, assigned role, and current reliability rating. Never hard-deleted.
- **MembershipStatus**: the lifecycle state of a membership — Active or Deactivated — together with the reason and moment of the most recent change.
- **CommunityRole**: the single capability level attached to a member — Member, Librarian, or Administrator — as defined by the product spec. Exactly one per member, hierarchical (Administrator ⊇ Librarian ⊇ Member), so the level held implies every capability below it.
- **MemberStandingChange**: an immutable, append-only record of one change to a member's standing — the kind of change (enrolment, status change, role change, or rating outcome), the previous and new values it affects, the moment, the actor, and an optional reason. Rating-outcome entries additionally carry the outcome type, the points applied after clamping, the resulting score, and the originating occurrence identifier. This is the single history stream per member; the member's rating is the running result of its rating-outcome entries. (Same shape of idea as Catalog's instance state-change history.)
- **ReliabilityOutcomeType**: the classification of a rating-outcome entry — overdue return, worsened-condition return, clean on-time return, or manual adjustment.
- **CommunityRules**: the single authoritative configuration set for the installation (maximum loan term, normal and reduced concurrent-loan limits, low-rating threshold, overdue/damage/clean-return point values, waitlist offer window, reminder lead time) with its last-changed metadata.
- **MemberStandingSnapshot**: the published shape other modules consume — enrolled, active, current rating, effective concurrent-loan limit — deliberately *not* including anything about loans, which Membership does not know about.
- **MembershipPermission**: the authorization boundary distinguishing roster administration, rules administration, member browsing, and self-service access.
- **Membership Standing Event**: the published notification that a member's standing changed, which other modules subscribe to.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An Administrator can enrol a new member and put them in a working state (role assigned, able to sign in) end-to-end in under 2 minutes without assistance.
- **SC-002**: 100% of attempts to enrol someone whose email or sign-in name is already in use are rejected.
- **SC-003**: 100% of roster-administration and rules-administration actions are blocked for users without the corresponding permission and for unauthenticated visitors (0 unauthorized successes).
- **SC-004**: 100% of attempts by a member to view another member's rating or standing history are denied, while Librarians and Administrators succeed in 100% of the same attempts.
- **SC-005**: A deactivated member is denied access in 100% of attempts — including with a session opened before deactivation — and an authenticated user with no member record is denied in 100% of attempts; in both cases the record (where one exists) and its full standing history remain retrievable by an Administrator.
- **SC-006**: It is impossible to leave the community without at least one active Administrator — 100% of attempts to deactivate or demote the last one are refused.
- **SC-007**: On a fresh installation, 100% of community rules have a defined default value and the system is operable without editing any of them.
- **SC-008**: A member's displayed rating equals the clamped running total of the rating-outcome entries in their standing history in 100% of cases, and never falls outside 0–100 regardless of the sequence of outcomes applied.
- **SC-009**: Reporting the same originating occurrence more than once changes the member's rating exactly once (0 double-applied outcomes).
- **SC-009a**: When multiple distinct outcomes for one member are reported concurrently, the resulting rating equals the clamped total of every one of them (0 lost updates), and no caller receives a contention failure.
- **SC-010**: 100% of standing changes — enrolment, status changes, role changes, and rating outcomes — are preserved as immutable history and remain available for auditing, including for deactivated members, and each corresponds to exactly one published event.
- **SC-011**: A separate module or test can look up a member's standing, read the community rules, report an outcome, and receive the standing-changed event without referencing any Membership-internal type (verified by a passing integration test).
- **SC-012**: A member can determine, from their own profile alone, why their rating has its current value — every point of difference from 100 is accounted for by a listed, dated entry.

## Assumptions

- **The technology stack is a constraint inherited from the constitution and prior features, not a choice made by this spec**: the Membership module follows the same modular-monolith shape as Catalog — its own module projects, its own database schema, references to other modules' data by identifier only, and a published contracts surface. These are recorded here as inputs; the requirements above stay capability-focused.
- Membership is delivered as a distinct module alongside Catalog, per the one-module-at-a-time workflow; Lending, Maintenance, and Notifications remain out of scope and are features 004+.
- There is no self-registration: an Administrator enrols members, per product spec FR-034 and the "authentication required, no public access" rule. Invitation flows and email onboarding are out of scope for this feature.
- Credential storage, password rules, and sign-in remain the responsibility of the existing built-in identity mechanism established in 002. Enrolment *drives* that mechanism to create the account (FR-001) but Membership itself stores only the resulting identity's identifier — never a credential — and the member record is the community-facing layer on top of it.
- Password reset for a member who forgets theirs is handled by the existing identity mechanism and is not re-specified here; the Administrator-set initial password (FR-001) exists only to bootstrap first access without email infrastructure.
- The three roles map onto the role mechanism already seeded in 002 (which created the `Librarian` role and granted it the Catalog management permissions); this feature governs the *assignment* of a role to a member, not the re-invention of the role system. Because the roles are hierarchical and a member holds exactly one (FR-004), the higher levels must be granted the capabilities of the lower ones rather than relying on a member holding several roles at once.
- One installation serves exactly one community, so there is exactly one set of community rules and no per-group overrides (per the product spec).
- Membership deliberately knows nothing about loans, reservations, or maintenance. It publishes a member's standing and allowance; the consuming module combines that with its own knowledge (how many items the member currently holds, whether any is overdue) to reach a borrowing decision — mirroring how Catalog publishes availability and lets Lending compute borrowability. Overdue-based blocking (product spec FR-010) is therefore Lending's decision, made using Membership's standing plus Lending's own loan data.
- Because Lending does not exist yet, rating-affecting outcomes are *reported into* Membership through its published contract rather than Membership subscribing to Lending events; this keeps the dependency direction Membership ← Lending, matching the module boundary rules.
- Default rule values assumed for a fresh installation, all editable by an Administrator: maximum loan term 14 days, concurrent-loan limit 3, low-rating threshold 50, reduced concurrent-loan limit 1, overdue penalty 10 points, damage penalty 20 points, clean-return reward 2 points, waitlist offer window 24 hours, return-reminder lead time 2 days.
- Deactivating a member does not cancel or recall anything they already hold; it blocks future borrowing and announces the change so a later module can act on outstanding items.
- Standing history is retained for the lifetime of the installation; no purge or retention window is defined in this feature.
- Member self-service is limited to *viewing* status, role, rating, and standing history. Editing one's own display name or contact details is deferred; nothing in this feature lets a member change their own standing.
- Notifying members about standing changes (e.g. "your rating dropped") is out of scope here and belongs to the Notifications feature; this feature only publishes the event such a feature would consume.

## Dependencies

- Depends on and must remain consistent with the product specification: [specs/001-tool-library/spec.md](../001-tool-library/spec.md) (roles, rating model and 0–100 scale, configurable rules, authentication requirement).
- Depends on the foundation delivered by [specs/002-catalog-foundation/spec.md](../002-catalog-foundation/spec.md): the modular-monolith skeleton, the identity and permission model, the dedicated migration/seed step, and the reproducible environment. This feature adds its schema and seed data through that same migration step and follows the permission-boundary pattern documented in [002's contracts](../002-catalog-foundation/contracts/catalog-permissions.md).
- **Changes an existing 002 behaviour**: FR-006a replaces 002's interim rule that any authenticated user may browse the catalog. Catalog's own code is unaffected — the gate moves from "authenticated" to "enrolled and active" at the application entry point — but 002's permission contract document and its access tests must be updated to match when this feature ships.
- Blocks feature 004 (Lending), which consumes the member standing contract (FR-024), the community rules contract (FR-026), the outcome-reporting contract (FR-027), and the standing-changed event (FR-028); and the later Notifications feature, which consumes the same event.
