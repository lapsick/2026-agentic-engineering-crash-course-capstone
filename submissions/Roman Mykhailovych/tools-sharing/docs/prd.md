# ToolShare — Product Requirements Document

**Status**: Consolidated from delivered specs 001–008 · **Last updated**: 2026-09-28

This document consolidates the business logic and requirements scattered across
`specs/001-tool-library` … `specs/008-out-of-band-maintenance` into one product-level view. It does
not replace the specs: each requirement carries a trace tag such as `[004 FR-014]` pointing to the
authoritative source. Technical and architectural rules live in
`.specify/memory/constitution.md`; this document covers *what the product does and why*, and
mentions architecture only where it shapes behaviour a user can observe.

---

## 1. Product overview

### 1.1 Problem

A local community (an HOA, a garage cooperative) shares a pool of tools. Today the pool is tracked in
messengers and spreadsheets, which leads to three recurring pains:

1. **Nobody knows who has what** — "who has the rotary hammer right now?"
2. **Broken tools sit unrepaired for years** — damage is not recorded, so nothing triggers a repair.
3. **Tools are returned late** — there are no reminders and no consequences.

### 1.2 Solution

ToolShare is a web application in which a community's members browse a catalog of tools, reserve a
specific physical instance for a date range (or queue for it), have it checked out and returned by a
Librarian who records its condition, and are held to a reliability standard: late or damaged returns
lower a member's rating, and a low rating reduces how many tools they may hold at once. Damaged
instances automatically go to maintenance, members get in-app and email reminders, and the Librarian
has reports on popularity, overdue loans and maintenance spend.

### 1.3 Goals

| # | Goal | Measured by |
|---|---|---|
| G1 | Every instance has an unambiguous, current status and a visible holder | [001 SC-002] |
| G2 | No instance is ever double-booked or double-checked-out | [001 SC-003], [004 SC-001/003] |
| G3 | Damage always leads to a repair ticket; broken tools never circulate | [001 SC-004/005], [008 SC-002] |
| G4 | Fewer overdue loans via reminders and a rating-based self-regulation | [001 SC-006/007] |
| G5 | Full, immutable audit trail of loans, maintenance, state and standing changes | [001 SC-009] |
| G6 | The community abandons parallel messenger/Excel record-keeping within a month | [001 SC-010] |

### 1.4 Delivered feature map

| Feature | Scope | Module |
|---|---|---|
| 001 Tool library | Product specification (user stories 1–8, rules, glossary) | — |
| 002 Catalog foundation | Categories, tools, instances, photos, search; auth skeleton; bootstrap admin | Catalog |
| 003 Membership & rules | Roster, roles, enrolment gate, community rules, reliability rating | Membership |
| 004 Lending | Reservations, FIFO waitlist, checkout/return, maintenance, reminders, rating outcomes | Lending |
| 005 Notifications | In-app inbox and email for loan deadlines and standing changes | Notifications |
| 006 Librarian reports | Overdue, popularity, maintenance cost | Lending |
| 007 Real-time notifications | Live push of inbox and unread badge | Notifications |
| 008 Out-of-band maintenance | Librarian sends a damaged, not-on-loan instance to maintenance | Lending (+ Catalog contract) |

---

## 2. Scope

### 2.1 In scope

- One installation serves exactly **one** community [001 Assumptions].
- Web application only; authentication mandatory; **no public or anonymous access** [001 FR-036].
- Catalog management, membership administration, community rules, reservations/waitlist,
  Librarian-mediated checkout and return, maintenance, reminders, notifications, reports.

### 2.2 Out of scope (explicit)

| Item | Source |
|---|---|
| Multiple communities / multi-tenancy | 001, 002 Assumptions |
| Mobile app, public access, self-registration | 001, 003 Assumptions |
| Payments, deposits, charging members for damage; maintenance cost is an accounting value only | 001, 008 Assumptions |
| Member self-service checkout or return; identity verification at physical handover | 004 Assumptions |
| Walk-up checkout without a reservation | 004 Assumptions |
| Member-reported damage; photos on damage reports | 008 Input |
| SMS/messenger channels (only the extensibility seam exists) | 005 Assumptions |
| Notification preferences / opt-out | 005, 007 Assumptions |
| OS/native push; multi-instance real-time backplane | 007 Assumptions |
| Report export (CSV/PDF) or scheduled report delivery | 006 Assumptions |
| Email-based invitation / onboarding; member self-editing of profile details | 003 Assumptions |
| External identity providers | 002 Assumptions |

---

## 3. Users and roles

### 3.1 Roles

