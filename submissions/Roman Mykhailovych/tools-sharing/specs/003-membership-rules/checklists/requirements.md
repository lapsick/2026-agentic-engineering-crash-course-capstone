# Specification Quality Checklist: Membership & Community Rules

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-30
**Feature**: [spec.md](../spec.md)

## Content Quality

- [X] No implementation details (languages, frameworks, APIs)
- [X] Focused on user value and business needs
- [X] Written for non-technical stakeholders
- [X] All mandatory sections completed

## Requirement Completeness

- [X] No [NEEDS CLARIFICATION] markers remain
- [X] Requirements are testable and unambiguous
- [X] Success criteria are measurable
- [X] Success criteria are technology-agnostic (no implementation details)
- [X] All acceptance scenarios are defined
- [X] Edge cases are identified
- [X] Scope is clearly bounded
- [X] Dependencies and assumptions identified

## Feature Readiness

- [X] All functional requirements have clear acceptance criteria
- [X] User scenarios cover primary flows
- [X] Feature meets measurable outcomes defined in Success Criteria
- [X] No implementation details leak into specification

## Notes

Validation performed 2026-07-30 (single pass, no iterations required). Detail on the
three items that needed a judgement call:

1. **"No implementation details"** — the Assumptions section names the module shape
   (own projects, own database schema, references to other modules by identifier,
   a published contracts surface). This is deliberate and follows the precedent set by
   [002's spec](../../002-catalog-foundation/spec.md): these are constraints *inherited*
   from `.specify/memory/constitution.md`, recorded as inputs rather than chosen here.
   No language, framework, package, or API is named anywhere in the requirements.

2. **"Success criteria are technology-agnostic"** — SC-011 refers to "a separate module
   or test" consuming the contracts "without referencing any Membership-internal type".
   The module boundary is itself the product requirement here (Constitution Principle II),
   so the criterion cannot be stated without referring to it; it mirrors 002's SC-007,
   which was accepted on the same grounds. It names no technology.

3. **Zero clarification markers** — every gap was closed with a documented default rather
   than a question. The three that were closest to needing the user's input, and the
   default chosen for each:
   - *Deactivating a member who still holds tools* → deactivation succeeds, blocks future
     borrowing only, and announces the change; Membership cannot see loans, so any other
     answer would require it to know about Lending.
   - *How ratings get updated before Lending exists* → Lending reports outcomes *into*
     Membership through a published contract, keeping the dependency direction
     Membership ← Lending.
   - *Concrete default rule values* → stated explicitly in Assumptions (14-day term,
     limit 3, threshold 50, reduced limit 1, −10 / −20 / +2 points, 24 h offer window,
     2-day reminder lead) so US2 is testable on a fresh installation.

   All three are recorded in Assumptions and are cheap to revisit in `/speckit-clarify`
   if the owner disagrees.
