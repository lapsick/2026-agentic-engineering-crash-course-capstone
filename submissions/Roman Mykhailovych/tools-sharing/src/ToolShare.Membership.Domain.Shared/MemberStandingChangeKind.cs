namespace ToolShare.Membership;

/// <summary>
/// Discriminates the rows of the single, unified <c>MemberStandingChanges</c>
/// append-only history table (research R8) — the same shape Catalog uses for
/// <c>ToolInstanceStateChange</c>.
/// </summary>
public enum MemberStandingChangeKind
{
    /// <summary>The member record was created.</summary>
    Enrolled = 0,

    /// <summary>Active &lt;-&gt; Deactivated.</summary>
    StatusChanged = 1,

    /// <summary>Role reassigned.</summary>
    RoleChanged = 2,

    /// <summary>A rating-affecting outcome was applied.</summary>
    RatingOutcome = 3
}
