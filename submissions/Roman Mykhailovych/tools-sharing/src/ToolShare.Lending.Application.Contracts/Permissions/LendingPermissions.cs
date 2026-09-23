namespace ToolShare.Lending.Permissions;

/// <summary>
/// The permission tree from contracts/lending-permissions.md. Creating,
/// cancelling, and joining a waitlist for one's own reservations carries no
/// permission constant — like Catalog's browsing (002) and Membership's
/// self-service (003), it is gated by the enrolment gate alone.
/// </summary>
public static class LendingPermissions
{
    public const string GroupName = "Lending";

    public static class Loans
    {
        public const string Default = GroupName + ".Loans";
        public const string Checkout = Default + ".Checkout";
        public const string Return = Default + ".Return";
    }

    public static class Maintenance
    {
        public const string Close = GroupName + ".Maintenance.Close";
    }

    /// <summary>
    /// 006-librarian-reports: a top-level permission rather than a child of
    /// <see cref="Loans"/>, because "may review community-wide analytics" is a
    /// capability separable from "may operate the loan desk" (research R8).
    /// </summary>
    public static class Reports
    {
        public const string Default = GroupName + ".Reports";
    }
}
