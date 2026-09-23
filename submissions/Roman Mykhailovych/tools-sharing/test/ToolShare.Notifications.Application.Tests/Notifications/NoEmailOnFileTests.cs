using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-009: a member with no email on file still gets the in-app notification; no email is attempted.</summary>
public class NoEmailOnFileTests : NotificationsAuthorizationTestBase
{
    [Fact]
    public async Task Member_with_no_email_gets_a_skipped_email_record_and_the_inapp_record_is_unaffected()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberRepository = GetRequiredService<IMemberRepository>();
        var member = await memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoEmailUserId);

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = member!.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            RaisedAt = DateTime.UtcNow
        });

        var notificationRepository = GetRequiredService<INotificationRepository>();
        var notifications = await notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10);
        var notification = notifications.ShouldHaveSingleItem();

        var emailRecord = notification.DeliveryRecords.Single(r => r.Channel == DeliveryChannel.Email);
        emailRecord.Status.ShouldBe(DeliveryStatus.Skipped);

        var inAppRecord = notification.DeliveryRecords.Single(r => r.Channel == DeliveryChannel.InApp);
        inAppRecord.Status.ShouldBe(DeliveryStatus.Delivered);

        EmailSender.SentMails.ShouldBeEmpty();
    }
}
