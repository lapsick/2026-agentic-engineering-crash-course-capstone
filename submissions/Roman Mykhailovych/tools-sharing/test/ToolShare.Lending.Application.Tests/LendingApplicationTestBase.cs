using System;
using System.Collections.Generic;
using System.Security.Claims;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ToolShare.Lending;

/// <summary>
/// Runs as the "admin" role by default (a synthetic principal, not the seeded
/// admin's real identity), mirroring <c>CatalogApplicationTestBase</c>/
/// <c>MembershipApplicationTestBase</c>. Uses the fixed
/// <see cref="LendingTestPrincipals.DefaultTestRunnerId"/> — seeded an Active
/// Administrator member by <see cref="LendingTestDataSeedContributor"/> —
/// since the enrolment gate resolves standing by identity user id via a real
/// database lookup regardless of role claims.
/// </summary>
[Collection(LendingApplicationTestConsts.CollectionDefinitionName)]
public abstract class LendingApplicationTestBase : ToolShareTestBase<LendingApplicationTestModule>
{
    protected LendingApplicationTestBase()
    {
        var currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, LendingTestPrincipals.DefaultTestRunnerId.ToString()),
            new(AbpClaimTypes.UserName, "test-runner"),
            new(AbpClaimTypes.Role, "admin")
        };

        // Intentionally not disposed — see CatalogApplicationTestBase for why.
        currentPrincipalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
    }
}
