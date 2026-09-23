using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Shouldly;
using ToolShare.Lending.Localization;
using ToolShare.Lending.Permissions;
using Volo.Abp.Authorization.Permissions;
using Xunit;

namespace ToolShare.Lending;

/// <summary>Every permission <see cref="LendingPermissionDefinitionProvider"/> defines resolves to an actual localized display name — no raw "Permission:*" key ever renders in the admin UI. Mirrors Membership's equivalent test.</summary>
public class LendingPermissionLocalizationTests : LendingApplicationTestBase
{
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;
    private readonly IStringLocalizer<LendingResource> _localizer;

    public LendingPermissionLocalizationTests()
    {
        _permissionDefinitionManager = GetRequiredService<IPermissionDefinitionManager>();
        _localizer = GetRequiredService<IStringLocalizer<LendingResource>>();
    }

    [Fact]
    public async Task Every_defined_Lending_permission_has_a_localized_display_name()
    {
        var groups = await _permissionDefinitionManager.GetGroupsAsync();
        var lendingGroup = groups.Single(g => g.Name == LendingPermissions.GroupName);

        var allPermissions = lendingGroup.Permissions
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
