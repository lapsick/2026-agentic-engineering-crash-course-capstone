# Feature Specification: Librarian Reports

**Feature Branch**: `006-librarian-reports`

**Created**: 2026-08-05

**Status**: Draft

**Input**: User description: "next feature" — resolved to the next feature in the roadmap chain
recorded by [specs/004-lending/spec.md](../004-lending/spec.md) and reaffirmed by
[specs/005-notifications/spec.md](../005-notifications/spec.md): **Librarian Reports** (most-popular
tools, current overdue loans, and maintenance cost over a period — product spec User Story 7), the
one User Story from [specs/001-tool-library/spec.md](../001-tool-library/spec.md) both 004 and 005
explicitly deferred as "read-only projections over the history [Lending] already commits to keeping
immutable."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Overdue Loans at a Glance (Priority: P1)

The librarian opens a report and immediately sees every loan that is currently overdue: which
member holds it, which tool and instance, when it was checked out, when it was due back, and how
long it has been overdue.

**Why this priority**: This is the most operationally urgent report — it is what the librarian
checks to decide who to follow up with today. Unlike the other two reports, it reflects a live
problem rather than historical accumulation, so it delivers value from the very first overdue loan
and does not depend on much accumulated history existing yet.

**Independent Test**: Create a loan whose planned return date has passed without a return being
recorded, open the overdue report, and confirm that loan appears with the member, tool/instance,
checkout date, planned return date, and days overdue; confirm a loan that has since been returned no
longer appears.

**Acceptance Scenarios**:

1. **Given** a loan whose planned return date has passed and which has not been returned, **When**
   the librarian opens the overdue report, **Then** the loan appears with the member, the tool and
   instance, the checkout date, the planned return date, and the number of days overdue.
2. **Given** an overdue loan is subsequently returned, **When** the librarian reopens the overdue
   report, **Then** that loan no longer appears.
3. **Given** no loan is currently overdue, **When** the librarian opens the overdue report, **Then**
   the report shows that there are no overdue loans rather than an error or a blank screen.
4. **Given** several loans are overdue by different amounts, **When** the librarian opens the report,
   **Then** the loans are ordered with the most overdue first.

---

### User Story 2 - Most-Borrowed Tools (Priority: P2)

The librarian opens a report and sees which tools have been borrowed most often, ranked by number of
loans, optionally narrowed to a selected date range.

**Why this priority**: Popularity data drives decisions about which tools to buy more of and which
underused ones to retire, but it is a planning input rather than a daily operational need, so it
ranks below the overdue report.

**Independent Test**: Accumulate loan history across several tools with different loan counts, open
the popularity report, and confirm tools are ranked by number of loans, most-borrowed first; narrow
to a date range that excludes some of the loans and confirm the ranking updates accordingly.

**Acceptance Scenarios**:

1. **Given** accumulated loan history across multiple tools, **When** the librarian opens the
   popularity report with no date range set, **Then** tools are listed ordered by their all-time
   number of loans, most-borrowed first.
2. **Given** the same history, **When** the librarian sets a date range, **Then** only loans checked
   out within that range count toward each tool's total, and the ranking reflects that narrower
   count.
3. **Given** two tools have been retired but were borrowed before retirement, **When** the librarian
   opens the popularity report, **Then** their historical loan counts still appear (retirement does
   not erase history).
4. **Given** a tool has never been borrowed, **When** the librarian opens the popularity report,
   **Then** that tool either appears with a count of zero or is omitted, but the report as a whole is
   not blocked by tools with no loans.

---

### User Story 3 - Maintenance Cost Over a Period (Priority: P3)

The librarian selects a date range and sees the total cost of maintenance work closed within that
period, so they can track how much the community is spending on upkeep.

**Why this priority**: Maintenance spend is a budgeting input reviewed periodically (e.g. monthly or
quarterly), not something the librarian needs day to day, and it depends on maintenance requests
having actually been closed with a recorded cost — the least immediately available of the three
reports.

**Independent Test**: Close several maintenance requests with recorded costs on different dates, open
the maintenance cost report for a date range that includes some but not all of them, and confirm the
total reflects only the requests closed inside that range.

**Acceptance Scenarios**:

1. **Given** maintenance requests were closed with recorded costs on various dates, **When** the
   librarian selects a date range covering some of them, **Then** the report shows the sum of the
   costs of only the requests closed within that range.
2. **Given** a maintenance request is still open (not yet closed), **When** the librarian runs the
   report for any date range, **Then** that request's cost does not contribute to the total, since no
   final cost has been recorded for it yet.
3. **Given** no maintenance request was closed within the selected date range, **When** the librarian
   runs the report, **Then** the total shown is zero rather than an error or a blank screen.
4. **Given** the librarian changes the date range, **When** they rerun the report, **Then** the total
   updates to match the new range without requiring any other action.

---

### Edge Cases

- What happens when the librarian selects a date range with the start date after the end date? The
  report MUST reject the range with a clear message rather than silently returning an empty or
  incorrect result.
- What happens when a tool referenced by historical loan or maintenance data has since been deleted
  from the catalog entirely (as opposed to merely retired)? Catalog never hard-deletes a tool once it
  has history (see 002/004 append-only guarantees), so this case does not arise; if a lookup ever
  fails, the report shows the affected row as unavailable rather than omitting it silently.
- What happens when a member referenced by an overdue loan is later deactivated? The overdue report
  still shows the loan, since the loan and the obligation to return the tool do not disappear when
  membership status changes.
