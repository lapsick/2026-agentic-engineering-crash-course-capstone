namespace ToolShare.Lending;

/// <summary>
/// Business-exception codes raised by the Lending domain, localized through
/// <see cref="Localization.LendingResource"/>. Mirrors the pattern documented
/// for Catalog and Membership.
/// </summary>
public static class LendingDomainErrorCodes
{
    /// <summary>RES-01.</summary>
    public const string LoanTermExceeded = "Lending:LoanTermExceeded";

    /// <summary>RES-02, LOAN-06.</summary>
    public const string InstanceUnavailable = "Lending:InstanceUnavailable";

    /// <summary>RES-03 — the friendly pre-check; the exclusion constraint is the authority under concurrency.</summary>
    public const string InstanceAlreadyReservedForRange = "Lending:InstanceAlreadyReservedForRange";

    /// <summary>RES-04.</summary>
    public const string MemberHasOverdueLoan = "Lending:MemberHasOverdueLoan";

    /// <summary>RES-05.</summary>
    public const string ConcurrentLoanLimitReached = "Lending:ConcurrentLoanLimitReached";

    /// <summary>RES-06.</summary>
    public const string ReservationNotCancellable = "Lending:ReservationNotCancellable";

    /// <summary>WL-02.</summary>
    public const string AlreadyOnWaitlist = "Lending:AlreadyOnWaitlist";

    /// <summary>MAINT-02.</summary>
    public const string MaintenanceCostRequired = "Lending:MaintenanceCostRequired";

    /// <summary>MAINT-01 — the friendly pre-check; the filtered unique index is the authority under concurrency.</summary>
    public const string MaintenanceRequestAlreadyOpen = "Lending:MaintenanceRequestAlreadyOpen";

    /// <summary>LOAN-01 — checkout attempted without a matching, not-yet-checked-out reservation.</summary>
    public const string NoMatchingActiveReservation = "Lending:NoMatchingActiveReservation";

    /// <summary>
    /// Defensive guard for entity-internal state transitions that a caller
    /// cannot normally trigger through the application layer (e.g. offering a
    /// waitlist entry that is not <c>Waiting</c>) — reachable only if an
    /// invariant this module itself guarantees elsewhere has been violated.
    /// </summary>
    public const string InvalidStateTransition = "Lending:InvalidStateTransition";

    /// <summary>
    /// 006-librarian-reports FR-009: a report's date range starts after it ends,
    /// or a bound the report requires was not supplied. Rejected rather than
    /// answered with an empty result, which would read as "nothing happened in
    /// that period" — a wrong answer to a question that was never valid.
    /// </summary>
    public const string InvalidReportDateRange = "Lending:InvalidReportDateRange";
}
