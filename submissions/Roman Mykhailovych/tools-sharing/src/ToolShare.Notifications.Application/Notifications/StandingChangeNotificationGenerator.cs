using System.Threading.Tasks;
using ToolShare.Membership.Members;
using ToolShare.Notifications.RealTime;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// Reacts to Membership's <see cref="MemberStandingChangedEto"/> (FR-003,
/// FR-004) — the consumer 003 explicitly deferred to. Reuses the same
/// display/delivery-record construction path <see cref="LoanNotificationGenerator"/>
/// established (research R4).
/// </summary>
public class StandingChangeNotificationGenerator : ILocalEventHandler<MemberStandingChangedEto>, ITransientDependency
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IMemberStandingAppService _memberStandingAppService;
    private readonly NotificationEmailDispatcher _emailDispatcher;
    private readonly IMemberNotificationBroadcaster _broadcaster;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IClock _clock;

    public StandingChangeNotificationGenerator(
        INotificationRepository notificationRepository,
        IMemberStandingAppService memberStandingAppService,
        NotificationEmailDispatcher emailDispatcher,
        IMemberNotificationBroadcaster broadcaster,
        IUnitOfWorkManager unitOfWorkManager,
        IGuidGenerator guidGenerator,
        IClock clock)
    {
        _notificationRepository = notificationRepository;
        _memberStandingAppService = memberStandingAppService;
        _emailDispatcher = emailDispatcher;
        _broadcaster = broadcaster;
        _unitOfWorkManager = unitOfWorkManager;
        _guidGenerator = guidGenerator;
        _clock = clock;
    }

    public virtual async Task HandleEventAsync(MemberStandingChangedEto eventData)
    {
        var kind = NotificationKindResolver.TryMapStandingChange(
            eventData.Kind, eventData.PreviousStatus, eventData.NewStatus, eventData.CrossedLowRatingThreshold);

        if (kind is null)
        {
            return;
        }

        // NOTIF-01/FR-005: the dedup guard — Membership's own domain methods
        // raise this event exactly once per invocation; this is
        // defense-in-depth (research R3).
        if (await _notificationRepository.ExistsForStandingChangeAsync(eventData.MemberId, kind.Value, eventData.ChangedAt))
        {
            return;
        }

        var standing = await _memberStandingAppService.GetAsync(eventData.MemberId);
        var displayText = BuildDisplayText(kind.Value, eventData);
        var email = string.IsNullOrWhiteSpace(standing.Email) ? null : standing.Email;

        var notification = Notification.Create(
            _guidGenerator.Create(),
            eventData.MemberId,
            kind.Value,
            displayText,
            _clock.Now,
            originatingLoanId: null,
            originatingToolInstanceId: null,
            originatingChangedAt: eventData.ChangedAt,
            memberEmail: email);

        await _notificationRepository.InsertAsync(notification, autoSave: true);

        await _emailDispatcher.EnqueueIfPendingAsync(notification, email);

        // FR-001: surface the new notification to the member's live sessions, after the unit of work
        // commits so a subscriber's re-query sees the row (research R3). Raised post-commit and
        // exception-isolated, so it never blocks or fails the generation/persistence/email above.
        await MemberNotificationSignal.AfterCommitAsync(_unitOfWorkManager, _broadcaster, eventData.MemberId);
    }

    private static string BuildDisplayText(NotificationKind kind, MemberStandingChangedEto eventData)
    {
        return kind switch
        {
            NotificationKind.StandingDeactivated => "Your membership has been deactivated.",
            NotificationKind.StandingReactivated => "Your membership has been reactivated.",
            NotificationKind.StandingRoleChanged => $"Your community role has changed to {eventData.NewRole}.",
            NotificationKind.StandingLowRatingCrossed => $"Your reliability rating has changed to {eventData.NewRating}, affecting how much you can currently borrow.",
            _ => "Your membership standing has changed."
        };
    }
}
