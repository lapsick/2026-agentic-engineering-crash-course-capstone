namespace ToolShare.Catalog.Permissions;

public static class CatalogPermissions
{
    public const string GroupName = "Catalog";

    public static class Categories
    {
        public const string Default = GroupName + ".Categories";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Tools
    {
        public const string Default = GroupName + ".Tools";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class ToolInstances
    {
        public const string Default = GroupName + ".ToolInstances";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string ChangeCondition = Default + ".ChangeCondition";
        public const string Retire = Default + ".Retire";
        public const string ManagePhotos = Default + ".ManagePhotos";

        /// <summary>Gates <c>IToolInstanceCirculationReportingAppService</c> (004-lending) — granted to Librarian and Administrator.</summary>
        public const string ReportLendingState = Default + ".ReportLendingState";
    }
}
