using System;

namespace ToolShare.Membership;

/// <summary>
/// Fixed identity user ids for Membership's own synthetic test principals —
/// same reasoning as <c>ToolShare.Catalog.CatalogTestPrincipals</c> (research
/// R10): the enrolment gate resolves standing by identity user id via a real
/// database lookup, so every hand-built <c>ClaimsPrincipal</c> needs a real,
/// pre-seeded <see cref="Members.Member"/> row. Seeded once into the template
/// database by <see cref="MembershipTestDataSeedContributor"/>.
/// </summary>
public static class MembershipTestPrincipals
{
    /// <summary>Backs <c>MembershipApplicationTestBase</c>'s default "test-runner" principal (role claim "admin"), an Active Administrator.</summary>
    public static readonly Guid DefaultTestRunnerId = new("44444444-4444-4444-4444-444444444444");

    /// <summary>An Active member with no Membership grants and no elevated ABP role — for SC-003-style denial assertions.</summary>
    public static readonly Guid NoGrantsMemberId = new("55555555-5555-5555-5555-555555555555");
}
