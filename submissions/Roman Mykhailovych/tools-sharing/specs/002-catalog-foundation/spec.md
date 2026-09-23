# Feature Specification: Application Foundation & Catalog Vertical Slice

**Feature Branch**: `002-catalog-foundation`

**Created**: 2026-07-27

**Status**: Draft

**Input**: User description: "Feature: application foundation and first vertical slice (Catalog module). Scope: modular-monolith skeleton (shared auth/authz infrastructure, PostgreSQL connection, separate DbMigrator, docker compose); a fully working Catalog module (tools, categories, instances with serial numbers, condition, photos; CRUD + search/filter with a Blazor UI); public Catalog contracts (Application.Contracts) for later modules, plus one example domain event from the Catalog schema. Out of scope: Membership, Lending, Maintenance, Notifications (later features 002+). Reference the product spec in specs/001-tool-library/spec.md for shared context and rules without duplicating it."

## Context

This feature establishes the technical foundation for the Shared Tool Library product and delivers the first end-to-end vertical slice: the **Catalog**. It does not re-state product rules — the authoritative product specification lives in [specs/001-tool-library/spec.md](../001-tool-library/spec.md) and governs shared concepts referenced here (unique inventory number per instance; the fixed 4-level condition scale New → Good → Worn → Damaged; the three roles Member / Librarian / Administrator; authentication required with no public access).

This feature is the enabling slice that later features (Membership, Lending, Maintenance, Notifications — 003+) build upon by referencing the public Catalog contracts and subscribing to Catalog domain events. Its "users" therefore include not only catalog operators and members but also operators who run the system and authors of downstream modules who depend on Catalog's published boundary.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Manage the tool catalog (Priority: P1)

A Librarian maintains the shared pool as data: they create and edit categories, create tools (a logical model with a name and category), and register individual physical instances under a tool — each with a unique serial/inventory number, a current condition, and one or more photos. They can update these records and retire an instance from circulation while its record is preserved.

**Why this priority**: This is the core value of the slice — it replaces the messenger/Excel record-keeping with a structured, authoritative catalog and is the prerequisite for every later feature. Without it there is nothing for Lending or Maintenance to reference.

**Independent Test**: Sign in as a Librarian, create a category and a tool, register two instances with distinct serial numbers, attach photos, set conditions, edit one record, retire the other, and confirm all changes persist and are reflected in the catalog.

**Acceptance Scenarios**:

1. **Given** a signed-in Librarian, **When** they create a category "Power Tools" and a tool "Rotary Hammer" in it, **Then** the tool appears in the catalog under that category.
2. **Given** the tool "Rotary Hammer", **When** the Librarian registers two instances with serial numbers "RH-001" and "RH-002", **Then** each becomes a distinct catalog unit with its own serial number and condition.
3. **Given** an instance being registered or edited, **When** the Librarian sets its condition, **Then** only the fixed scale values (New, Good, Worn, Damaged) are accepted.
4. **Given** an instance, **When** the Librarian attaches one or more photos, **Then** the photos are stored and shown with the instance.
5. **Given** the Librarian attempts to register an instance with a serial number already used within the catalog, **Then** the system rejects it and states the number must be unique.
6. **Given** an instance in circulation, **When** the Librarian retires it with a stated reason, **Then** it is marked retired, is excluded from normal catalog browsing by default, and its record and history remain retrievable.

---

### User Story 2 - Find and view tools (Priority: P1)

An authenticated user (Member or Librarian) opens the catalog, searches by name and filters by category, and views a tool's details including its instances, each instance's serial number, condition, and photos.

**Why this priority**: Discoverability is half of the catalog's value ("who has what, and what do we even own"). It is independently demonstrable and validates the read side of the slice end-to-end through the UI.

**Independent Test**: Sign in, search for a tool by a partial name, filter the list by category, open a result, and confirm the instance list with serial numbers, conditions, and photos is displayed.

**Acceptance Scenarios**:

