using ToolShare.Membership.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace ToolShare.Membership.Permissions;

public class MembershipPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var membershipGroup = context.AddGroup(MembershipPermissions.GroupName, L("Permission:Membership"));

        var membersPermission = membershipGroup.AddPermission(MembershipPermissions.Members.Default, L("Permission:Membership.Members"));
        membersPermission.AddChild(MembershipPermissions.Members.Enrol, L("Permission:Membership.Members.Enrol"));
        membersPermission.AddChild(MembershipPermissions.Members.ChangeRole, L("Permission:Membership.Members.ChangeRole"));
        membersPermission.AddChild(MembershipPermissions.Members.Deactivate, L("Permission:Membership.Members.Deactivate"));
        membersPermission.AddChild(MembershipPermissions.Members.AdjustRating, L("Permission:Membership.Members.AdjustRating"));

        var rulesPermission = membershipGroup.AddPermission(MembershipPermissions.Rules.Default, L("Permission:Membership.Rules"));
        rulesPermission.AddChild(MembershipPermissions.Rules.Edit, L("Permission:Membership.Rules.Edit"));

        var reliabilityPermission = membershipGroup.AddPermission(MembershipPermissions.Reliability.Default, L("Permission:Membership.Reliability"));
        reliabilityPermission.AddChild(MembershipPermissions.Reliability.Report, L("Permission:Membership.Reliability.Report"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<MembershipResource>(name);
    }
}
