namespace ToolShare.Membership;

/// <summary>
/// A member's standing with respect to using the application. Says nothing
/// about loans/lending — mirrors how Catalog's <c>ToolInstanceCirculationState</c>
/// only speaks to Catalog's own concerns.
/// </summary>
public enum MembershipStatus
{
    Active = 0,
    Deactivated = 1
}