- How does the overdue report handle a loan that became overdue and was then reported to Membership as
  an outcome (004's rating-affecting report)? The two are independent: the overdue report reflects the
  loan's current due status regardless of whether or when Membership was notified.
- What happens when two maintenance requests for the same instance are closed on the same day with
  different costs? Both contribute their own recorded cost to the total; costs are never merged or
  deduplicated by instance.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a report listing tools ordered by their number of loans, most-
  borrowed first (product spec FR-030).
- **FR-002**: The popularity report MUST support an optional date range filter that restricts the
  loan count to loans checked out within the selected range; when no range is set, the count MUST
  reflect all-time history.
- **FR-003**: The popularity report MUST include tools that have since been retired, using their
  preserved historical loan counts, consistent with the append-only history guarantee (product spec
  FR-029).
- **FR-004**: The system MUST provide a report listing every loan that is currently overdue (product
  spec FR-031), showing the member, the tool and instance, the checkout date, the planned return
  date, and the number of days overdue.
- **FR-005**: The overdue report MUST reflect the current moment: a loan that has since been returned
  MUST NOT appear, and the report MUST be able to show zero results when nothing is overdue.
- **FR-006**: The overdue report MUST order results with the most-overdue loan first.
- **FR-007**: The system MUST provide a report showing the total maintenance cost for maintenance
  requests closed within a selected date range (product spec FR-032).
- **FR-008**: The maintenance cost report MUST count a maintenance request's cost only if the request
  has been closed (a cost is recorded), and MUST attribute it to the period using its closure date;
  open maintenance requests MUST NOT contribute to any period's total.
- **FR-009**: The system MUST reject a selected date range whose start is after its end, for any
  report that accepts a date range, with a message explaining the problem.
- **FR-010**: All three reports MUST be usable with no manual consolidation of data from other
  screens — each report MUST be produced from a single request/view (product spec SC-008).
- **FR-011**: Report data MUST be read-only: producing a report MUST NOT create, modify, or delete any
  loan, maintenance request, or other record.
- **FR-012**: Access to all three reports MUST be restricted to the Librarian and Administrator roles
  (product spec FR-033); a Member without one of those roles MUST be refused.
- **FR-013**: Each report MUST be able to represent an empty result (no overdue loans, no loans in a
  popularity range, zero maintenance cost in a period) distinctly from an error condition.

### Key Entities *(include if feature involves data)*

- **Popularity Report Entry**: a read-only projection pairing a tool (catalog entry) with its loan
  count for the selected date range (or all time), used only for ranking and display.
- **Overdue Loan Entry**: a read-only projection of a currently-overdue loan showing the member, the
  tool and instance, the checkout date, the planned return date, and the computed days-overdue value.
- **Maintenance Cost Summary**: a read-only projection of the total maintenance cost recorded for
  requests closed within a selected date range, together with the range itself.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The librarian produces any of the three reports (popularity, overdue, maintenance cost)
  in under 1 minute from opening the report, with no manual data consolidation from other screens
  (product spec SC-008).
- **SC-002**: 100% of loans whose planned return date has passed and which have not been returned
  appear in the overdue report at the time it is generated.
- **SC-003**: 100% of closed maintenance requests within a selected date range are reflected in that
  period's maintenance cost total, and 0% of open (uncosted) maintenance requests contribute to any
  total.
- **SC-004**: A tool's historical popularity ranking is unaffected by that tool's instances being
  later retired — 100% of pre-retirement loans still count.
- **SC-005**: A user without the Librarian or Administrator role is refused access to every report in
  100% of attempts.

## Assumptions

- **Reports are viewed on-screen only in this feature.** Exporting to CSV/PDF or scheduling recurring
  report delivery is a plausible future enhancement, not required by the product spec's US7/FR-030–
  FR-032, and is out of scope here.
- **No new persisted domain data is introduced.** All three reports are read-only projections over
  history that Catalog (tool identity, popularity) and Lending (loans, overdue status, maintenance
  requests and their costs) already commit to keeping immutable (per 002 and 004); this feature adds
  no new append-only record of its own.
- **The popularity report's default (no date range set) is all-time**, matching product spec
  Acceptance Scenario 1 for US7, which shows the ranking from "accumulated loan history" without
  mentioning a period; the date-range narrowing described in Acceptance Scenario 3 is additive on top
  of that default.
- **A maintenance request's cost is attributed to the report period by its closure date**, since a
  cost is recorded only when the request closes (product spec FR-018) and has no other date to
  attribute it by.
- **Access is limited to Librarian and Administrator**, consistent with product spec User Story 7
  ("The librarian views reports") and the role hierarchy already established (Administrator ⊇
  Librarian ⊇ Member, per 001's Assumptions).
- **Report data reflects the moment the report is generated** (no caching or scheduled refresh); the
  overdue report in particular is always computed against the current date/time.
- **Date ranges are inclusive of both the start and end date**, matching ordinary user expectations
  for a "period" selector.

## Dependencies

- Depends on [specs/002-catalog-foundation/spec.md](../002-catalog-foundation/spec.md)'s public tool
  lookup to resolve a loan's tool/instance identity into displayable report content, exactly as
  Lending itself already does.
- Depends on [specs/004-lending/spec.md](../004-lending/spec.md)'s immutable, append-only record of
  every reservation, loan, and maintenance request and every change to their state (FR-026) — this
  feature is the consumer 004 explicitly deferred to ("Librarian reports … are out of scope for this
  feature. They are read-only projections over the history this feature already commits to keeping
  immutable, and can be built later without changing anything specified here").
- Depends on [specs/003-membership-rules/spec.md](../003-membership-rules/spec.md) only indirectly,
  through the membership-gating rule (every application-service call is refused unless the caller is
  an enrolled, Active member) that already governs every module; this feature adds the Librarian/
  Administrator role restriction on top of that baseline (FR-012).
- This feature does not block any currently-planned feature; it completes the last User Story named
  by the original product specification ([001-tool-library](../001-tool-library/spec.md)) that had not
  yet been turned into its own feature.
