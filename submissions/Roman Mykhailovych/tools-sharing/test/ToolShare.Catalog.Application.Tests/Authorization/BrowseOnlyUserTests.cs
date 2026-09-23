using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Catalog.Authorization;

/// <summary>FR-012, SC-003: an authenticated user holding no Catalog grants is denied every management operation, but browsing succeeds.</summary>
public class BrowseOnlyUserTests : CatalogAuthorizationTestBase
{
    [Fact]
    public async Task Every_management_operation_is_denied_for_a_user_with_no_catalog_grants()
    {
        var (category, tool, instance) = await SeedCatalogDataAsync();

        using (AsAuthenticatedUserWithNoGrants())
        {
            foreach (var (name, action) in BuildManagementOperations(category, tool, instance))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(action, $"{name} should have been denied for a user with no Catalog grants");
            }
        }
    }

    [Fact]
    public async Task Browse_operations_succeed_for_a_user_with_no_catalog_grants()
    {
        var (_, tool, instance) = await SeedCatalogDataAsync();

        using (AsAuthenticatedUserWithNoGrants())
        {
            foreach (var (name, action) in BuildBrowseOperations(tool, instance))
            {
                await Should.NotThrowAsync(action, $"{name} should be allowed for any authenticated, enrolled Active member");
            }
        }
    }

    /// <summary>
    /// 003 ripple (research R10, T064): from 003 onward browsing is gated on
    /// active membership, not merely authentication — an authenticated principal
    /// with no member record anywhere is refused every Catalog operation,
    /// including browsing, distinct from <see cref="Browse_operations_succeed_for_a_user_with_no_catalog_grants"/>
    /// (an enrolled member with no Catalog grants, who DOES get through).
    /// </summary>
    [Fact]
    public async Task Browse_operations_are_refused_for_an_authenticated_principal_with_no_member_record()
    {
        var (_, tool, instance) = await SeedCatalogDataAsync();

        using (AsAuthenticatedNonMember())
        {
            foreach (var (name, action) in BuildBrowseOperations(tool, instance))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(action, $"{name} should have been refused for an authenticated, never-enrolled principal");
            }
        }
    }

    [Fact]
    public async Task Every_management_operation_is_also_refused_for_an_authenticated_principal_with_no_member_record()
    {
        var (category, tool, instance) = await SeedCatalogDataAsync();

        using (AsAuthenticatedNonMember())
        {
            foreach (var (name, action) in BuildManagementOperations(category, tool, instance))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(action, $"{name} should have been refused for an authenticated, never-enrolled principal");
            }
        }
    }
}
