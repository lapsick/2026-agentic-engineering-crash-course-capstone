using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>FR-023/FR-025: marks IsOverdue, raises the overdue notice once (idempotent via Loan.OverdueNoticeSentAt), and the flag survives an eventual return.</summary>
public class OverdueMarkingWorkerTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly ILoanRepository _loanRepository;
    private readonly OverdueMarkingWorker _worker;

    public OverdueMarkingWorkerTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _loanRepository = GetRequiredService<ILoanRepository>();
        _worker = GetRequiredService<OverdueMarkingWorker>();
    }

    [Fact]
    public async Task A_loan_past_its_planned_return_date_is_marked_overdue_exactly_once()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        var end = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

        LoanDto loan;
        using (AsLibrarian())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = end
            });

            loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
        }

        await _worker.ExecuteOnceAsync();

        var marked = await _loanRepository.GetAsync(loan.Id);
        marked.IsOverdue.ShouldBeTrue();
        marked.OverdueNoticeSentAt.ShouldNotBeNull();
        var firstNoticeTime = marked.OverdueNoticeSentAt!.Value;

        await _worker.ExecuteOnceAsync();

        var stillMarked = await _loanRepository.GetAsync(loan.Id);
        stillMarked.OverdueNoticeSentAt.ShouldBe(firstNoticeTime);
    }

    [Fact]
    public async Task The_overdue_flag_survives_an_eventual_return()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        var end = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

        LoanDto loan;
        using (AsLibrarian())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = end
            });

            loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
        }

        await _worker.ExecuteOnceAsync();

        LoanDto returned;
        using (AsLibrarian())
        {
            returned = await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Good });
        }

        returned.IsOverdue.ShouldBeTrue();
    }

    [Fact]
    public async Task A_loan_not_yet_due_is_left_untouched()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var end = start.AddDays(5);

        LoanDto loan;
        using (AsLibrarian())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = end
            });

            loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
        }

        await _worker.ExecuteOnceAsync();

        var untouched = await _loanRepository.GetAsync(loan.Id);
        untouched.IsOverdue.ShouldBeFalse();
        untouched.OverdueNoticeSentAt.ShouldBeNull();
    }
}
