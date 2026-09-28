using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace ToolShare.E2E.Catalog;

/// <summary>
/// The full write path through the UI: Categories page → dialog → ICategoryAppService →
/// PostgreSQL → table reload with the page's search filter.
/// </summary>
public class CategoryManagementTests : E2ETestBase
{
    public CategoryManagementTests(E2EAppFixture app)
        : base(app)
    {
    }

    [Fact]
    public Task Administrator_creates_a_category_and_finds_it_by_search() => RunAsync(async () =>
    {
        var name = $"E2E {Guid.NewGuid():N}"[..20];
        await SignInAsAdminAsync();
        await Page.GotoAsync("/catalog/categories");

        var dialog = Page.Locator(".mud-dialog");
        await ClickUntilVisibleAsync(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New category" }), dialog);

        var nameField = dialog.Locator("input").First;
        await nameField.FillAsync(name);
        await nameField.PressAsync("Tab");
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Save" }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();

        await Page.Locator(".mud-paper input").First.FillAsync(name);

        await Expect(Page.GetByRole(AriaRole.Cell, new PageGetByRoleOptions { Name = name, Exact = true })).ToBeVisibleAsync();
    });
}