A member holds **exactly one** role. Roles are **hierarchical**: Administrator ⊇ Librarian ⊇ Member —
each level has every capability of the levels below [003 FR-004].

| Role | Who | Core responsibilities |
|---|---|---|
| **Member** | A community resident who borrows tools | Browse catalog, reserve, join waitlists, cancel own reservations, view own profile/rating/loans/notifications |
| **Librarian** | The person running the counter | Everything a Member can, plus: manage catalog, check out and accept returns, record condition, manage maintenance (incl. out-of-band reports), view any member's profile and loans, run reports |
| **Administrator** | Community organiser | Everything a Librarian can, plus: enrol/deactivate/reactivate members, assign roles, edit community rules, record manual rating adjustments |

### 3.2 The enrolment gate (applies to everyone)

- Authentication alone is **not** enough. Every application action — including catalog browsing — is
  refused unless the caller is an **enrolled member with Active status** [003 FR-006a].
- A user who can sign in but is not enrolled, or is Deactivated, sees only an explanatory page that
  directs them to an Administrator [003 FR-006a].
- The check runs **on every request**; the sign-in credential is never disabled. A member deactivated
  mid-session is refused on their very next action [003 FR-006].
- Capabilities are evaluated per action: a role change takes effect on the member's next action
  [003 Edge Cases].

### 3.3 Capability matrix

| Capability | Member | Librarian | Admin | Source |
|---|:-:|:-:|:-:|---|
| Browse/search catalog, view tool and instance details | ✓ | ✓ | ✓ | 002 FR-007 |
| See the current holder of a loaned instance | — | ✓ | ✓ | 001 FR-005 |
| Create/edit categories, tools, instances; manage photos; retire instance | — | ✓ | ✓ | 002 FR-012 |
| Reserve, join waitlist, confirm offer, cancel **own** reservation | ✓ | ✓ | ✓ | 004 FR-029 |
| View **own** reservations, loans, waitlist entries | ✓ | ✓ | ✓ | 004 FR-030 |
| View **any** member's reservations and loans | — | ✓ | ✓ | 004 FR-030 |
| Check out, record return and condition | — | ✓ | ✓ | 004 FR-029 |
| Close maintenance request | — | ✓ | ✓ | 004 FR-029 |
| Report out-of-band maintenance | — | ✓ | ✓ | 008 FR-020 |
| View **own** profile, rating, standing history | ✓ | ✓ | ✓ | 003 FR-022 |
| List/search members; view **any** member's profile and history | — | ✓ | ✓ | 003 FR-008, FR-022 |
| Enrol, deactivate, reactivate, change role | — | — | ✓ | 003 FR-001–FR-005 |
| Read community rules | ✓ | ✓ | ✓ | 003 FR-013 |
| Edit community rules | — | — | ✓ | 003 FR-013 |
| Record manual rating adjustment | — | — | ✓ | 003 FR-021 |
| View **own** notifications, mark read | ✓ | ✓ | ✓ | 005 FR-012–FR-014 |
| Run reports (overdue, popularity, maintenance cost) | — | ✓ | ✓ | 006 FR-012 |

Nobody can view another member's notifications [005 FR-014]. A member cannot view another member's
rating or standing history [003 FR-022].

---

## 4. Domain glossary

| Term | Meaning |
|---|---|
| **Category** | A classification of tools. A tool belongs to exactly one. |
| **Tool** (catalog entry) | Logical model ("Rotary Hammer") with a name and a category; groups instances. |
| **Tool instance** | One physical unit with a catalog-wide unique serial/inventory number, a condition, photos and a circulation state. Identical models are separate instances. |
| **Condition** | Fixed, ordered 4-level scale: **New → Good → Worn → Damaged** (New best). Not configurable. |
| **Circulation state** | In circulation · On loan · Under maintenance · Retired. |
| **Member** | An enrolled person: display name, email, sign-in identity, enrolment date, status, role, rating. Never hard-deleted. |
| **Membership status** | Active or Deactivated (with reason and moment of the latest change). |
| **Reliability rating** | Integer 0–100, starts at 100, clamped. |
| **Effective concurrent-loan limit** | Normal limit if rating ≥ low-rating threshold, otherwise the reduced limit. |
| **Community rules** | The single, installation-wide set of borrowing parameters (§5.3). |
| **Standing history** | One append-only stream per member of enrolment, status, role and rating entries. |
| **Reservation** | A member's claim on a specific instance for a date range. |
| **Waitlist entry** | A member's FIFO place in line for a specific instance, with offer state. |
| **Offer** | A time-boxed chance given to the head of the waitlist to confirm a reservation. |
| **Loan** | An instance checked out to a member against a reservation. |
| **Overdue** | A loan whose planned return date passed without a return. |
| **Maintenance request** | A repair ticket for one instance; origin is *return-triggered* or *out-of-band*. |
| **Notification** | A fact surfaced to exactly one member, delivered in-app and by email. |

