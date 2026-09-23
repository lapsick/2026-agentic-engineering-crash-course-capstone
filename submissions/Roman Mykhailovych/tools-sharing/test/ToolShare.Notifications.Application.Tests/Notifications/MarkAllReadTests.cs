using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-013: marking all read clears the unread count; a subsequently generated notification is unread again.</summary>
public class MarkAllReadTests : NotificationsAuthorizationTestBase
{
    [Fact]
    public async Task MarkAllReadAsync_clears_the_unread_count_and_a_later_notification_is_unread_again()
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

            await myNotificationsAppService.MarkAllReadAsync();
            (await myNotificationsAppService.GetUnreadCountAsync()).ShouldBe(0);
        }

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = member.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            RaisedAt = DateTime.UtcNow
        });

        using (Impersonate(BuildPrincipal(NotificationsTestPrincipals.NoGrantsUserId, "no-grants-user")))
        {
            var myNotificationsAppService = GetRequiredService<IMyNotificationsAppService>();
            (await myNotificationsAppService.GetUnreadCountAsync()).ShouldBe(1);
        }
    }
}