1. **Given** a populated catalog, **When** a user searches by a partial tool name, **Then** matching tools are listed.
2. **Given** a populated catalog, **When** a user filters by a category, **Then** only tools in that category are listed.
3. **Given** a tool with instances, **When** a user opens it, **Then** they see each instance with its serial number, condition, and photos.
4. **Given** an empty result set, **When** a search or filter matches nothing, **Then** the user sees a clear empty-state message rather than an error.
5. **Given** a retired instance, **When** a user browses normally, **Then** it is not shown among available catalog units unless retired items are explicitly requested.

---

### User Story 3 - Secure, role-aware access (Priority: P1)

Access requires authentication; unauthenticated visitors cannot reach the catalog. Catalog management actions (create/edit/retire) are restricted to the Librarian/Administrator role, while browsing and viewing are available to any authenticated user. The permission boundary for Catalog is defined and enforced so later modules inherit a consistent authorization model.

**Why this priority**: The product mandates "authentication required, no public access." Getting the shared auth/authz skeleton and Catalog permissions right in the first slice sets the pattern every later module reuses; retrofitting it later is costly.

**Independent Test**: Attempt to reach the catalog while signed out (blocked/redirected to sign-in); sign in as a browse-only user and confirm management actions are hidden/denied; sign in with the management role and confirm they are allowed.

**Acceptance Scenarios**:

1. **Given** an unauthenticated visitor, **When** they request any catalog page, **Then** they are denied access and directed to authenticate.
2. **Given** a signed-in user without the Catalog management permission, **When** they attempt to create, edit, or retire a catalog record, **Then** the action is denied.
3. **Given** a signed-in user with the Catalog management permission, **When** they perform the same actions, **Then** they are allowed.
4. **Given** a fresh installation, **When** it is first started, **Then** an initial administrator account exists so the catalog can be populated before the Membership feature is built.

---

### User Story 4 - Reproducibly run the system and evolve its schema (Priority: P2)

An operator or developer can start the whole system — application and database — reproducibly from a single defined setup, and apply all database schema and seed data through a dedicated migration step that is separate from the running application.

**Why this priority**: A reproducible run-and-migrate foundation is what makes every subsequent feature buildable and testable. It has lower user-facing priority than the catalog itself but is a hard dependency for delivering and demonstrating the slice.

**Independent Test**: From a clean checkout, bring up the defined environment, run the dedicated migration step to create the schema and seed initial data, then start the application and reach a working catalog.

**Acceptance Scenarios**:

1. **Given** a clean environment, **When** the operator brings up the defined setup, **Then** the application and its database start and can connect to each other.
2. **Given** a database without the current schema, **When** the operator runs the dedicated migration step, **Then** the schema is created/updated and initial seed data (including the initial administrator and Catalog permissions) is applied.
3. **Given** the application is running, **When** schema changes are needed, **Then** they are delivered through the migration step rather than by the application mutating its own schema at runtime.

---

### User Story 5 - Publish a stable Catalog boundary for other modules (Priority: P2)

A downstream module author can depend on Catalog's public contracts (the data shapes and operations other modules are allowed to use) without depending on Catalog's internal implementation, and can subscribe to at least one Catalog domain event to react to catalog changes.

**Why this priority**: The product is a modular monolith where Lending, Maintenance, and Notifications reference Catalog. Defining a clean public boundary and one worked example of a domain event now prevents later modules from reaching into Catalog internals and establishes the integration pattern.

**Independent Test**: From a separate module or test, reference only the published Catalog contracts to look up a tool/instance, and subscribe a handler to the example Catalog domain event; trigger the event by performing the corresponding catalog action and confirm the handler receives it.

**Acceptance Scenarios**:

1. **Given** the published Catalog contracts, **When** another module needs catalog data, **Then** it can obtain it through the public contracts without referencing Catalog's internal types.
2. **Given** a subscribed handler for the example Catalog domain event, **When** the corresponding catalog action occurs (e.g., an instance is registered or retired), **Then** the event is published and the handler receives it with the relevant identifiers.
3. **Given** the public contracts, **When** Catalog's internal implementation changes without changing the contract, **Then** dependent modules continue to work unchanged.

