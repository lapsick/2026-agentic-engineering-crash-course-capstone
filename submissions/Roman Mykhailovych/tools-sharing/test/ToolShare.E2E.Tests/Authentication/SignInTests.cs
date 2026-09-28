using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace ToolShare.E2E.Authentication;

/// <summary>
/// Product spec: authentication is mandatory, there is no public access; the bootstrap
/// administrator seeded by DbMigrator is an enrolled member and can use the catalog.
/// </summary>
public class SignInTests : E2ETestBase
{
    public SignInTests(E2EAppFixture app)
        : base(app)
    {
    }

    [Fact]
    public Task Anonymous_visitor_opening_the_catalog_is_sent_to_the_login_page() => RunAsync(async () =>
    {
        await Page.GotoAsync("/catalog");

        await Expect(Page).ToHaveURLAsync(new Regex("/Account/Login", RegexOptions.IgnoreCase));
    });

    [Fact]
    public Task Seeded_administrator_signs_in_and_sees_the_starter_categories() => RunAsync(async () =>
    {
        await SignInAsAdminAsync();

        await Page.GotoAsync("/catalog/categories");

        // DbMigrator seeds the starter categories, so the table is never empty here.
        await Expect(Page.Locator(".mud-table-body tr").First).ToBeVisibleAsync();
    });
}
