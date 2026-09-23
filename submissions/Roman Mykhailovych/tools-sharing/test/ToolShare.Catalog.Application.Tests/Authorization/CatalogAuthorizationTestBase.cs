using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Tools;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Identity;
using Volo.Abp.Content;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;

namespace ToolShare.Catalog.Authorization;

/// <summary>
/// Shared impersonation and fixture-data helpers for the Phase 5/US3 authorization
/// tests. Impersonation is done purely via <see cref="ICurrentPrincipalAccessor.Change"/>
/// with hand-built claims — permission value providers (<c>RolePermissionValueProvider</c>,
/// <c>UserPermissionValueProvider</c>) read roles/user id straight off the ambient
/// <see cref="ClaimsPrincipal"/>, so no real Identity sign-in is required to exercise
/// the real, database-backed permission checker.
/// </summary>
public abstract class CatalogAuthorizationTestBase : CatalogApplicationTestBase
{
    protected readonly ICategoryAppService CategoryAppService;
    protected readonly IToolAppService ToolAppService;
    protected readonly IToolInstanceAppService ToolInstanceAppService;

    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IIdentityUserRepository _identityUserRepository;

    protected CatalogAuthorizationTestBase()
    {
        CategoryAppService = GetRequiredService<ICategoryAppService>();
        ToolAppService = GetRequiredService<IToolAppService>();
        ToolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _identityUserRepository = GetRequiredService<IIdentityUserRepository>();
    }

    /// <summary>
    /// Synchronously swaps in the given principal for the scope of the returned
    /// disposable. <b>Must always be called directly by the method that owns the
    /// <c>using</c> block</b> — never returned across an <c>await</c> from a
    /// nested async helper. <see cref="ICurrentPrincipalAccessor.Change"/> mutates
    /// an <c>AsyncLocal</c>, and a Task continuation restores whatever
    /// <c>ExecutionContext</c> was captured when the <c>await</c> was issued —
    /// mutations made *inside* an awaited call never propagate back out to the
    /// caller once that call returns, only mutations made in the caller's own
    /// synchronous continuation do.
    /// </summary>
    protected IDisposable Impersonate(ClaimsPrincipal principal)
    {
        return _currentPrincipalAccessor.Change(principal);
    }

