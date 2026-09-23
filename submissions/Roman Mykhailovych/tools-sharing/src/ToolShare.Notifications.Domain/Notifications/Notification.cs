using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// One surfaced fact for one member (data-model.md). Owns no domain fact of
/// its own beyond what its originating event already established — it only
/// reacts to <c>LendingNotificationDueEto</c> and <c>MemberStandingChangedEto</c>
/// (research R4).
/// </summary>
public class Notification : FullAuditedAggregateRoot<Guid>
{
    public virtual Guid MemberId { get; private set; }

    public virtual NotificationKind Kind { get; private set; }

    /// <summary>Set for <see cref="NotificationKind.LoanReturnReminder"/>/<see cref="NotificationKind.LoanOverdue"/>; null otherwise.</summary>
    public virtual Guid? OriginatingLoanId { get; private set; }

    public virtual Guid? OriginatingToolInstanceId { get; private set; }

    /// <summary>UTC; set for the three <c>Standing*</c> kinds — the source event's <c>ChangedAt</c>, doubling as half of the dedup key (`NOTIF-01`).</summary>
    public virtual DateTime? OriginatingChangedAt { get; private set; }

    public virtual string DisplayText { get; private set; } = default!;

    public virtual DateTime CreatedAt { get; private set; }

    /// <summary>Set once, on the member's first "mark read"; <c>null</c> = unread (`NOTIF-02`).</summary>
    public virtual DateTime? ReadAt { get; private set; }

    public virtual List<NotificationDeliveryRecord> DeliveryRecords { get; private set; } = new();

    protected Notification()
    {
    }

    private Notification(
        Guid id, Guid memberId, NotificationKind kind, string displayText, DateTime createdAt,
        Guid? originatingLoanId, Guid? originatingToolInstanceId, DateTime? originatingChangedAt)
        : base(id)
    {
        MemberId = memberId;
        Kind = kind;
        DisplayText = Check.Length(displayText, nameof(displayText), NotificationsDomainSharedConsts.DisplayTextMaxLength)!;
        CreatedAt = createdAt;
        OriginatingLoanId = originatingLoanId;
        OriginatingToolInstanceId = originatingToolInstanceId;
        OriginatingChangedAt = originatingChangedAt;
    }

    /// <summary>
    /// `NOTIF-04`/`NOTIF-05`: always yields a <c>Delivered</c> in-app record, plus
    /// a <c>Pending</c> (when <paramref name="memberEmail"/> is supplied) or
    /// <c>Skipped</c> (FR-009) email record.
    /// </summary>
    public static Notification Create(
        Guid id, Guid memberId, NotificationKind kind, string displayText, DateTime createdAt,
        Guid? originatingLoanId, Guid? originatingToolInstanceId, DateTime? originatingChangedAt,
        string? memberEmail)
    {
        var notification = new Notification(id, memberId, kind, displayText, createdAt, originatingLoanId, originatingToolInstanceId, originatingChangedAt);

        notification.DeliveryRecords.Add(NotificationDeliveryRecord.CreateInApp(Guid.NewGuid(), notification.Id, createdAt));

        notification.DeliveryRecords.Add(memberEmail is null
            ? NotificationDeliveryRecord.CreateSkippedEmail(Guid.NewGuid(), notification.Id, createdAt)
            : NotificationDeliveryRecord.CreatePendingEmail(Guid.NewGuid(), notification.Id, createdAt));

        return notification;
    }

    /// <summary>`NOTIF-02`: write-once — calling this again after the first is a no-op.</summary>
    public void MarkRead(DateTime at)
    {
        if (ReadAt.HasValue)
        {
            return;
        }

        ReadAt = at;
    }
}
