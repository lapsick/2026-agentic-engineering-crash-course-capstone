using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// The notification generators react to events raised by <i>someone else's</i>
/// action — seeding in <c>ToolShare.DbMigrator</c> (no principal at all), a
/// background worker, or an administrator acting on another member. Looking up
/// the recipient's standing and the tool is system work, not the caller's, so it
/// must not depend on the caller passing the enrolment gate. Regression for the
/// DbMigrator crash found by the E2E suite: seeding the admin's Member raised
/// <see cref="MemberStandingChangedEto"/>, the generator called
/// <c>IMemberStandingAppService.GetAsync</c> as the anonymous seeding context,
/// the gate refused, and the migrator exited.
/// </summary>
public class GeneratorsRunAsSystemTests : NotificationsAuthorizationTestBase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IMemberRepository _memberRepository;

    public GeneratorsRunAsSystemTests()
    {
        _notificationRepository = GetRequiredService<INotificationRepository>();
        _memberRepository = GetRequiredService<IMemberRepository>();
    }

    [Fact]
    public async Task A_standing_change_raised_with_no_principal_still_creates_its_notification()
    {
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.OtherMemberId);

        using (AsAnonymous())
        {
            await LocalEventBus.PublishAsync(RoleChangedTo(member!, CommunityRole.Librarian));
        }

        var notifications = await _notificationRepository.GetPagedListForMemberAsync(member!.Id, 0, 10);
        notifications.ShouldHaveSingleItem();
        notifications[0].Kind.ShouldBe(NotificationKind.StandingRoleChanged);
    }

    [Fact]
    public async Task A_standing_change_raised_by_a_non_member_caller_still_creates_its_notification()
    {
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.OtherMemberId);

        using (AsAuthenticatedNonMember())
        {
            await LocalEventBus.PublishAsync(RoleChangedTo(member!, CommunityRole.Librarian));
        }

        var notifications = await _notificationRepository.GetPagedListForMemberAsync(member!.Id, 0, 10);
        notifications.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_loan_notification_raised_with_no_principal_still_creates_its_notification()
    {
        var (_, tool, instance) = await SeedCatalogDataAsync();
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoGrantsUserId);

        using (AsAnonymous())
        {
            await LocalEventBus.PublishAsync(new LendingNotificationDueEto
            {
                LoanId = Guid.NewGuid(),
                MemberId = member!.Id,
                ToolInstanceId = instance.Id,
                Kind = LendingNotificationKind.Overdue,
                PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                RaisedAt = DateTime.UtcNow
            });
        }

        var notifications = await _notificationRepository.GetPagedListForMemberAsync(member!.Id, 0, 10);
        notifications.ShouldHaveSingleItem();
        notifications[0].DisplayText.ShouldContain(tool.Name);
    }

    private static MemberStandingChangedEto RoleChangedTo(Member member, CommunityRole newRole) => new()
    {
        MemberId = member.Id,
        IdentityUserId = member.IdentityUserId,
        Kind = MemberStandingChangeKind.RoleChanged,
        NewStatus = MembershipStatus.Active,
        PreviousRole = CommunityRole.Member,
        NewRole = newRole,
        NewRating = 100,
        CrossedLowRatingThreshold = false,
        ChangedAt = DateTime.UtcNow
    };
}