    protected IDisposable AsAnonymous()
    {
        return Impersonate(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    /// <summary>
    /// Uses the fixed <see cref="CatalogTestPrincipals.NoGrantsUserId"/> (003
    /// ripple, research R10) rather than a fresh <c>Guid.NewGuid()</c>: the
    /// enrolment gate resolves standing by identity user id via a real database
    /// lookup, so this principal needs a real, pre-seeded (Active, no Catalog
    /// grants) member record — see <c>CatalogTestDataSeedContributor</c>.
    /// </summary>
    protected IDisposable AsAuthenticatedUserWithNoGrants()
    {
        return Impersonate(BuildPrincipal(CatalogTestPrincipals.NoGrantsUserId, "no-grants-user"));
    }

    /// <summary>Same reasoning as <see cref="AsAuthenticatedUserWithNoGrants"/>, using the fixed <see cref="CatalogTestPrincipals.LibrarianUserId"/>.</summary>
    protected IDisposable AsLibrarian()
    {
        return Impersonate(BuildPrincipal(CatalogTestPrincipals.LibrarianUserId, "librarian-user", LibrarianRoleDataSeedContributor.RoleName));
    }

    /// <summary>
    /// A genuinely fresh identity, on purpose (003 ripple, research R10, T064):
    /// authenticated, but with no backing <c>Member</c> row anywhere — the "signed
    /// in but never enrolled" case the enrolment gate exists to refuse (FR-006),
    /// distinct from <see cref="AsAuthenticatedUserWithNoGrants"/>, which IS an
    /// enrolled member (just with no Catalog grants).
    /// </summary>
    protected IDisposable AsAuthenticatedNonMember()
    {
        return Impersonate(BuildPrincipal(Guid.NewGuid(), "never-enrolled-user"));
    }

    /// <summary>Looks up the seeded admin's real identity — does not impersonate; pass the result to <see cref="Impersonate"/> yourself (see that method's remarks on why).</summary>
    protected async Task<ClaimsPrincipal> BuildAdminPrincipalAsync()
    {
        var admin = await _identityUserRepository.FindByNormalizedUserNameAsync("ADMIN")
            ?? throw new InvalidOperationException("The seeded 'admin' user was not found — template seeding did not run.");

        return BuildPrincipal(admin.Id, admin.UserName, "admin");
    }

    private static ClaimsPrincipal BuildPrincipal(Guid userId, string userName, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, userId.ToString()),
            new(AbpClaimTypes.UserName, userName)
        };
        claims.AddRange(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    /// <summary>Creates a category/tool/instance as admin, so denied-operation tests have a real target to act on.</summary>
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

    protected static AddToolInstancePhotoDto CreatePhotoDto(string fileName = "front.jpg")
    {
        return new AddToolInstancePhotoDto
        {
            File = new RemoteStreamContent(new MemoryStream(new byte[1024]), fileName, "image/jpeg")
        };
    }

    /// <summary>Every Catalog management operation (see contracts/catalog-permissions.md), against the given seeded data.</summary>
    protected List<(string Name, Func<Task> Action)> BuildManagementOperations(
        CategoryDto category, ToolDto tool, ToolInstanceDto instance)
    {
        // Fake — DeletePhoto/SetPrimaryPhoto should be denied before the photo is
        // even looked up, so no photo needs to actually exist.
        var photoId = Guid.NewGuid();

        return new List<(string, Func<Task>)>
        {
            // Delete targets a real, existing aggregate: since none of these operations
            // should ever reach the method body, nothing is actually deleted, and using
            // a real id (rather than a random one) also proves a *valid* target is denied.
            ("Category.Create", () => CategoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() })),
            ("Category.Update", () => CategoryAppService.UpdateAsync(category.Id, new UpdateCategoryDto { Name = Guid.NewGuid().ToString(), ConcurrencyStamp = category.ConcurrencyStamp })),
            ("Category.Delete", () => CategoryAppService.DeleteAsync(category.Id)),
            ("Tool.Create", () => ToolAppService.CreateAsync(new CreateToolDto { Name = Guid.NewGuid().ToString(), CategoryId = category.Id })),
            ("Tool.Update", () => ToolAppService.UpdateAsync(tool.Id, new UpdateToolDto { Name = Guid.NewGuid().ToString(), CategoryId = category.Id, ConcurrencyStamp = tool.ConcurrencyStamp })),
            ("Tool.Delete", () => ToolAppService.DeleteAsync(tool.Id)),
            ("ToolInstance.Create", () => ToolInstanceAppService.CreateAsync(new CreateToolInstanceDto { ToolId = tool.Id, SerialNumber = Guid.NewGuid().ToString("N")[..8], Condition = ToolCondition.Good })),
            ("ToolInstance.Update", () => ToolInstanceAppService.UpdateAsync(instance.Id, new UpdateToolInstanceDto { SerialNumber = instance.SerialNumber, ConcurrencyStamp = instance.ConcurrencyStamp })),
            ("ToolInstance.ChangeCondition", () => ToolInstanceAppService.ChangeConditionAsync(instance.Id, new ChangeToolInstanceConditionDto { Condition = ToolCondition.Worn, ConcurrencyStamp = instance.ConcurrencyStamp })),
            ("ToolInstance.Retire", () => ToolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto { Reason = "test", ConcurrencyStamp = instance.ConcurrencyStamp })),
            ("ToolInstance.AddPhoto", () => ToolInstanceAppService.AddPhotoAsync(instance.Id, CreatePhotoDto())),
            ("ToolInstance.DeletePhoto", () => ToolInstanceAppService.DeletePhotoAsync(instance.Id, photoId)),
            ("ToolInstance.SetPrimaryPhoto", () => ToolInstanceAppService.SetPrimaryPhotoAsync(instance.Id, photoId))
        };
    }

    protected List<(string Name, Func<Task> Action)> BuildBrowseOperations(ToolDto tool, ToolInstanceDto instance)
    {
        return new List<(string, Func<Task>)>
        {
            ("Category.GetList", () => CategoryAppService.GetListAsync(new GetCategoryListInput())),
            ("Category.GetLookup", () => CategoryAppService.GetLookupAsync()),
            ("Tool.GetList", () => ToolAppService.GetListAsync(new GetToolListInput())),
            ("Tool.Get", () => ToolAppService.GetAsync(tool.Id)),
            ("ToolInstance.Get", () => ToolInstanceAppService.GetAsync(instance.Id)),
            ("ToolInstance.GetListByTool", () => ToolInstanceAppService.GetListByToolAsync(tool.Id)),
            ("ToolInstance.GetHistory", () => ToolInstanceAppService.GetHistoryAsync(instance.Id))
        };
    }

    /// <summary>
    /// Exercises every Catalog management operation exactly once, in a real
    /// dependency order (so business guards like CR-04/TR-04 never fire),
    /// under whatever principal is currently impersonated. Throws if any
    /// operation is rejected — success is "no exception".
    /// </summary>
    protected async Task RunFullManagementLifecycleAsync()
    {
        var category = await CategoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        category = await CategoryAppService.UpdateAsync(category.Id, new UpdateCategoryDto { Name = Guid.NewGuid().ToString(), ConcurrencyStamp = category.ConcurrencyStamp });

        var tool = await ToolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer", CategoryId = category.Id });
        tool = await ToolAppService.UpdateAsync(tool.Id, new UpdateToolDto { Name = "Rotary Hammer XL", CategoryId = category.Id, ConcurrencyStamp = tool.ConcurrencyStamp });

        var instance = await ToolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.Good
        });
        instance = await ToolInstanceAppService.UpdateAsync(instance.Id, new UpdateToolInstanceDto { SerialNumber = instance.SerialNumber, ConcurrencyStamp = instance.ConcurrencyStamp });
        instance = await ToolInstanceAppService.ChangeConditionAsync(instance.Id, new ChangeToolInstanceConditionDto { Condition = ToolCondition.Worn, ConcurrencyStamp = instance.ConcurrencyStamp });

        var photo = await ToolInstanceAppService.AddPhotoAsync(instance.Id, CreatePhotoDto());
        await ToolInstanceAppService.SetPrimaryPhotoAsync(instance.Id, photo.Id);
        await ToolInstanceAppService.DeletePhotoAsync(instance.Id, photo.Id);

        var freshInstance = await ToolInstanceAppService.GetAsync(instance.Id);
        await ToolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto { Reason = "worn out", ConcurrencyStamp = freshInstance.ConcurrencyStamp });

        // Delete is exercised against fresh, empty aggregates so CR-04/TR-04 never fire.
        var emptyTool = await ToolAppService.CreateAsync(new CreateToolDto { Name = Guid.NewGuid().ToString(), CategoryId = category.Id });
        await ToolAppService.DeleteAsync(emptyTool.Id);

        var emptyCategory = await CategoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        await CategoryAppService.DeleteAsync(emptyCategory.Id);
    }
}
