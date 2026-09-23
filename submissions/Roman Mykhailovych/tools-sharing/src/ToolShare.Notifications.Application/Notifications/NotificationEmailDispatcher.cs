using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// Shared by both generators (research R4) — enqueues the background job
/// (research R2) for a just-created notification's <c>Pending</c> email
/// delivery record, if it has one. A no-op when the member had no email on
/// file (FR-009), since <see cref="Notification.Create"/> then produced a
/// <c>Skipped</c> record instead of a <c>Pending</c> one.
/// </summary>
public class NotificationEmailDispatcher : ITransientDependency
{
    private readonly IBackgroundJobManager _backgroundJobManager;

    public NotificationEmailDispatcher(IBackgroundJobManager backgroundJobManager)
    {
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task EnqueueIfPendingAsync(Notification notification, string? memberEmail)
    {
        if (memberEmail is null)
        {
            return;
        }

        var emailRecord = notification.DeliveryRecords
            .FirstOrDefault(r => r.Channel == DeliveryChannel.Email && r.Status == DeliveryStatus.Pending);

        if (emailRecord is null)
        {
            return;
        }

        var (subject, body) = NotificationEmailContentFactory.Build(notification.Kind, notification.DisplayText);

        await _backgroundJobManager.EnqueueAsync(new SendEmailNotificationJobArgs
        {
            NotificationId = notification.Id,
            DeliveryRecordId = emailRecord.Id,
            ToEmail = memberEmail,
            Subject = subject,
            Body = body
        });
    }
}