---

## 5. Business requirements

### 5.1 Catalog [002]

**Management (Librarian/Admin)**

- **CAT-1** Create, edit and list categories. A category **cannot be deleted while tools are assigned**
  to it — tools are never orphaned [002 FR-001].
- **CAT-2** Create and edit tools, each with a name and exactly one category [002 FR-002].
- **CAT-3** Register instances under a tool with a serial/inventory number **unique across the whole
  catalog** (case-insensitive); duplicates are rejected with a clear message [002 FR-003, SC-004].
- **CAT-4** Each instance has a condition from the fixed 4-level scale only [002 FR-004].
- **CAT-5** Attach one or more photos to an instance. Defaults: JPEG/PNG/WebP, ≤ 5 MB each, ≤ 5 per
  instance (configurable). Oversize/unsupported files are rejected with a clear message
  [002 FR-005, FR-009].
- **CAT-6** Retire an instance with a stated reason (write-off, sale). Retired instances are excluded
  from normal browsing by default, cannot be reserved or checked out, and **their record and full
  history remain retrievable** [001 FR-027/028, 002 FR-006].
- **CAT-7** Concurrent edits of the same record are detected; the second save gets a conflict, never a
  silent overwrite [002 FR-010].

**Browsing (any active member)**

- **CAT-8** Search tools by partial name — **case-insensitive and accent-tolerant**, including
  Ukrainian names — and filter by category [002 FR-007, SC-008].
- **CAT-9** Tool detail shows each instance with serial number, condition, photos and current status
  (free / reserved until a date / on loan / under maintenance / retired) [001 FR-004/005].
- **CAT-10** The current **holder** of a loaned instance is visible to Librarians/Admins only; hidden
  from ordinary members for privacy [001 FR-005, Assumptions].
- **CAT-11** An empty search/filter result shows a clear empty state, distinct from an error
  [002 FR-008].
- **CAT-12** Lending-driven states (on loan, under maintenance) appear in the same catalog
  availability view members already use [004 FR-027].

### 5.2 Membership and roster [003]

- **MEM-1** An Administrator enrols a person in **one action** that captures display name, email and an
  initial password and creates both the sign-in account and the member record. No invitation email
  [003 FR-001].
- **MEM-2** The member must **change the initial password on first sign-in** before anything else
  [003 FR-001a].
- **MEM-3** A person is enrolled at most once; an email or sign-in name already in use is rejected with
  a message naming the conflict [003 FR-002].
- **MEM-4** New members get: role **Member**, status **Active**, an enrolment date, rating **100**
  [003 FR-003].
- **MEM-5** The Administrator changes a member's single role [003 FR-004].
- **MEM-6** The Administrator deactivates a member with a stated reason and can reactivate them;
  **reactivation restores the previous rating unchanged** [003 FR-005].
- **MEM-7** Deactivation blocks all future activity immediately but **does not cancel or recall** loans
  already held; those must still be returned [003 Edge Cases].
- **MEM-8** The **last active Administrator** can be neither deactivated nor demoted [003 FR-007].
- **MEM-9** Librarians/Admins list and search members by name, email, status and role [003 FR-008].
- **MEM-10** Member records are **never hard-deleted**; ending membership is deactivation [003 FR-009].
- **MEM-11** A fresh installation has a bootstrap administrator who is also an enrolled member
  [002 FR-013, 003 FR-010].
- **MEM-12** A member's own profile shows status, role, current rating and the chronological standing
  history; a new member sees rating 100 and an empty-state for rating movement. A member cannot change
  their own rating, status or role [003 US3].
- **MEM-13** Concurrent edits of the same member record: second writer gets a conflict [003 FR-031].

### 5.3 Community rules [003]

Exactly one authoritative rule set per installation, editable by Administrators only, readable by
every active member [003 FR-011, FR-013].

| Rule | Default | Validation on save |
|---|---|---|
| Maximum loan term | **14 days** | > 0 |
| Normal concurrent-loan limit | **3** | > 0 |
| Low-rating threshold | **50** | 0–100 |
| Reduced concurrent-loan limit (below threshold) | **1** | > 0, ≤ normal limit |
| Overdue penalty | **10 points** | ≥ 0 |
| Damage penalty | **20 points** | ≥ 0 |
| Clean-return reward | **2 points** | ≥ 0 |
| Waitlist offer window | **24 hours** | > 0 |
| Return-reminder lead time | **2 days** | > 0 |

