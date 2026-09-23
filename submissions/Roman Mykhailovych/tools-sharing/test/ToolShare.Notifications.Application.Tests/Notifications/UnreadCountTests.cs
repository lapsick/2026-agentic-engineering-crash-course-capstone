using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-013: the unread count always matches the caller's actual unread notifications.</summary>
public class UnreadCountTests : NotificationsAuthorizationTestBase
{
    [Fact]
    public async Task GetUnreadCountAsync_matches_the_actual_unread_count()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberRepository = GetRequiredService<IMemberRepository>();
        var member = await memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoGrantsUserId);

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = member!.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            RaisedAt = DateTime.UtcNow
        });

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = member.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.Overdue,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            RaisedAt = DateTime.UtcNow
        });

        using (Impersonate(BuildPrincipal(NotificationsTestPrincipals.NoGrantsUserId, "no-grants-user")))
        {
            var myNotificationsAppService = GetRequiredService<IMyNotificationsAppService>();
            var unreadCount = await myNotificationsAppService.GetUnreadCountAsync();

            unreadCount.ShouldBe(2);
        }
    }
}
