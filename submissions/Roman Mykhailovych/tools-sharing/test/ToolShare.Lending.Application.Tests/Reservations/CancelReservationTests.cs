using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>RES-06: cancellable only while Active.</summary>
public class CancelReservationTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;

    public CancelReservationTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
    }

    [Fact]
    public async Task Cancelling_an_active_reservation_succeeds_and_frees_the_instance()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start,
            EndDate = start.AddDays(4)
        });

        await _reservationAppService.CancelAsync(reservation.Id);

        // The instance is reservable again for the same range.
        var second = await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start,
            EndDate = start.AddDays(4)
        });

        second.Status.ShouldBe(ReservationStatus.Active);
    }

    [Fact]
    public async Task Cancelling_a_reservation_twice_is_rejected()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start,
            EndDate = start.AddDays(4)
        });

        await _reservationAppService.CancelAsync(reservation.Id);

        var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.CancelAsync(reservation.Id));
        exception.Code.ShouldBe("Lending:ReservationNotCancellable");
    }
}
