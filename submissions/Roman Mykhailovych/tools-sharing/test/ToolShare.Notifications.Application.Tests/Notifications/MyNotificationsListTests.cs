using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-012, FR-014: a member's own notifications, most recent first, never another member's.</summary>
public class MyNotificationsListTests : NotificationsAuthorizationTestBase
{
    [Fact]
    public async Task GetListAsync_returns_only_the_callers_own_notifications_most_recent_first()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberRepository = GetRequiredService<IMemberRepository>();
        var self = await memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoGrantsUserId);
        var other = await memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.OtherMemberId);

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = self!.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            RaisedAt = DateTime.UtcNow
        });

        // A short delay guarantees this second notification's CreatedAt is
        // measurably later than the first's, so "most recent first" ordering
        // below is deterministic rather than depending on clock resolution.
        await Task.Delay(10);

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = self.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.Overdue,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            RaisedAt = DateTime.UtcNow
        });

        // A notification for a different member — must never appear in self's list.
        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = other!.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            RaisedAt = DateTime.UtcNow
        });

        using (Impersonate(BuildPrincipal(NotificationsTestPrincipals.NoGrantsUserId, "no-grants-user")))
        {
            var myNotificationsAppService = GetRequiredService<IMyNotificationsAppService>();
            var result = await myNotificationsAppService.GetListAsync(new GetMyNotificationsInput());

            result.TotalCount.ShouldBe(2);
            result.Items.Count.ShouldBe(2);
            result.Items[0].Kind.ShouldBe(NotificationKind.LoanOverdue); // most recent
            result.Items[1].Kind.ShouldBe(NotificationKind.LoanReturnReminder);
        }
    }
}
