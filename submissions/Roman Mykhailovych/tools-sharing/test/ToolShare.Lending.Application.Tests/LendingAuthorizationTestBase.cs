using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ToolShare.Catalog;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Catalog.Tools;
using ToolShare.Identity;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;

namespace ToolShare.Lending;

/// <summary>
/// Shared impersonation and Catalog-fixture helpers for Lending's own test
/// suites — mirrors <c>ToolShare.Catalog.Authorization.CatalogAuthorizationTestBase</c>
/// and <c>ToolShare.Membership.MembershipAuthorizationTestBase</c>.
/// </summary>
public abstract class LendingAuthorizationTestBase : LendingApplicationTestBase
{
    protected readonly ICategoryAppService CategoryAppService;
    protected readonly IToolAppService ToolAppService;
    protected readonly IToolInstanceAppService ToolInstanceAppService;

    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IIdentityUserRepository _identityUserRepository;

    protected LendingAuthorizationTestBase()
    {
        CategoryAppService = GetRequiredService<ICategoryAppService>();
        ToolAppService = GetRequiredService<IToolAppService>();
        ToolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _identityUserRepository = GetRequiredService<IIdentityUserRepository>();
    }

    /// <summary>Same "must be called directly by the caller, never returned across an await" rule as the Catalog equivalent — see that type's remarks.</summary>
    protected IDisposable Impersonate(ClaimsPrincipal principal)
    {
        return _currentPrincipalAccessor.Change(principal);
    }

    protected IDisposable AsAnonymous()
    {
        return Impersonate(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    /// <summary>An enrolled, Active member holding no Lending permissions — see <see cref="LendingTestPrincipals.NoGrantsUserId"/>.</summary>
    protected IDisposable AsMemberWithNoGrants()
    {
        return Impersonate(BuildPrincipal(LendingTestPrincipals.NoGrantsUserId, "no-grants-user"));
    }

    /// <summary>See <see cref="LendingTestPrincipals.LibrarianUserId"/>.</summary>
    protected IDisposable AsLibrarian()
    {
        return Impersonate(BuildPrincipal(LendingTestPrincipals.LibrarianUserId, "librarian-user", LibrarianRoleDataSeedContributor.RoleName));
    }

    /// <summary>A genuinely fresh identity with no backing member row anywhere — the "signed in but never enrolled" case (FR-019).</summary>
    protected IDisposable AsAuthenticatedNonMember()
    {
        return Impersonate(BuildPrincipal(Guid.NewGuid(), "never-enrolled-user"));
    }

    /// <summary>Looks up the seeded admin's real identity — does not impersonate; pass the result to <see cref="Impersonate"/> yourself.</summary>
    protected async Task<ClaimsPrincipal> BuildAdminPrincipalAsync()
    {
        var admin = await _identityUserRepository.FindByNormalizedUserNameAsync("ADMIN")
            ?? throw new InvalidOperationException("The seeded 'admin' user was not found — template seeding did not run.");

        return BuildPrincipal(admin.Id, admin.UserName, "admin");
    }

    protected static ClaimsPrincipal BuildPrincipal(Guid userId, string userName, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, userId.ToString()),
            new(AbpClaimTypes.UserName, userName)
        };
        claims.AddRange(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    /// <summary>Creates a category/tool/instance as admin, so reservation/checkout tests have a real, available instance to act on.</summary>
    protected async Task<(CategoryDto Category, ToolDto Tool, ToolInstanceDto Instance)> SeedCatalogDataAsync()
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var category = await CategoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
            var tool = await ToolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer", CategoryId = category.Id });
            var instance = await ToolInstanceAppService.CreateAsync(new CreateToolInstanceDto
            {
                ToolId = tool.Id,
                SerialNumber = Guid.NewGuid().ToString("N")[..8],
                Condition = ToolCondition.Good
            });

            return (category, tool, instance);
        }
    }
}
