using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ToolShare.Catalog;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Catalog.Tools;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;

namespace ToolShare.Notifications;

/// <summary>
/// Shared impersonation, Catalog-fixture, and event-publishing helpers for
/// Notifications' own test suites — mirrors
/// <c>ToolShare.Lending.LendingAuthorizationTestBase</c>.
/// </summary>
public abstract class NotificationsAuthorizationTestBase : NotificationsApplicationTestBase
{
    protected readonly ICategoryAppService CategoryAppService;
    protected readonly IToolAppService ToolAppService;
    protected readonly IToolInstanceAppService ToolInstanceAppService;
    protected readonly ILocalEventBus LocalEventBus;
    protected readonly FakeEmailSender EmailSender;

    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IIdentityUserRepository _identityUserRepository;

    protected NotificationsAuthorizationTestBase()
    {
        CategoryAppService = GetRequiredService<ICategoryAppService>();
        ToolAppService = GetRequiredService<IToolAppService>();
        ToolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
        LocalEventBus = GetRequiredService<ILocalEventBus>();
        EmailSender = GetRequiredService<FakeEmailSender>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _identityUserRepository = GetRequiredService<IIdentityUserRepository>();
    }

    /// <summary>Same "must be called directly by the caller, never returned across an await" rule as the Catalog equivalent.</summary>
    protected IDisposable Impersonate(ClaimsPrincipal principal)
    {
        return _currentPrincipalAccessor.Change(principal);
    }

    protected IDisposable AsAnonymous()
    {
        return Impersonate(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    protected IDisposable AsMemberWithNoGrants()
    {
        return Impersonate(BuildPrincipal(NotificationsTestPrincipals.NoGrantsUserId, "no-grants-user"));
    }

    protected IDisposable AsOtherMember()
    {
        return Impersonate(BuildPrincipal(NotificationsTestPrincipals.OtherMemberId, "other-member-user"));
    }

    protected IDisposable AsNoEmailMember()
    {
        return Impersonate(BuildPrincipal(NotificationsTestPrincipals.NoEmailUserId, "no-email-user"));
    }

    /// <summary>A genuinely fresh identity with no backing member row anywhere.</summary>
    protected IDisposable AsAuthenticatedNonMember()
    {
        return Impersonate(BuildPrincipal(Guid.NewGuid(), "never-enrolled-user"));
    }

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

    /// <summary>Creates a category/tool/instance as admin, so loan-notification tests have a real tool to name.</summary>
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
