using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// Runs off the request path (research R2/FR-010): actually sending an email
/// never blocks or fails the loan/member transaction that generated the
/// notification. Idempotent-safe — if the targeted delivery record is no
/// longer <c>Pending</c> (already resolved, or the notification is somehow
/// gone), it does nothing.
/// </summary>
[Serializable]
public class SendEmailNotificationJobArgs
{
    public Guid NotificationId { get; set; }

    public Guid DeliveryRecordId { get; set; }

    public string ToEmail { get; set; } = default!;

    public string Subject { get; set; } = default!;

    public string Body { get; set; } = default!;
}

public class SendEmailNotificationJob : AsyncBackgroundJob<SendEmailNotificationJobArgs>, ITransientDependency
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public SendEmailNotificationJob(INotificationRepository notificationRepository, IEmailSender emailSender, IClock clock, IUnitOfWorkManager unitOfWorkManager)
    {
        _notificationRepository = notificationRepository;
        _emailSender = emailSender;
        _clock = clock;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public override async Task ExecuteAsync(SendEmailNotificationJobArgs args)
    {
        // Background jobs run off the request path (research R2) and are not
        // wrapped by the same per-call unit-of-work interception an app
        // service call gets — an explicit, new, transactional UOW here
        // guarantees the delivery-record update actually commits regardless
        // of how ExecuteAsync is invoked (the real queue processor, or
        // directly, as tests do).
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

        var notification = await _notificationRepository.FindWithDetailsAsync(args.NotificationId);
        var record = notification?.DeliveryRecords.FirstOrDefault(r => r.Id == args.DeliveryRecordId);

        if (notification is null || record is null || record.Status != DeliveryStatus.Pending)
        {
            return;
        }

        try
        {
            await _emailSender.SendAsync(args.ToEmail, args.Subject, args.Body, false);
            record.MarkDelivered(_clock.Now);
        }
        catch (Exception ex)
        {
            record.MarkFailed(_clock.Now, ex.Message.Length > NotificationsDomainSharedConsts.FailureDetailMaxLength
                ? ex.Message[..NotificationsDomainSharedConsts.FailureDetailMaxLength]
                : ex.Message);
        }

        await _notificationRepository.UpdateAsync(notification, autoSave: true);

        await uow.CompleteAsync();
    }
}
