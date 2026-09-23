using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Catalog.Authorization;

/// <summary>FR-011, SC-003: every management operation is rejected for an anonymous (unauthenticated) principal.</summary>
public class AnonymousAccessTests : CatalogAuthorizationTestBase
{
    [Fact]
    public async Task Every_management_operation_throws_for_an_anonymous_principal()
    {
        var (category, tool, instance) = await SeedCatalogDataAsync();

        using (AsAnonymous())
        {
            foreach (var (name, action) in BuildManagementOperations(category, tool, instance))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(action, $"{name} should have thrown for an anonymous principal");
            }
        }
    }

    [Fact]
    public async Task Browse_operations_also_throw_for_an_anonymous_principal()
    {
        var (_, tool, instance) = await SeedCatalogDataAsync();

        using (AsAnonymous())
        {
            foreach (var (name, action) in BuildBrowseOperations(tool, instance))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(action, $"{name} should have thrown for an anonymous principal");
            }
        }
    }
}
