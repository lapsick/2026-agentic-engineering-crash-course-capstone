using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Loans;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>RES-08, MAINT-03: a worsened return cancels every future Active reservation for the same instance, leaving the already-CheckedOut one untouched.</summary>
public class ReservationCancellationCascadeTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IReservationRepository _reservationRepository;

    public ReservationCancellationCascadeTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _reservationRepository = GetRequiredService<IReservationRepository>();
    }

    [Fact]
    public async Task A_worsened_return_cancels_future_active_reservations_for_the_same_instance()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start1 = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var end1 = start1.AddDays(2);
        var start2 = end1.AddDays(2);
        var end2 = start2.AddDays(2);

        ReservationDto reservation1;
        using (AsMemberWithNoGrants())
        {
            reservation1 = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start1,
                EndDate = end1
            });
        }

        ReservationDto reservation2;
        using (AsLibrarian())
        {
            reservation2 = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start2,
                EndDate = end2
            });
        }

        using (AsLibrarian())
        {
            var loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation1.Id });
            await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Damaged });
        }

        var untouched = await _reservationRepository.GetAsync(reservation1.Id);
        untouched.Status.ShouldBe(ReservationStatus.CheckedOut);

        var cancelled = await _reservationRepository.GetAsync(reservation2.Id);
        cancelled.Status.ShouldBe(ReservationStatus.Cancelled);
    }
}
