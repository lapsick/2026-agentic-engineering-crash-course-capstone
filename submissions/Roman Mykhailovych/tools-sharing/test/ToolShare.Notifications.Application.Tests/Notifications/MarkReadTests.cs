using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-013/FR-014: marking one notification read changes only that one; another member's notification id is refused.</summary>
public class MarkReadTests : NotificationsAuthorizationTestBase
{
    [Fact]
    public async Task Marking_one_notification_read_changes_only_that_one_and_decrements_the_count()
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

        var notificationRepository = GetRequiredService<INotificationRepository>();
        var notifications = await notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10);
        var toMarkRead = notifications[0].Id;

        using (Impersonate(BuildPrincipal(NotificationsTestPrincipals.NoGrantsUserId, "no-grants-user")))
        {
            var myNotificationsAppService = GetRequiredService<IMyNotificationsAppService>();

            await myNotificationsAppService.MarkReadAsync(toMarkRead);

            (await myNotificationsAppService.GetUnreadCountAsync()).ShouldBe(1);

            var list = await myNotificationsAppService.GetListAsync(new GetMyNotificationsInput());
            list.Items.Single(n => n.Id == toMarkRead).ReadAt.ShouldNotBeNull();
            list.Items.Single(n => n.Id != toMarkRead).ReadAt.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Marking_another_members_notification_is_refused()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberRepository = GetRequiredService<IMemberRepository>();
        var owner = await memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoGrantsUserId);

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = owner!.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            RaisedAt = DateTime.UtcNow
        });

        var notificationRepository = GetRequiredService<INotificationRepository>();
        var ownersNotification = (await notificationRepository.GetPagedListForMemberAsync(owner.Id, 0, 10))[0];

        using (Impersonate(BuildPrincipal(NotificationsTestPrincipals.OtherMemberId, "other-member-user")))
        {
            var myNotificationsAppService = GetRequiredService<IMyNotificationsAppService>();

            await Should.ThrowAsync<EntityNotFoundException>(() => myNotificationsAppService.MarkReadAsync(ownersNotification.Id));
        }
    }
}
