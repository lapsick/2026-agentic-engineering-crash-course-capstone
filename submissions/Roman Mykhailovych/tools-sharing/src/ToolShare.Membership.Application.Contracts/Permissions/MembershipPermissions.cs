namespace ToolShare.Membership.Permissions;

/// <summary>
/// The permission tree from contracts/membership-permissions.md. Constants are
/// <c>public const string</c> so the host's role seeder — and any future admin
/// UI — can grant them without magic strings, exactly as <c>CatalogPermissions</c>
/// does.
/// </summary>
public static class MembershipPermissions
{
    public const string GroupName = "Membership";

    public static class Members
    {
        public const string Default = GroupName + ".Members";
        public const string Enrol = Default + ".Enrol";
        public const string ChangeRole = Default + ".ChangeRole";
        public const string Deactivate = Default + ".Deactivate";
        public const string AdjustRating = Default + ".AdjustRating";
    }

    public static class Rules
    {
        public const string Default = GroupName + ".Rules";
        public const string Edit = Default + ".Edit";
    }

    public static class Reliability
    {
        public const string Default = GroupName + ".Reliability";
        public const string Report = Default + ".Report";
    }
}
