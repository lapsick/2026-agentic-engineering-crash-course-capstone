using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.Members;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Xunit;

namespace ToolShare.Lending.PublicContract;

/// <summary>
/// Proves Lending's one outward-facing Tier 1 surface (contracts/lending-events.md,
/// quickstart.md step 5): a probe <see cref="ILocalEventHandler{TEvent}"/>,
/// discovered purely by ABP's conventional registration, receives
/// <see cref="LendingNotificationDueEto"/> for both a return reminder and an
/// overdue notice — driven entirely through <see cref="ILoanAppService"/>,
/// <see cref="ReturnReminderWorker"/>, and <see cref="OverdueMarkingWorker"/>,
/// and a worsened return's Membership rating effect is confirmed the same
/// way. This file imports no <c>ToolShare.Lending.Domain…</c>/
/// <c>…EntityFrameworkCore…</c>, <c>ToolShare.Membership.Domain…</c>/
/// <c>…EntityFrameworkCore…</c>, or <c>ToolShare.Catalog.Domain…</c>/
/// <c>…EntityFrameworkCore…</c> namespace anywhere — the absent import is the
/// assertion (mirrors Membership's own <c>MemberStandingChangedEventTests</c>).
/// </summary>
public class LendingNotificationContractTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMemberAppService _memberAppService;
    private readonly ReturnReminderWorker _reminderWorker;
    private readonly OverdueMarkingWorker _overdueWorker;
    private readonly LendingNotificationEventCapture _capture;

    public LendingNotificationContractTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _memberAppService = GetRequiredService<IMemberAppService>();
        _reminderWorker = GetRequiredService<ReturnReminderWorker>();
        _overdueWorker = GetRequiredService<OverdueMarkingWorker>();
        _capture = GetRequiredService<LendingNotificationEventCapture>();
    }

    [Fact]
    public async Task Checkout_overdue_detection_and_a_worsened_return_raise_the_expected_events_and_rating_change()
    {
        // --- Reminder: a loan returning within the lead time ---
        var (_, _, reminderInstance) = await SeedCatalogDataAsync();
        var reminderStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var reminderEnd = DateOnly.FromDateTime(DateTime.UtcNow);

        LoanDto reminderLoan;
        using (AsLibrarian())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = reminderInstance.Id,
                StartDate = reminderStart,
                EndDate = reminderEnd
            });
            reminderLoan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
        }

        await _reminderWorker.ExecuteOnceAsync();

        var reminderEvents = _capture.Events.Where(e => e.LoanId == reminderLoan.Id).ToList();
        reminderEvents.Count.ShouldBe(1);
        reminderEvents[0].Kind.ShouldBe(LendingNotificationKind.ReturnReminder);
        reminderEvents[0].MemberId.ShouldBe(reminderLoan.MemberId);
        reminderEvents[0].ToolInstanceId.ShouldBe(reminderInstance.Id);

        // --- Overdue detection + a worsened return ---
        var (_, _, overdueInstance) = await SeedCatalogDataAsync();
        var overdueStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        var overdueEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

        LoanDto overdueLoan;
        Guid memberId;
        using (AsLibrarian())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = overdueInstance.Id,
                StartDate = overdueStart,
                EndDate = overdueEnd
            });
            memberId = reservation.MemberId;
            overdueLoan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
        }

        await _overdueWorker.ExecuteOnceAsync();

        var overdueEvents = _capture.Events.Where(e => e.LoanId == overdueLoan.Id).ToList();
        overdueEvents.Count.ShouldBe(1);
        overdueEvents[0].Kind.ShouldBe(LendingNotificationKind.Overdue);
        overdueEvents[0].MemberId.ShouldBe(memberId);

        var beforeReturn = await _memberAppService.GetAsync(memberId);

        LoanDto returned;
        using (AsLibrarian())
        {
            returned = await _loanAppService.ReturnAsync(overdueLoan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Worn });
        }

        var afterReturn = await _memberAppService.GetAsync(memberId);
        afterReturn.CurrentRating.ShouldBeLessThan(beforeReturn.CurrentRating);
        returned.IsOverdue.ShouldBeTrue();

        // A retried close is Lending's own idempotency guarantee — no second rating move.
        using (AsLibrarian())
        {
            await _loanAppService.ReturnAsync(overdueLoan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Good });
        }

        var afterRetriedClose = await _memberAppService.GetAsync(memberId);
        afterRetriedClose.CurrentRating.ShouldBe(afterReturn.CurrentRating);
    }
}

/// <summary>Per-test-instance capture buffer (a fresh DI container is built per test method).</summary>
public class LendingNotificationEventCapture : ISingletonDependency
{
    public List<LendingNotificationDueEto> Events { get; } = new();
}

/// <summary>
/// The probe handler itself: discovered purely by ABP's conventional
/// registration of this test assembly — no explicit wiring anywhere, mirroring
/// Membership's <c>MemberStandingChangedProbeHandler</c>.
/// </summary>
public class LendingNotificationProbeHandler : ILocalEventHandler<LendingNotificationDueEto>, ITransientDependency
{
    private readonly LendingNotificationEventCapture _capture;

    public LendingNotificationProbeHandler(LendingNotificationEventCapture capture)
    {
        _capture = capture;
    }

    public Task HandleEventAsync(LendingNotificationDueEto eventData)
    {
        _capture.Events.Add(eventData);
        return Task.CompletedTask;
    }
}
