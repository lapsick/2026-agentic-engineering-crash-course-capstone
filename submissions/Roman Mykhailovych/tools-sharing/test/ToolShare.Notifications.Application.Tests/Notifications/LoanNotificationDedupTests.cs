using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-005/NOTIF-01: the same (LoanId, Kind) pair never produces more than one notification.</summary>
public class LoanNotificationDedupTests : NotificationsAuthorizationTestBase
{
    [Fact]
    public async Task Publishing_the_same_event_twice_creates_only_one_notification()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberRepository = GetRequiredService<IMemberRepository>();
        var member = await memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoGrantsUserId);

        var eto = new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = member!.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            RaisedAt = DateTime.UtcNow
        };

        await LocalEventBus.PublishAsync(eto);
        await LocalEventBus.PublishAsync(eto);

        var notificationRepository = GetRequiredService<INotificationRepository>();
        var count = await notificationRepository.GetCountForMemberAsync(member.Id);

        count.ShouldBe(1);
    }
}
