using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// Closes the loop opened by <c>OverdueBlockTests</c> (T054) and
/// <c>EligibilityConsumesMembershipTests</c> (T101): an open loan the
/// OverdueMarkingWorker actually marks overdue — not manually flagged by the
/// test — blocks that member's next reservation attempt.
/// </summary>
public class OverdueBlocksNewReservationLiveTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly OverdueMarkingWorker _worker;

    public OverdueBlocksNewReservationLiveTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _worker = GetRequiredService<OverdueMarkingWorker>();
    }

    [Fact]
    public async Task A_loan_the_worker_marks_overdue_blocks_a_new_reservation_by_the_same_member()
    {
        var (_, _, overdueInstance) = await SeedCatalogDataAsync();
        var (_, _, otherInstance) = await SeedCatalogDataAsync();

        var pastStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

        using (AsMemberWithNoGrants())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = overdueInstance.Id,
                StartDate = pastStart,
                EndDate = pastEnd
            });

            using (AsLibrarian())
            {
                await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
            }
        }

        await _worker.ExecuteOnceAsync();

        var futureStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        using (AsMemberWithNoGrants())
        {
            var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = otherInstance.Id,
                StartDate = futureStart,
                EndDate = futureStart.AddDays(2)
            }));

            exception.Code.ShouldBe(LendingDomainErrorCodes.MemberHasOverdueLoan);
        }
    }
}
