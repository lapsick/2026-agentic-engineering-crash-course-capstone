using System.Threading.Tasks;
using Xunit;

namespace ToolShare.Catalog.Authorization;

/// <summary>FR-012, SC-003: a user in the Librarian role can perform every Catalog management operation.</summary>
public class LibrarianRoleTests : CatalogAuthorizationTestBase
{
    [Fact]
    public async Task A_librarian_can_perform_every_management_operation()
    {
        using (AsLibrarian())
        {
            await RunFullManagementLifecycleAsync();
        }
    }
}
