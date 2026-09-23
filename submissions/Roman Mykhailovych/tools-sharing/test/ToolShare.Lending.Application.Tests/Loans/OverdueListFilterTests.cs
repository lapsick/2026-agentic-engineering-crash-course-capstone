using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Reservations;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>FR-024: GetLoanListInput.OnlyOverdue distinguishes overdue loans in ILoanAppService.GetListAsync.</summary>
public class OverdueListFilterTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly OverdueMarkingWorker _worker;

    public OverdueListFilterTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _worker = GetRequiredService<OverdueMarkingWorker>();
    }

    [Fact]
    public async Task OnlyOverdue_returns_just_the_overdue_loan()
    {
        var (_, _, overdueInstance) = await SeedCatalogDataAsync();
        var (_, _, currentInstance) = await SeedCatalogDataAsync();

        var pastStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);
        var futureStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        LoanDto overdueLoan;
        LoanDto currentLoan;
        using (AsLibrarian())
        {
            var overdueReservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = overdueInstance.Id,
                StartDate = pastStart,
                EndDate = pastEnd
            });
            overdueLoan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = overdueReservation.Id });

            var currentReservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = currentInstance.Id,
                StartDate = futureStart,
                EndDate = futureStart.AddDays(5)
            });
            currentLoan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = currentReservation.Id });
        }

        await _worker.ExecuteOnceAsync();

        using (AsLibrarian())
        {
            var overdueOnly = await _loanAppService.GetListAsync(new GetLoanListInput { OnlyOverdue = true, MaxResultCount = 100 });
            overdueOnly.Items.ShouldContain(l => l.Id == overdueLoan.Id);
            overdueOnly.Items.ShouldNotContain(l => l.Id == currentLoan.Id);

            var all = await _loanAppService.GetListAsync(new GetLoanListInput { OnlyOverdue = false, MaxResultCount = 100 });
            all.Items.ShouldContain(l => l.Id == overdueLoan.Id);
            all.Items.ShouldContain(l => l.Id == currentLoan.Id);
        }
    }
}