- **RULE-1** Every rule has a default on a fresh install; the system is usable without editing any
  [003 FR-012].
- **RULE-2** Invalid values are rejected with a message **naming the offending rule** [003 FR-014].
- **RULE-3** Each change records when and by whom [003 FR-015].
- **RULE-4** Changes apply **only to decisions made afterwards**, never retroactively. Lowering a limit
  below what a member already holds invalidates nothing; only their next attempt is evaluated against
  the new value [003 FR-015, 004 Edge Cases].
- **RULE-5** Two Administrators saving simultaneously: the second is rejected as a conflict and told to
  reload and reapply [003 FR-031].

### 5.4 Reliability rating [001 FR-024, 003]

- **RAT-1** Integer 0–100, starts at 100, always **clamped** to that range [003 FR-016].
- **RAT-2** Outcome types and their effect [003 FR-018]:

  | Outcome | Effect | Reported by |
  |---|---|---|
  | Overdue return | − overdue penalty | Lending, when a late loan closes |
  | Return in worsened condition | − damage penalty | Lending, when a damaged loan closes |
  | Clean on-time return | + clean-return reward | Lending, when a loan closes on time and undamaged |
  | Manual adjustment | ± stated value, **reason mandatory**, Admin only | Administrator |

- **RAT-3** A loan that closes **both late and damaged** yields **both** an overdue and a damage
  outcome; the clean-return reward applies only when **neither** holds [004 FR-020].
- **RAT-4** **Idempotency**: an outcome is applied at most once per (originating occurrence, outcome
  type). The loan's identifier is the occurrence; a repeated report is recorded as already handled
  [003 FR-019, 004 FR-021].
- **RAT-5** Each rating entry records outcome type, points applied **after clamping**, resulting score
  and originating occurrence — at 100, a clean return records 0 effective points [003 FR-017, US4].
- **RAT-6** Effective concurrent-loan limit = normal limit if rating ≥ threshold, else reduced limit
  [003 FR-023].
- **RAT-7** Crossing the low-rating threshold in either direction is announced as a standing change
  [003 US4-7, FR-028].
- **RAT-8** Concurrent outcome reports for one member are serialized internally: every outcome applied
  exactly once, no lost updates, caller never sees a contention error [003 FR-032].
- **RAT-9** A report for an identity that is not an enrolled member is rejected, never auto-creating a
  member [003 FR-027, Edge Cases].
- **RAT-10** A member can explain every point of difference from 100 from their own profile
  [003 SC-012].
- **RAT-11** Rating logic lives only in Membership; Lending reports facts and never computes a rating
  [004 FR-028].
- **RAT-12** Out-of-band maintenance **never** affects anyone's rating [008 FR-013].

### 5.5 Reservations and waitlist [004]

