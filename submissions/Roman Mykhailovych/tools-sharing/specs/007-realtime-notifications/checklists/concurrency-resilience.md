# Concurrency & Resilience Checklist: Real-Time In-App Notifications

**Purpose**: Author sanity-check on the *quality* of the multi-session, reconnection, degradation, and
consistency requirements — are they complete, unambiguous, consistent, and measurable before planning is
trusted? (Tests the requirements, not the implementation.)
**Created**: 2026-08-06
**Resolved**: 2026-08-06 (all items addressed — see per-item resolution notes)
**Feature**: [spec.md](../spec.md)
**Depth**: Lightweight (author) · **Focus**: Concurrency & resilience (FR-005, FR-006, FR-008, FR-009)

## Requirement Completeness & Coverage

- [x] CHK001 Are requirements defined for a notification generated while a member's connection is
  *dropped but the session has not yet been torn down* (vs a fully rebuilt circuit and vs never
  connected)? Are these three distinct states each covered? [Coverage, Spec §FR-004/FR-005, §Edge Cases]
  → **Resolved**: all three states are now covered — never-connected (FR-004), transient drop that
  self-heals + full circuit rebuild that reconciles on reload (SC-004, distinguished explicitly), and
  the reconciliation guarantee (FR-005 / Edge Cases "Reconnection reconciliation").
- [x] CHK002 Does the degradation requirement address *partial* real-time failure (push succeeds for
  some of a member's sessions but fails for others), or only total unavailability of the live path?
  [Coverage, Spec §FR-009] → **Resolved by spec edit**: added Edge Case "Partial live-delivery failure"
  requiring per-session isolation (one session's failed push must not affect the member's other sessions
  or other members; the un-updated session reconciles on next interaction/reload).
- [x] CHK003 Are requirements specified for interleaved generation *bursts arriving across more than one
  of a member's sessions at once*, not just a burst to a single session? [Coverage/Gap, Spec §Edge Cases
  "burst"] → **Resolved by spec edit**: the "Burst of notifications" Edge Case now explicitly covers
  bursts interleaving across a member's multiple sessions, and states why re-querying the authoritative
  count (not a local tally) makes interleaving order harmless.

## Requirement Clarity & Ambiguity

- [x] CHK004 Is "reconcile to the authoritative persisted state" defined precisely enough to
  distinguish a full re-query from replaying missed pushes, and does the FR wording stay consistent with
  the Edge Cases wording that says exactly that? [Clarity, Spec §FR-005, §Edge Cases] → **Resolved (no
  edit needed)**: FR-005 ("reconcile to the authoritative persisted state … none shown twice") and the
  Edge Case ("reconciled against the authoritative persisted list rather than replaying only the missed
  pushes") already agree and are precise — reconciliation is a full re-query, not a replay.
- [x] CHK005 Is the cross-session convergence expectation for a *newly generated* notification bounded
  with a maximum window, or left as the unquantified "converging"/"within a few seconds"? [Ambiguity,
  Spec §FR-006, §SC-002] → **Resolved (analyze remediation)**: SC-002 now defines a 3-second settling
  window and FR-006 references it ("converging to the same authoritative count within the SC-002 settling
  window").
- [x] CHK006 Does "live where a session is connected, otherwise on that session's next interaction or
  reconnection" (read-state propagation) bound the *maximum staleness* a member could observe, or leave
  it open-ended? [Clarity, Spec §FR-008] → **Resolved by spec edit**: FR-008 now states a connected
  session converges within the SC-002 window, while a disconnected/idle session carries no separate bound
  and reconciles on next interaction/reload (consistent with FR-004) — the open-endedness is now an
  explicit, justified accepted-softness, not an oversight.

## Consistency

- [x] CHK007 Are the coalescing allowance ("fewer indicator refreshes as long as the **final** count is
  correct") and the rule that the indicator "always equals the authoritative unread count" mutually
  consistent about permissible *intermediate* values during a burst? [Consistency, Spec §Assumptions,
  §FR-003] → **Resolved (analyze remediation)**: FR-003 was reworded from "always equal" to "settle to …
  transient in-flight differences permitted only until the burst settles," aligning it with the
  coalescing allowance and the SC-002 window.
- [x] CHK008 Is the single-host assumption stated as a *validated precondition* that the multi-session
  consistency requirements (FR-006/FR-008) depend on — i.e., that all of a member's sessions live on one
  instance — rather than left implicit? [Assumption, Spec §Assumptions] → **Resolved by spec edit**:
  added Assumption "Single application instance (validated precondition for cross-session consistency),"
  naming FR-006/FR-008 as dependent on it and pointing to plan.md boundary note 1 / research R2 for the
  multi-instance backplane that would otherwise be required.

## Measurability of Resilience Criteria

- [x] CHK009 Can "0 notifications missed as a result of a dropped-and-restored connection" be
  objectively measured given that no outage duration or reconnection bound is specified? Is the intended
  outage envelope stated? [Measurability, Spec §SC-004] → **Resolved (analyze remediation)**: SC-004 now
  states the envelope — a transient drop self-heals within the circuit's reconnection-retry window, and a
  longer loss rebuilds the circuit and reconciles on reload — making the guarantee objectively bounded.
- [x] CHK010 Is "0 lasting discrepancies" backed by a defined settling-time threshold that makes
  "lasting" (vs a momentary in-flight difference) objectively testable? [Measurability, Spec §SC-002] →
  **Resolved (analyze remediation)**: SC-002 now defines a 3-second settling window; a discrepancy
  persisting beyond it is a failure, transient in-flight differences within it are not.

## Notes

- **Status: all 10 items resolved.** Six were satisfied by the `/speckit-analyze` remediation (CHK004,
  CHK005, CHK007, CHK009, CHK010) or already-consistent wording (CHK001 via the SC-004 edit); four were
  resolved here by targeted spec edits (CHK002, CHK003, CHK006, CHK008).
- A checked item means the *requirement is written well enough*, not that the feature works — those
  behaviors are validated by the integration tests and quickstart scenarios in tasks.md.
- The single remaining deliberate softness (CHK006: a disconnected/idle session has no staleness bound)
  is now explicit in FR-008 and justified by FR-004's "persisted state is always authoritative."
