using ToolShare.Lending.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace ToolShare.Lending.Permissions;

public class LendingPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var lendingGroup = context.AddGroup(LendingPermissions.GroupName, L("Permission:Lending"));

        var loansPermission = lendingGroup.AddPermission(LendingPermissions.Loans.Default, L("Permission:Lending.Loans"));
        loansPermission.AddChild(LendingPermissions.Loans.Checkout, L("Permission:Lending.Loans.Checkout"));
        loansPermission.AddChild(LendingPermissions.Loans.Return, L("Permission:Lending.Loans.Return"));

        lendingGroup.AddPermission(LendingPermissions.Maintenance.Close, L("Permission:Lending.Maintenance.Close"));
        lendingGroup.AddPermission(LendingPermissions.Maintenance.Report, L("Permission:Lending.Maintenance.Report"));

        lendingGroup.AddPermission(LendingPermissions.Reports.Default, L("Permission:Lending.Reports"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<LendingResource>(name);
    }
}
