using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Shouldly;
using ToolShare.Membership.Localization;
using ToolShare.Membership.Permissions;
using Volo.Abp.Authorization.Permissions;
using Xunit;

namespace ToolShare.Membership;

/// <summary>Every permission <see cref="MembershipPermissionDefinitionProvider"/> defines resolves to an actual localized display name — no raw "Permission:*" key ever renders in the admin UI.</summary>
public class MembershipPermissionLocalizationTests : MembershipApplicationTestBase
{
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;
    private readonly IStringLocalizer<MembershipResource> _localizer;

    public MembershipPermissionLocalizationTests()
    {
        _permissionDefinitionManager = GetRequiredService<IPermissionDefinitionManager>();
        _localizer = GetRequiredService<IStringLocalizer<MembershipResource>>();
    }

    [Fact]
    public async Task Every_defined_Membership_permission_has_a_localized_display_name()
    {
        var groups = await _permissionDefinitionManager.GetGroupsAsync();
        var membershipGroup = groups.Single(g => g.Name == MembershipPermissions.GroupName);

        var allPermissions = membershipGroup.Permissions
            .SelectMany(FlattenWithChildren)
            .ToList();

        allPermissions.ShouldNotBeEmpty();

        foreach (var permission in allPermissions)
        {
            var localized = _localizer[$"Permission:{permission.Name}"];
            localized.ResourceNotFound.ShouldBeFalse($"'{permission.Name}' has no localized display name");
            localized.Value.ShouldNotBeNullOrWhiteSpace();
        }
    }

    private static IEnumerable<PermissionDefinition> FlattenWithChildren(PermissionDefinition permission)
    {
        yield return permission;

        foreach (var child in permission.Children.SelectMany(FlattenWithChildren))
        {
            yield return child;
        }
    }
}
