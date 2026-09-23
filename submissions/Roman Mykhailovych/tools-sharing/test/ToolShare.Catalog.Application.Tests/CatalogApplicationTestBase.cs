using System;
using System.Collections.Generic;
using System.Security.Claims;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ToolShare.Catalog;

/// <summary>
/// Runs as the "admin" role by default (a synthetic principal, not the seeded
/// admin's real identity — permission checks only read role claims, so no
/// identity DB lookup is needed for permissions), so pre-Phase-5 test suites
/// written before authorization was enforced keep working unchanged. Phase 5's
/// own <c>CatalogAuthorizationTestBase</c> temporarily overrides this per-block
/// via nested <c>using (Impersonate(...))</c> scopes to exercise the boundary
/// itself.
///
/// Since 003 (research R10): the enrolment gate DOES need a real database
/// lookup (by identity user id) regardless of role claims, so this principal
/// uses the fixed <see cref="CatalogTestPrincipals.DefaultTestRunnerId"/> —
/// seeded an Active Administrator member by <c>CatalogTestDataSeedContributor</c>
/// — rather than a fresh <c>Guid.NewGuid()</c> per test, which would have no
/// backing member record.
/// </summary>
[Collection(CatalogApplicationTestConsts.CollectionDefinitionName)]
public abstract class CatalogApplicationTestBase : ToolShareTestBase<CatalogApplicationTestModule>
{
    protected CatalogApplicationTestBase()
    {
        var currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, CatalogTestPrincipals.DefaultTestRunnerId.ToString()),
            new(AbpClaimTypes.UserName, "test-runner"),
            new(AbpClaimTypes.Role, "admin")
        };

        // Intentionally not disposed: this principal should stay active for the
        // whole test (a fresh service provider — and AsyncLocal chain — is torn
        // down with it), and reverting would need a matching `using` scope this
        // constructor has no natural place to close.
        currentPrincipalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
    }
}