**Creating a reservation** — a member reserves a *specific instance* for a date range. The attempt is
refused if any of these holds (checked in the member's context at the moment of the attempt):

| # | Refused when | Source |
|---|---|---|
| RES-1 | Caller is not an enrolled, Active member (per Membership) | 004 FR-019 |
| RES-2 | Range longer than the maximum loan term, or end before start | 004 FR-001, US1-3 |
| RES-3 | Instance is under maintenance or retired | 004 FR-006 |
| RES-4 | Member currently holds **any overdue loan** (until returned) | 004 FR-007 |
| RES-5 | Active reservations + open loans would exceed the member's effective limit (the message says whether the reduced limit applies) | 004 FR-008, US1-4 |
| RES-6 | Range overlaps an existing active reservation or loan on that instance → the member is **offered the waitlist instead** of a dead end | 004 FR-002, FR-003 |

- **RES-7** Under concurrency exactly one of two overlapping attempts succeeds; the other is refused as
  no-longer-available and offered the waitlist. Double-booking is impossible (database-enforced)
  [004 FR-009, SC-001].
- **RES-8** A member may **cancel their own** active reservation at any time **before checkout**. After
  checkout only an early return is possible [004 FR-004].
- **RES-9** A new reservation is visible as "taken for that range" to every other member within the
  same interaction [004 SC-001].

**Waitlist (per instance, FIFO)**

- **WL-1** Order is strictly by join time; ties are impossible [001 FR-014].
- **WL-2** A member cannot hold two unresolved entries for the same instance. Joining is refused when
  the instance is currently available (there is nothing to wait for) [004 data-model WL-02].
- **WL-3** When an instance **frees up** (cancellation, early return, clean return, maintenance
  closed), the earliest waiting member receives a **time-boxed offer** lasting the configured offer
  window [001 FR-020a, 004 FR-005].
- **WL-4** Confirming within the window creates the reservation. If the window lapses, the offer
  **rolls to the next member** in join order, repeating until confirmed or the list is exhausted
  [004 FR-005].
- **WL-5** If the waitlist is exhausted, the instance simply stays free and reservable by anyone
  [004 Edge Cases].
- **WL-6** An unconfirmed offer **cannot be confirmed while the instance is under maintenance**
  [008 FR-010].
- **WL-7** When an instance goes to maintenance, every waiting member **keeps their place**; the queue
  is re-offered when maintenance closes [004 Edge Cases, 008 FR-010].
- **WL-8** Voluntarily leaving a waitlist is **not supported** (gap, §9).
- **WL-9** A plain member sees that a waitlist exists and their own position, not other members'
  identities [004 contracts].

### 5.6 Checkout and return [004]

- **LOAN-1** Only a Librarian/Admin checks out, and **only against an existing, not-yet-checked-out
  reservation** — no walk-up checkout [004 FR-010, Assumptions].
- **LOAN-2** Checkout re-verifies member standing (enrolled, active, not overdue, within limit) and
  instance availability; an instance still on loan (even overdue), under maintenance or retired cannot
  be checked out [004 FR-013, FR-019, SC-007].
- **LOAN-3** Checkout records the moment, marks the instance **On loan**, turns the reservation into a
  loan (1:1, one-way), and snapshots the **condition at checkout** [004 FR-010].
- **LOAN-4** Return is recorded by a Librarian/Admin — **early, on time or late** — with the moment and
  the returned condition on the 4-level scale [004 FR-011].
- **LOAN-5** **Any** return closes the loan [004 FR-012]. Then:
  - Condition **same or better** than at checkout → instance back **In circulation** and the waitlist is
    offered (WL-3). A better condition is treated exactly as "no worse" [004 Edge Cases].
  - Condition **worse** → a maintenance request opens automatically and the instance goes straight to
    **Under maintenance** (§5.7).
- **LOAN-6** On closing, reliability outcomes are reported to Membership per RAT-3/RAT-4
  [004 FR-020/021].
- **LOAN-7** Checkout and return moments and both condition values remain part of the instance's
  permanent history [004 US2-6].

### 5.7 Maintenance [004, 008]

A maintenance request has **exactly one origin**:

| Origin | Created by | Carries | Source |
|---|---|---|---|
| **Return-triggered** | Automatically, on a return with worsened condition | The triggering loan | 004 FR-014 |
| **Out-of-band** | A Librarian/Admin, from the instance's management page | Reporter, moment, reason, observed condition | 008 FR-001, FR-015 |

Requests existing before 008 are all return-triggered and keep their loan link, dates and cost
[008 FR-016].

**Common rules**

- **MNT-1** At most **one open request per instance**, regardless of origin (database-enforced)
  [004 FR-015, 008 FR-007].
- **MNT-2** While open, the instance is **Under maintenance**: not reservable, not checkable-out, shown
  as such in every availability view [004 FR-014, 008 FR-007].
- **MNT-3** A Librarian/Admin **closes** a request with a **mandatory cost**: zero is valid, blank is
  not [004 FR-016, 008 FR-012].
- **MNT-4** Closing returns the instance to **In circulation in its current (possibly worsened)
  condition** — repair does not upgrade condition — and triggers the waitlist offer [008 Assumptions].
- **MNT-5** A closed request's cost and open/close moments remain in the instance's history forever
  [004 FR-017].
- **MNT-6** The open maintenance queue shows each request's origin; for out-of-band requests also the
  reason and observed condition [008 FR-017].

**Return-triggered specifics**

- **MNT-7** Opening cancels, with a reason, every active reservation on the instance whose **start date
  is still in the future**. The holder sees the cancellation and reason in their reservations; **no
  notification is sent** [004 data-model MAINT-03/RES-08, 008 Clarifications].

**Out-of-band specifics [008]**

- **OOB-1** Allowed only for an instance **in circulation, not on loan, with no open request**
  [008 FR-001].
- **OOB-2** Reason is required: trimmed, non-empty, ≤ 500 characters [008 FR-002].
- **OOB-3** Observed condition must be **equal to or worse than** the current one [008 FR-003].
- **OOB-4** Cause-specific refusals [008 FR-004, US3]:

  | Instance state | Message intent |
  |---|---|
  | On loan | Damage on a loaned instance is recorded at its return |
  | Retired | Instance is retired |
  | Open request exists (either origin) | A maintenance request is already open |
  | Observed condition better than current | Observed condition cannot be better than current |
  | Missing/whitespace reason | A reason is required |

- **OOB-5** A refused report leaves **no trace**: no request, condition change, circulation change or
  cancellation [008 FR-005].
- **OOB-6** Worse observed condition → becomes the instance's current condition and a new condition
  history entry is appended. Equal condition (e.g. a frayed cord that doesn't change the grade) →
  condition and history untouched [008 FR-011].
- **OOB-7** Cancels **every not-yet-checked-out reservation** on the instance — **including one whose
  range has already begun** but was not collected — with moment and reason; **no notification**
  [008 FR-008, FR-009]. (Deliberately stricter than MNT-7, which is unchanged [008 FR-022].)
- **OOB-8** Attributes damage to no member; reports nothing to Membership [008 FR-013].
- **OOB-9** A mistaken report is not edited: it is closed with cost 0, and stays in history
  [008 Edge Cases].
- **OOB-10** The reporter is recorded as of the time of the report, even if they later lose the role or
  are deactivated [008 Edge Cases].

### 5.8 Reminders and overdue tracking [004]

- **REM-1** When the configured **reminder lead time** before a loan's planned return date is reached,
  a return reminder is generated for the holder — **once per loan** [004 FR-022].
- **REM-2** When the planned return date passes without a return, the loan is **marked overdue** and an
  overdue notice is generated — once per loan [004 FR-023].
- **REM-3** The overdue marking is **permanent**: it stays on the loan after it is returned
  [004 FR-025].
- **REM-4** Librarians can distinguish overdue loans from loans still within term [004 FR-024].
- **REM-5** An overdue loan blocks the member's new reservations and checkouts (RES-4, LOAN-2).
- **REM-6** Reminders, overdue marking and waitlist-offer expiry are driven by periodic background
  processing, so they happen without anyone being present [004 research R4/R5].

### 5.9 Notifications [005, 007]

**Triggers** — a notification is generated for exactly one member when:

| Trigger | Source event | Source |
|---|---|---|
| Return reminder for a loan they hold | Lending | 005 FR-001 |
| Overdue notice for a loan they hold | Lending | 005 FR-002 |
| Their membership deactivated or reactivated | Membership | 005 FR-003 |
| Their role changed | Membership | 005 FR-003 |
| Their rating **crossed** the low-rating threshold (either direction) — ordinary point changes do **not** notify | Membership | 005 FR-004 |

Not notified (by decision): reservation cancelled because the instance went to maintenance
[008 FR-009]; waitlist offers are not in 005's trigger list (see §9).

**Rules**

- **NTF-1** At most **one notification per (originating occurrence, kind)** even if the event is
  observed more than once [005 FR-005].
- **NTF-2** Content (which tool, which loan, which standing fact) is resolved via the owning module's
  published read contracts at generation time [005 FR-006].
- **NTF-3** Channels: **in-app** always; **email** when the member has an address on file. No email
  address is not a failure [005 FR-007–FR-009].
- **NTF-4** One channel's failure never blocks or delays the other [005 FR-010].
- **NTF-5** New channels (SMS, messenger) must be addable without touching generation logic
  [005 FR-011, 001 FR-023].
- **NTF-6** Inbox: own notifications only, newest first, unread/read distinction, unread count, mark one
  or all as read [005 FR-012–FR-014].
- **NTF-7** Every notification and every per-channel delivery attempt (whether, when, success) is kept
  permanently and never edited; a retry is a new record [005 FR-015/016].
- **NTF-8** Notifications survive the member's later deactivation [005 Edge Cases].
- **NTF-9** The member's email is the one held by Membership; Notifications stores no email of its own
  [005 Assumptions].

**Real-time delivery [007]**

- **RT-1** For a member with the app open, a new notification appears in the inbox and the unread badge
  increases **within 3 seconds** (≥ 99% of cases), with no reload [007 FR-001, SC-001].
- **RT-2** The unread badge is live **on every page**, not just the inbox, and settles to the
  authoritative count within a 3-second window; bursts may be coalesced [007 FR-003, SC-002].
- **RT-3** Updates reach only the recipient's sessions — never another member's [007 FR-002].
- **RT-4** All of a member's open sessions (tabs) converge to the same count, including after
  mark-as-read in one of them [007 FR-006, FR-008].
- **RT-5** After a dropped connection, the view reconciles against the persisted inbox — nothing missed,
  nothing duplicated [007 FR-005].
- **RT-6** Real-time is **additive and best-effort**: the persisted inbox is the source of truth; if the
  live path fails, generation, persistence and email are unaffected and the member sees everything on
  next load [007 FR-004, FR-009].
- **RT-7** Applies to the in-app channel only; creates/alters no notification or delivery record
  [007 FR-007, FR-010].

### 5.10 Librarian reports [006, 008]

Read-only, Librarian/Admin only, computed live at the moment of viewing, each produced from a single
view in under a minute, each with an explicit empty state [006 FR-010–FR-013].

| Report | Content | Rules |
|---|---|---|
| **Overdue loans** | Member, tool, instance, checkout date, planned return date, days overdue | Currently overdue & unreturned only; **most overdue first**; still shown if the member was later deactivated [006 FR-004–FR-006] |
| **Most-borrowed tools** | Tools ranked by number of loans, most first | All-time by default; optional range counts loans **checked out** within it; retired tools keep their historical counts; never-borrowed tools appear with 0 or are omitted [006 FR-001–FR-003] |
| **Maintenance cost** | Total cost for a date range | Only **closed** requests, attributed by **closure date**; open requests contribute nothing; both origins included. Itemized list (origin, instance, closure date, cost) plus **per-origin subtotals** that add up to the total [006 FR-007/008, 008 FR-018, SC-005] |

- **RPT-1** Date ranges are inclusive at both ends; a start after the end is rejected with a message
  [006 FR-009, Assumptions].
- **RPT-2** Costs are never merged or deduplicated by instance [006 Edge Cases].
- **RPT-3** If a referenced tool can't be resolved, the row is shown as unavailable rather than silently
  dropped [006 Edge Cases].

### 5.11 History and audit

- **AUD-1** Loans, returns, maintenance records, reservations, waitlist entries, instance condition and
  circulation changes, member standing changes and notifications are **append-only**: existing facts
  are never edited or deleted; corrections are new entries [001 FR-029, 003 FR-020, 004 FR-026,
  005 FR-016, 008 FR-019].
- **AUD-2** Each member standing transition yields **exactly one** history entry and **exactly one**
  published event — never one without the other [003 FR-017a].
- **AUD-3** History of retired instances and deactivated members remains fully viewable
  [001 SC-009, 003 SC-010].
- **AUD-4** Retention is unbounded; no purge policy [003, 005 Assumptions].

### 5.12 Concurrency guarantees (user-observable)

| Scenario | Guaranteed outcome | Source |
|---|---|---|
| Two members reserve the last free slot | Exactly one succeeds; the other is offered the waitlist | 004 FR-009 |
| Two Librarians edit the same catalog record / member / rules | Second save rejected as a conflict | 002 FR-010, 003 FR-031 |
| Concurrent rating outcomes for one member | All applied exactly once, no error to caller | 003 FR-032 |
| Out-of-band report vs. checkout of the same instance | Exactly one succeeds; never both on loan and under maintenance | 008 FR-014 |
| Two out-of-band reports on one instance | Exactly one accepted; the other refused (request already open) | 008 FR-014 |
| Out-of-band report vs. new reservation | Reservation either refused or created then cancelled; none survives | 008 FR-014 |

---

## 6. Lifecycles

### 6.1 Tool instance circulation

```text
                 checkout                       clean return
  In circulation ────────► On loan ──────────────────────────► In circulation
       │   ▲                  │
       │   │                  │ worsened return (auto request)
       │   │ close request    ▼
       │   └──────────── Under maintenance ◄── out-of-band report (not on loan)
       │
       └── retire (not on loan) ──► Retired   (terminal; history preserved)
```

### 6.2 Reservation

```text
  Active ──checkout──► CheckedOut (became a loan; terminal)
     │
     ├──member cancels (before checkout)──────────► Cancelled
     └──instance sent to maintenance (with reason)─► Cancelled
```

### 6.3 Waitlist entry

```text
  Waiting ──instance freed & first in line──► Offered ──confirmed in window──► Confirmed (reservation created)
                                                 │
                                                 └──window lapsed──► Expired (offer rolls to next Waiting)
```

### 6.4 Loan

```text
  Open ──planned date passes──► Open + Overdue
   │                                │
   └──────────return────────────────┴──► Closed  (overdue flag kept; outcomes reported to Membership)
```

### 6.5 Maintenance request

```text
  Open ──close with cost (≥ 0)──► Closed   (origin: ReturnTriggered | OutOfBand, fixed at creation)
```

### 6.6 Member

```text
  (enrol) ──► Active ◄──reactivate── Deactivated
                 └────deactivate (reason)───┘
  Never deleted. Last active Administrator cannot be deactivated or demoted.
```

---

## 7. Module responsibilities and integration (business view)

| Module | Owns | Publishes to others | Consumes |
|---|---|---|---|
| **Catalog** | Categories, tools, instances, condition, circulation state, photos | Instance lookup (identity, condition, availability); state-reporting contract for Lending (on loan, returned, under maintenance, maintenance closed, observed condition); instance state-changed event | — |
| **Membership** | Members, roles, status, community rules, rating & standing history | Standing lookup (enrolled? active? rating, effective limit); rules lookup; outcome-reporting contract; standing-changed event | — |
| **Lending** | Reservations, waitlist, loans, maintenance requests, reports | Loan-deadline event (reminder / overdue) | Catalog lookup + state reporting; Membership standing, rules, outcome reporting |
| **Notifications** | Notifications, per-channel delivery records | — | Lending deadline event; Membership standing event; Catalog/Membership lookups |

Business consequences of the boundaries:

- Lending never keeps its own copy of a member's rating or status; every eligibility decision asks
  Membership live [004 FR-018, FR-028].
- Lending never writes Catalog data directly; it asks Catalog to change an instance's state
  [004 Assumptions, 008 Assumptions].
- Membership knows nothing about loans; overdue blocking is Lending's decision [003 Assumptions].

---

## 8. Success criteria (consolidated)

| Area | Criterion | Source |
|---|---|---|
| Findability | Find a tool and its availability in ≤ 30 s | 001 SC-001, 002 SC-002 |
| Catalog ops | Register a tool with an instance and photo in < 3 min | 002 SC-001 |
| Roster ops | Enrol a member into a working state in < 2 min | 003 SC-001 |
| Operations | Out-of-band report in < 1 min | 008 SC-001 |
| Reports | Any report in ≤ 1 min, no manual consolidation | 001 SC-008, 006 SC-001 |
| Integrity | 0 double-bookings / double-checkouts | 001 SC-003, 004 SC-001/003 |
| Maintenance | 100% worsened returns create a request; 0 reservations survive an OOB report | 004 SC-004, 008 SC-002 |
| Rating | Displayed rating always = clamped running total; 0 double-applied outcomes | 003 SC-008/009 |
| Access | 0 unauthorized successes across all permission boundaries | 002 SC-003, 003 SC-003/005, 006 SC-005 |
| Notifications | Exactly one notification per event (0 missed, 0 duplicated); live within 3 s | 005 SC-001, 007 SC-001 |
| Business outcome | Overdue share −50%; broken-and-idle tools −70% in 3 months; spreadsheets abandoned in 1 month | 001 SC-005/006/010 |
| Operability | Clean checkout → running app in < 15 min; migrations idempotent | 002 SC-005/006 |

---

## 9. Known gaps and open questions

Items the specs explicitly leave open or where they diverge — candidates for future features.

| # | Gap | Where it comes from |
|---|---|---|
| 1 | **Members are not notified when a reservation is cancelled due to maintenance** (either origin). 004's edge case promised a notice; the delivered system and 008's clarification say no. | 004 Edge Cases vs. 008 Clarifications/FR-009 |
| 2 | **Waitlist offers are not a notification trigger** in 005, although 001 FR-020a says the member "is notified" of an offer. Members must discover offers in the app. | 001 FR-020a vs. 005 FR-001–FR-004 |
| 3 | Leaving a waitlist voluntarily is not supported. | 004 data-model WL-06 |
| 4 | When a return is due but someone is on the waitlist, is the next person told about a delay? Not specified. | 001 Edge Cases |
| 5 | Repair does not upgrade an instance's condition; a repaired "Damaged" tool re-enters circulation as "Damaged". | 008 Assumptions |
| 6 | Retiring an instance that has active future reservations — expected handling is not specified. | 001 Edge Cases |
| 7 | Deactivated members' outstanding loans are not surfaced anywhere specific; Membership only announces the change. | 003 Edge Cases |
| 8 | Member self-editing of display name/contact details is deferred. | 003 Assumptions |
| 9 | Notification preferences/opt-out, SMS/messenger channels, report export — future work. | 005, 006, 007 Assumptions |
| 10 | Real-time delivery assumes a single application instance; scaling out needs a backplane. | 007 Assumptions |

---

## 10. Non-functional constraints (summary)

Binding details live in `.specify/memory/constitution.md`; listed here only for product context.

- Stack is fixed: .NET 10, ABP Framework (free/OSS), Blazor Web App (InteractiveServer), PostgreSQL 16
  via EF Core.
- Modular monolith; each module has its own database schema; modules interact only via published
  contracts and integration events.
- Schema changes and seed data are applied only by a dedicated migration step, which is idempotent; the
  running app never mutates its schema [002 FR-015/016].
- If the database is unreachable at startup, the app fails clearly instead of serving empty pages
  [002 FR-020].
- The whole system runs reproducibly via `docker compose` (app + database) [002 FR-014].
- Test-first: domain rules have unit tests; application behaviour is tested against a real PostgreSQL.
