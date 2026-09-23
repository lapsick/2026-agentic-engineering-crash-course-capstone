using System;
using System.Collections.Generic;
using System.Security.Claims;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ToolShare.Membership;

/// <summary>
/// Runs as the "admin" role by default (a synthetic principal, not the seeded
/// admin's real identity), mirroring <c>CatalogApplicationTestBase</c>. Story
/// tests that need to exercise the boundary itself (the enrolment gate,
/// self-only access, roster authorization) override the principal per-block.
///
/// Since US1 (T044+): the enrolment gate needs a real database lookup (by
/// identity user id) regardless of role claims, so this principal uses the
/// fixed <see cref="MembershipTestPrincipals.DefaultTestRunnerId"/> — seeded an
/// Active Administrator member by <see cref="MembershipTestDataSeedContributor"/> —
/// rather than a fresh <c>Guid.NewGuid()</c> per test, which would have no
/// backing member record.
/// </summary>
[Collection(MembershipApplicationTestConsts.CollectionDefinitionName)]
public abstract class MembershipApplicationTestBase : ToolShareTestBase<MembershipApplicationTestModule>
{
    protected MembershipApplicationTestBase()
    {
        var currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, MembershipTestPrincipals.DefaultTestRunnerId.ToString()),
            new(AbpClaimTypes.UserName, "test-runner"),
            new(AbpClaimTypes.Role, "admin")
        };

        // Intentionally not disposed — see CatalogApplicationTestBase for why.
        currentPrincipalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
    }
}