---

### Edge Cases

- What happens when a photo upload exceeds the allowed size or is an unsupported format? (Rejected with a clear message; see Assumptions for limits.)
- What happens when a Librarian tries to retire an instance that another module might reference later — retirement preserves the record and history and does not hard-delete.
- What happens when a category that still contains tools is deleted? (Deletion is blocked or requires reassignment; the catalog never leaves tools orphaned.)
- How does search behave with diacritics/case (e.g., Ukrainian names) — matching is case-insensitive and accent-tolerant.
- What happens when the migration step is run twice? (It is idempotent; re-running does not duplicate seed data or corrupt the schema.)
- What happens when the database is unreachable at startup? (The application surfaces a clear, non-cryptic failure rather than silently serving empty pages.)
- What happens when two Librarians edit the same record concurrently? (The second save is detected as a conflict rather than silently overwriting.)

## Requirements *(mandatory)*

### Functional Requirements — Catalog

- **FR-001**: The system MUST allow an authorized user to create, edit, and list categories, and MUST prevent a category from being deleted while tools are still assigned to it.
- **FR-002**: The system MUST allow an authorized user to create and edit tools, each with a name and exactly one owning category.
- **FR-003**: The system MUST allow an authorized user to register physical instances under a tool, each carrying a serial/inventory number that is unique within the catalog. (Aligns with the product rule that identical models are separate instances with their own numbers.)
- **FR-004**: The system MUST record each instance's condition using only the fixed 4-level scale defined in the product spec (New → Good → Worn → Damaged).
- **FR-005**: The system MUST allow one or more photos to be attached to an instance and displayed with it.
- **FR-006**: The system MUST allow an authorized user to retire an instance from circulation with a stated reason, preserving the instance record for later reference and excluding it from normal browsing by default.
- **FR-007**: The system MUST allow authenticated users to search tools by name (case-insensitive, accent-tolerant) and filter by category, and to view a tool with its instances, serial numbers, conditions, and photos.
- **FR-008**: The system MUST present a clear empty state when a search or filter returns no results, distinct from an error state.
- **FR-009**: The system MUST validate inputs (required fields, unique serial number, allowed condition values, photo size/format) and return clear, user-facing messages on rejection.
- **FR-010**: The system MUST detect concurrent conflicting edits to the same catalog record instead of silently overwriting.

### Functional Requirements — Foundation, Security & Boundary

- **FR-011**: The system MUST require authentication for all catalog access; unauthenticated requests MUST be denied and directed to sign in (no public/anonymous access).
- **FR-012**: The system MUST enforce a defined Catalog permission boundary: management actions (create/edit/retire) restricted to the Librarian/Administrator role, while browsing/viewing is available to any authenticated user.
- **FR-013**: The system MUST provide an initial administrator account on a fresh installation so the catalog can be operated before the Membership feature exists.
- **FR-014**: The system MUST be startable as a defined, reproducible environment comprising the application and its database, able to connect to each other from a clean setup.
- **FR-015**: The system MUST apply all database schema creation/changes and initial seed data through a dedicated migration step that is separate from the running application; the running application MUST NOT mutate its own schema.
- **FR-016**: The migration/seed step MUST be idempotent — running it more than once MUST NOT duplicate seed data or corrupt the schema.
- **FR-017**: The system MUST publish stable public Catalog contracts (the data shapes and operations exposed to other modules) that dependent modules can consume without referencing Catalog's internal implementation.
- **FR-018**: The system MUST publish at least one Catalog domain event (e.g., instance registered or instance retired) that other modules can subscribe to, carrying the identifiers needed to react.
- **FR-019**: The system MUST keep Catalog's internal implementation changeable without breaking dependent modules, so long as the public contract is unchanged.
- **FR-020**: The system MUST surface a clear, actionable failure when the database is unreachable at startup rather than serving empty or misleading pages.

