using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-010: a mail-transport failure never blocks or reverses the in-app delivery; the email record becomes Failed, never stuck Pending.</summary>
public class EmailFailureIsolationTests : NotificationsAuthorizationTestBase
{
    [Fact]
    public async Task A_failing_mail_transport_leaves_the_inapp_record_delivered_and_marks_the_email_record_failed()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberRepository = GetRequiredService<IMemberRepository>();
        var member = await memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoGrantsUserId);

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = member!.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.Overdue,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            RaisedAt = DateTime.UtcNow
        });

        var notificationRepository = GetRequiredService<INotificationRepository>();
        var notifications = await notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10);
        var notification = notifications.ShouldHaveSingleItem();
        var emailRecord = notification.DeliveryRecords.Single(r => r.Channel == DeliveryChannel.Email);

        EmailSender.ThrowOnSend = true;

        var job = GetRequiredService<SendEmailNotificationJob>();
        var (subject, body) = NotificationEmailContentFactory.Build(notification.Kind, notification.DisplayText);
        await job.ExecuteAsync(new SendEmailNotificationJobArgs
        {
            NotificationId = notification.Id,
            DeliveryRecordId = emailRecord.Id,
            ToEmail = $"{NotificationsTestPrincipals.NoGrantsUserId:N}@notifications-tests.local",
            Subject = subject,
            Body = body
        });

        var reloaded = await notificationRepository.FindWithDetailsAsync(notification.Id);

        var reloadedInApp = reloaded!.DeliveryRecords.Single(r => r.Channel == DeliveryChannel.InApp);
        reloadedInApp.Status.ShouldBe(DeliveryStatus.Delivered);

        var reloadedEmail = reloaded.DeliveryRecords.Single(r => r.Channel == DeliveryChannel.Email);
        reloadedEmail.Status.ShouldBe(DeliveryStatus.Failed);
        reloadedEmail.FailureDetail.ShouldNotBeNullOrEmpty();
        reloadedEmail.CompletedAt.ShouldNotBeNull();
    }
}
