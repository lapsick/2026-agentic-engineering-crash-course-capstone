using System.Threading.Tasks;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using ToolShare.Notifications.RealTime;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// Reacts to Lending's <see cref="LendingNotificationDueEto"/> (FR-001, FR-002).
/// Same shape as <c>MemberStandingCacheInvalidator</c> (003) — a transient
/// <see cref="ILocalEventHandler{TEventData}"/> — reused for a second,
/// unrelated purpose (research R4).
/// </summary>
public class LoanNotificationGenerator : ILocalEventHandler<LendingNotificationDueEto>, ITransientDependency
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;
    private readonly IMemberStandingAppService _memberStandingAppService;
    private readonly NotificationEmailDispatcher _emailDispatcher;
    private readonly IMemberNotificationBroadcaster _broadcaster;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IClock _clock;

    public LoanNotificationGenerator(
        INotificationRepository notificationRepository,
        IToolInstanceLookupAppService toolInstanceLookupAppService,
        IMemberStandingAppService memberStandingAppService,
        NotificationEmailDispatcher emailDispatcher,
        IMemberNotificationBroadcaster broadcaster,
        IUnitOfWorkManager unitOfWorkManager,
        IGuidGenerator guidGenerator,
        IClock clock)
    {
        _notificationRepository = notificationRepository;
        _toolInstanceLookupAppService = toolInstanceLookupAppService;
        _memberStandingAppService = memberStandingAppService;
        _emailDispatcher = emailDispatcher;
        _broadcaster = broadcaster;
        _unitOfWorkManager = unitOfWorkManager;
        _guidGenerator = guidGenerator;
        _clock = clock;
    }

    public virtual async Task HandleEventAsync(LendingNotificationDueEto eventData)
    {
        var kind = NotificationKindResolver.TryMapLendingKind(eventData.Kind);
        if (kind is null)
        {
            return;
        }

        // NOTIF-01/FR-005: the dedup guard — Lending's own ReminderSentAt/
        // OverdueNoticeSentAt already make this at-most-once at the source;
        // this is defense-in-depth (research R3).
        if (await _notificationRepository.ExistsForLoanAsync(eventData.MemberId, kind.Value, eventData.LoanId))
        {
            return;
        }

        var toolInstance = await _toolInstanceLookupAppService.FindAsync(eventData.ToolInstanceId);
        var standing = await _memberStandingAppService.GetAsync(eventData.MemberId);

        var displayText = BuildDisplayText(kind.Value, toolInstance?.ToolName, eventData.PlannedReturnDate);
        var email = string.IsNullOrWhiteSpace(standing.Email) ? null : standing.Email;

        var notification = Notification.Create(
            _guidGenerator.Create(),
            eventData.MemberId,
            kind.Value,
            displayText,
            _clock.Now,
            originatingLoanId: eventData.LoanId,
            originatingToolInstanceId: eventData.ToolInstanceId,
            originatingChangedAt: null,
            memberEmail: email);

        await _notificationRepository.InsertAsync(notification, autoSave: true);

        await _emailDispatcher.EnqueueIfPendingAsync(notification, email);

        // FR-001: surface the new notification to the member's live sessions, after the unit of work
        // commits so a subscriber's re-query sees the row (research R3). Raised post-commit and
        // exception-isolated, so it never blocks or fails the generation/persistence/email above.
        await MemberNotificationSignal.AfterCommitAsync(_unitOfWorkManager, _broadcaster, eventData.MemberId);
    }

    private static string BuildDisplayText(NotificationKind kind, string? toolName, System.DateOnly plannedReturnDate)
    {
        var name = string.IsNullOrWhiteSpace(toolName) ? "a tool" : toolName;

        return kind switch
        {
            NotificationKind.LoanReturnReminder => $"Your loan for '{name}' is due back on {plannedReturnDate:yyyy-MM-dd}.",
            NotificationKind.LoanOverdue => $"Your loan for '{name}' is overdue — it was due back on {plannedReturnDate:yyyy-MM-dd}.",
            _ => $"An update is available about your loan for '{name}'."
        };
    }
}
