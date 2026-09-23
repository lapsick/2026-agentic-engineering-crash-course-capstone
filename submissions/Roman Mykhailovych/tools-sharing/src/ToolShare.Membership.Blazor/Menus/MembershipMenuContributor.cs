using System.Threading.Tasks;
using ToolShare.Membership.Localization;
using ToolShare.Membership.Permissions;
using Volo.Abp.UI.Navigation;

namespace ToolShare.Membership.Blazor.Menus;

public class MembershipMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            ConfigureMainMenu(context);
        }

        return Task.CompletedTask;
    }

    private static void ConfigureMainMenu(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<MembershipResource>();

        var membershipMenu = new ApplicationMenuItem(
            MembershipMenuNames.GroupName,
            l["Menu:Membership"],
            icon: "fas fa-users"
        );

        membershipMenu.AddItem(new ApplicationMenuItem(
            MembershipMenuNames.Members,
            l["Menu:Membership.Members"],
            "/membership/members",
            requiredPermissionName: MembershipPermissions.Members.Default
        ));

        // FR-022: available to any active member — IMyMembershipAppService's
        // only requirement is [Authorize], same reasoning as the Rules entry
        // below — so this item also carries no requiredPermissionName.
        membershipMenu.AddItem(new ApplicationMenuItem(
            MembershipMenuNames.MyMembership,
            l["Menu:Membership.MyMembership"],
            "/membership/my-membership"
        ));

        // FR-013: reading the community rules is available to any active
        // member, not gated by a specific permission grant (only an
        // Administrator holds Membership.Rules.Edit) — so, unlike the roster
        // entry above, this item carries no requiredPermissionName. The
        // enrolment gate (any active member reaching the main menu at all)
        // is the only requirement, matching ICommunityRulesAppService.GetAsync.
        membershipMenu.AddItem(new ApplicationMenuItem(
            MembershipMenuNames.Rules,
            l["Menu:Membership.Rules"],
            "/membership/rules"
        ));

        context.Menu.AddItem(membershipMenu);
    }
}
