using System.Collections.Generic;
using System.Security.Claims;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ToolShare.Notifications;

/// <summary>
/// Runs as the "admin" role by default (a synthetic principal), mirroring
/// <c>LendingApplicationTestBase</c>. Uses the fixed
/// <see cref="NotificationsTestPrincipals.DefaultTestRunnerId"/> — seeded an
/// Active Administrator member by <see cref="NotificationsTestDataSeedContributor"/>.
/// </summary>
[Collection(NotificationsApplicationTestConsts.CollectionDefinitionName)]
public abstract class NotificationsApplicationTestBase : ToolShareTestBase<NotificationsApplicationTestModule>
{
    protected NotificationsApplicationTestBase()
    {
        var currentPrincipalAccessor = GetRequiredService<Volo.Abp.Security.Claims.ICurrentPrincipalAccessor>();
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, NotificationsTestPrincipals.DefaultTestRunnerId.ToString()),
            new(AbpClaimTypes.UserName, "test-runner"),
            new(AbpClaimTypes.Role, "admin")
        };

        // Intentionally not disposed — see CatalogApplicationTestBase for why.
        currentPrincipalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
    }
}
