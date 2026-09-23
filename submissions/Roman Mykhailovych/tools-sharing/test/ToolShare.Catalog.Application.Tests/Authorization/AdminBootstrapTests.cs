using System.Threading.Tasks;
using Xunit;

namespace ToolShare.Catalog.Authorization;

/// <summary>FR-013, SC-009: a freshly seeded admin can perform every Catalog management operation on a fresh install.</summary>
public class AdminBootstrapTests : CatalogAuthorizationTestBase
{
    [Fact]
    public async Task The_seeded_admin_can_perform_every_management_operation()
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            await RunFullManagementLifecycleAsync();
        }
    }
}
