using System;

namespace ToolShare.Notifications;

/// <summary>
/// Fixed identity user ids for Notifications' own synthetic test principals —
/// same reasoning as <c>ToolShare.Lending.LendingTestPrincipals</c>: the
/// enrolment gate resolves standing by identity user id via a real database
/// lookup, so every hand-built <c>ClaimsPrincipal</c> needs a real, pre-seeded
/// member row. Seeded once into the template database by
/// <see cref="NotificationsTestDataSeedContributor"/>.
/// </summary>
public static class NotificationsTestPrincipals
{
    /// <summary>Backs <c>NotificationsApplicationTestBase</c>'s default "test-runner" principal (role claim "admin"), an Active Administrator.</summary>
    public static readonly Guid DefaultTestRunnerId = new("99999999-9999-9999-9999-999999999991");

    /// <summary>An Active member with no elevated ABP role — for self-service/permission-denial assertions.</summary>
    public static readonly Guid NoGrantsUserId = new("99999999-9999-9999-9999-999999999992");

    /// <summary>A second, distinct member — used to prove one member never sees another's notifications (FR-014).</summary>
    public static readonly Guid OtherMemberId = new("99999999-9999-9999-9999-999999999993");

    /// <summary>
    /// Seeded with an ordinary email like any other member — Membership's own
    /// domain layer (<c>Member.SetEmail</c>) refuses a blank one even when
    /// seeding bypasses <c>EnrolMemberDto</c>'s validation, so "no email on
    /// file" (FR-009) cannot be produced through real enrolment at all.
    /// <see cref="EmailMaskingMemberStandingAppService"/> (test-only) strips
    /// this identity's email specifically when Notifications asks Membership
    /// for standing, isolating the condition for US2's tests.
    /// </summary>
    public static readonly Guid NoEmailUserId = new("99999999-9999-9999-9999-999999999994");
}
