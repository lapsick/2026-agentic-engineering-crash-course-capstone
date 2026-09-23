namespace ToolShare.Membership;

/// <summary>
/// Business-exception codes raised by the Membership domain, localized through
/// <see cref="Localization.MembershipResource"/>. Mirrors the pattern
/// documented for Catalog, collected here (rather than as raw literals) because
/// contracts/membership-app-services.md enumerates them as a single published
/// table this feature must keep in sync with.
/// </summary>
public static class MembershipDomainErrorCodes
{
    /// <summary>MR-01.</summary>
    public const string DisplayNameRequired = "Membership:DisplayNameRequired";

    /// <summary>MR-02 — identity id already has a member record (FR-002).</summary>
    public const string IdentityAlreadyEnrolled = "Membership:IdentityAlreadyEnrolled";

    /// <summary>MR-04.</summary>
    public const string AlreadyDeactivated = "Membership:AlreadyDeactivated";

    /// <summary>MR-04.</summary>
    public const string AlreadyActive = "Membership:AlreadyActive";

    /// <summary>MR-05.</summary>
    public const string RoleUnchanged = "Membership:RoleUnchanged";

    /// <summary>MR-10 (FR-007, SC-006).</summary>
    public const string LastAdministrator = "Membership:LastAdministrator";

    /// <summary>Outcome reported for a non-member (FR-027); also the gate's refusal reason.</summary>
    public const string NotAnEnrolledMember = "Membership:NotAnEnrolledMember";

    /// <summary>The enrolment gate refusing a deactivated member (FR-006).</summary>
    public const string MembershipInactive = "Membership:MembershipInactive";

    /// <summary>ManualAdjustment submitted through the public reporting contract (FR-021).</summary>
    public const string ManualAdjustmentNotReportable = "Membership:ManualAdjustmentNotReportable";

    /// <summary>CRR-01, with a <c>rule</c> data key naming the field (FR-014).</summary>
    public const string InvalidRule = "Membership:InvalidRule";

    /// <summary>Manual adjustment without a reason (FR-021).</summary>
    public const string AdjustmentReasonRequired = "Membership:AdjustmentReasonRequired";
}