### Key Entities *(include if feature involves data)*

- **Category**: a classification for tools (name); a tool belongs to exactly one; cannot be removed while tools reference it.
- **Tool (Catalog Entry)**: the logical model of a tool (name, owning category); groups one or more instances. (Same concept as in the product spec.)
- **ToolInstance**: a specific physical unit with a unique serial/inventory number, a current condition (fixed 4-level scale), attached photos, and a circulation state limited in this feature to *in circulation* or *retired*. (Statuses driven by Lending/Maintenance are out of scope here.)
- **Photo**: an image attached to an instance, subject to size/format limits.
- **CatalogPermission**: the authorization boundary distinguishing Catalog management from browsing.
- **Administrator (bootstrap)**: the initial account seeded on installation; full member/role management is deferred to the Membership feature.
- **Catalog Public Contract**: the published surface (data shapes + operations) other modules may depend on.
- **Catalog Domain Event**: the published notification of a catalog change (example event) that other modules may subscribe to.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Librarian can register a new tool with at least one instance (serial number, condition, photo) end-to-end through the UI in under 3 minutes without assistance.
- **SC-002**: An authenticated user can locate a specific tool by name or category and open its instance details in under 30 seconds.
- **SC-003**: 100% of catalog management actions are blocked for users without the Catalog management permission and for unauthenticated visitors (0 unauthorized successes).
- **SC-004**: 100% of attempts to register a duplicate serial number within the catalog are rejected.
- **SC-005**: From a clean checkout, an operator can bring up the environment, apply migrations/seed, and reach a working catalog in under 15 minutes following the documented steps.
- **SC-006**: Running the migration/seed step twice in a row produces an identical database state (idempotent) with no duplicated seed rows.
- **SC-007**: A separate module/test can consume the published Catalog contracts and receive the example domain event without referencing any Catalog-internal type (verified by a passing integration test).
- **SC-008**: Search returns correct case-insensitive, accent-tolerant matches for tool names, including Ukrainian-language names.
- **SC-009**: The initial administrator account is present and able to perform all Catalog management actions immediately after a fresh install.

## Assumptions

- **Technology stack is a given constraint from the requester, not chosen by this spec**: an ABP-based modular monolith with a Blazor UI, PostgreSQL as the database, a dedicated DbMigrator for schema/seed, and a docker compose definition for the reproducible environment. These are recorded here as inputs; the requirements above stay capability-focused.
- This installation serves a single community; multi-community/multi-tenant separation is out of scope (per the product spec).
- Membership, Lending, Maintenance, and Notifications are explicitly out of scope and delivered in later features (003+); this feature only exposes the Catalog boundary they will consume.
- Because Membership is deferred, full role/member management is out of scope; a single seeded administrator plus the Catalog permission boundary is sufficient for this slice. Any authenticated user may browse; management is limited to the administrator/Librarian role.
- Instance circulation state in this feature is limited to *in circulation* and *retired*; the *on loan* and *under maintenance* states are introduced by the Lending and Maintenance features and are out of scope here.
- Photo handling uses reasonable defaults: common web image formats (e.g., JPEG/PNG/WebP) with a per-photo size limit (assumed ~5 MB) and a modest per-instance count limit (assumed up to 5); exact limits are configurable.
- The example Catalog domain event is assumed to be an instance-lifecycle event (instance registered and/or instance retired); the precise event(s) are finalized during planning.
- Authentication uses the framework's built-in identity with username/password sign-in; external identity providers are out of scope for this feature.
- "Reproducible environment" targets a local developer/operator setup; production hardening (secrets management, scaling, backups) is out of scope for this feature.

## Dependencies

- Depends on and must remain consistent with the product specification: [specs/001-tool-library/spec.md](../001-tool-library/spec.md) (shared roles, condition scale, uniqueness and access rules).
- Later features (003+: Membership, Lending, Maintenance, Notifications) depend on the public Catalog contracts (FR-017) and the example domain event (FR-018) delivered here.
