using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// Child entity of <see cref="Notification"/> (data-model.md `DR-01`, `DR-02`) —
/// one row per channel attempted, including the in-app channel, whose
/// "delivery" is definitionally instant (plan.md Complexity Tracking). No
/// independent repository; always loaded/written together with its parent.
/// </summary>
public class NotificationDeliveryRecord : Entity<Guid>
{
    public virtual Guid NotificationId { get; private set; }

    public virtual DeliveryChannel Channel { get; private set; }

    public virtual DeliveryStatus Status { get; private set; }

    public virtual DateTime AttemptedAt { get; private set; }

    /// <summary>Set once, when a <c>Pending</c> record resolves — <c>null</c> for already-terminal <c>InApp</c>/<c>Skipped</c> rows.</summary>
    public virtual DateTime? CompletedAt { get; private set; }

    public virtual string? FailureDetail { get; private set; }

    protected NotificationDeliveryRecord()
    {
    }

    private NotificationDeliveryRecord(Guid id, Guid notificationId, DeliveryChannel channel, DeliveryStatus status, DateTime attemptedAt, DateTime? completedAt)
        : base(id)
    {
        NotificationId = notificationId;
        Channel = channel;
        Status = status;
        AttemptedAt = attemptedAt;
        CompletedAt = completedAt;
    }

    /// <summary>NOTIF-04: the in-app channel's "delivery" is the row's existence — already terminal.</summary>
    public static NotificationDeliveryRecord CreateInApp(Guid id, Guid notificationId, DateTime attemptedAt)
        => new(id, notificationId, DeliveryChannel.InApp, DeliveryStatus.Delivered, attemptedAt, attemptedAt);

    /// <summary>NOTIF-05: the member has an email address on file — enqueued, awaiting the background job.</summary>
    public static NotificationDeliveryRecord CreatePendingEmail(Guid id, Guid notificationId, DateTime attemptedAt)
        => new(id, notificationId, DeliveryChannel.Email, DeliveryStatus.Pending, attemptedAt, null);

    /// <summary>NOTIF-05/FR-009: the member has no email address on file — not attempted by design, already terminal.</summary>
    public static NotificationDeliveryRecord CreateSkippedEmail(Guid id, Guid notificationId, DateTime attemptedAt)
        => new(id, notificationId, DeliveryChannel.Email, DeliveryStatus.Skipped, attemptedAt, attemptedAt);

    /// <summary>DR-01: <c>Pending</c> → <c>Delivered</c>, exactly once.</summary>
    public void MarkDelivered(DateTime at)
    {
        if (Status != DeliveryStatus.Pending)
        {
            throw new BusinessException(NotificationsDomainErrorCodes.InvalidStateTransition);
        }

        Status = DeliveryStatus.Delivered;
        CompletedAt = at;
    }

    /// <summary>DR-01: <c>Pending</c> → <c>Failed</c>, exactly once.</summary>
    public void MarkFailed(DateTime at, string failureDetail)
    {
        if (Status != DeliveryStatus.Pending)
        {
            throw new BusinessException(NotificationsDomainErrorCodes.InvalidStateTransition);
        }

        Status = DeliveryStatus.Failed;
        CompletedAt = at;
        FailureDetail = Check.Length(failureDetail, nameof(failureDetail), NotificationsDomainSharedConsts.FailureDetailMaxLength);
    }
}
