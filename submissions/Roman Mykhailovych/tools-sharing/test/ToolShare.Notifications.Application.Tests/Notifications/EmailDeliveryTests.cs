using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// FR-007, FR-008: a member with an email on file gets a `Pending` → `Delivered`
/// email delivery record and the mail sender receives exactly one call.
/// The background job (research R2) is invoked directly rather than through
/// the real queued pipeline — deterministic, and exercises exactly the same
/// production code (<see cref="SendEmailNotificationJob.ExecuteAsync"/>) a
/// real worker would run.
/// </summary>
public class EmailDeliveryTests : NotificationsAuthorizationTestBase
{
    [Fact]
    public async Task Member_with_email_gets_a_delivered_email_record_and_one_sent_mail()
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

        var notificationRepository = GetRequiredService<INotificationRepository>();
        var notifications = await notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10);
        var notification = notifications.ShouldHaveSingleItem();

        var emailRecord = notification.DeliveryRecords.Single(r => r.Channel == DeliveryChannel.Email);
        emailRecord.Status.ShouldBe(DeliveryStatus.Pending);

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
        var reloadedEmailRecord = reloaded!.DeliveryRecords.Single(r => r.Channel == DeliveryChannel.Email);
        reloadedEmailRecord.Status.ShouldBe(DeliveryStatus.Delivered);
        reloadedEmailRecord.CompletedAt.ShouldNotBeNull();

        EmailSender.SentMails.Count.ShouldBe(1);
        EmailSender.SentMails.Single().To.Single().Address.ShouldBe($"{NotificationsTestPrincipals.NoGrantsUserId:N}@notifications-tests.local");
    }
}
