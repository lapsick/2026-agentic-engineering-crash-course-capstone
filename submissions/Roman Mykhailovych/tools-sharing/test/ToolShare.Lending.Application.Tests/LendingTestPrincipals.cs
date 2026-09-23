using System;

namespace ToolShare.Lending;

/// <summary>
/// Fixed identity user ids for Lending's own synthetic test principals —
/// same reasoning as <c>ToolShare.Catalog.CatalogTestPrincipals</c> and
/// <c>ToolShare.Membership.MembershipTestPrincipals</c>: the enrolment gate
/// resolves standing by identity user id via a real database lookup, so every
/// hand-built <c>ClaimsPrincipal</c> needs a real, pre-seeded
/// <see cref="Membership.Members.Member"/> row. Seeded once into the template
/// database by <see cref="LendingTestDataSeedContributor"/>.
/// </summary>
public static class LendingTestPrincipals
{
    /// <summary>Backs <c>LendingApplicationTestBase</c>'s default "test-runner" principal (role claim "admin"), an Active Administrator.</summary>
    public static readonly Guid DefaultTestRunnerId = new("66666666-6666-6666-6666-666666666666");

    /// <summary>An Active member with no Lending grants and no elevated ABP role — for permission-denial assertions.</summary>
    public static readonly Guid NoGrantsUserId = new("77777777-7777-7777-7777-777777777777");

    /// <summary>Backs <c>LendingAuthorizationTestBase.AsLibrarian</c>.</summary>
    public static readonly Guid LibrarianUserId = new("88888888-8888-8888-8888-888888888888");
}
