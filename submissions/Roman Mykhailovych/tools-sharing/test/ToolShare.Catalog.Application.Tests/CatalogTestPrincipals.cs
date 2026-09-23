using System;

namespace ToolShare.Catalog;

/// <summary>
/// Fixed identity user ids for Catalog's synthetic test principals (003 ripple,
/// research R10, T063). These principals are hand-built <c>ClaimsPrincipal</c>s
/// (see <c>CatalogAuthorizationTestBase</c>/<c>CatalogApplicationTestBase</c>),
/// never real Identity sign-ins — but the enrolment gate resolves standing by
/// identity user id via a real database lookup, so each one needs a real,
/// pre-seeded <see cref="Membership.Members.Member"/> row. Fixed (rather than
/// <c>Guid.NewGuid()</c> per test run) so <c>CatalogTestDataSeedContributor</c>
/// can seed them once into the template database, mirroring how
/// <c>MembershipDataSeedContributor</c> seeds the bootstrap administrator.
/// </summary>
public static class CatalogTestPrincipals
{
    /// <summary>Backs <c>CatalogApplicationTestBase</c>'s default "test-runner" principal (role claim "admin"), used by every test suite that does not explicitly impersonate someone else.</summary>
    public static readonly Guid DefaultTestRunnerId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>Backs <c>CatalogAuthorizationTestBase.AsAuthenticatedUserWithNoGrants</c>.</summary>
    public static readonly Guid NoGrantsUserId = new("22222222-2222-2222-2222-222222222222");

    /// <summary>Backs <c>CatalogAuthorizationTestBase.AsLibrarian</c>.</summary>
    public static readonly Guid LibrarianUserId = new("33333333-3333-3333-3333-333333333333");
}
