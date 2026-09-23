using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>FR-003, RES-03: an overlapping request is refused and offered a waitlist join instead of a dead-end error.</summary>
public class OverlapAndWaitlistTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;

    public OverlapAndWaitlistTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
    }

    [Fact]
    public async Task An_overlapping_reservation_attempt_is_rejected_naming_the_conflict()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var end = start.AddDays(4);

        await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start,
            EndDate = end
        });

        var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start.AddDays(2),
            EndDate = end.AddDays(2)
        }));

        exception.Code.ShouldBe("Lending:InstanceAlreadyReservedForRange");
    }

    [Fact]
    public async Task Joining_the_waitlist_for_a_reserved_instance_succeeds()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start,
            EndDate = start.AddDays(4)
        });

        var entry = await _reservationAppService.JoinWaitlistAsync(new JoinWaitlistDto { ToolInstanceId = instance.Id });

        entry.OfferState.ShouldBe(WaitlistOfferState.Waiting);
        entry.ToolInstanceId.ShouldBe(instance.Id);
    }

    [Fact]
    public async Task Non_overlapping_reservations_for_the_same_instance_both_succeed()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start,
            EndDate = start.AddDays(2)
        });

        var second = await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start.AddDays(3),
            EndDate = start.AddDays(5)
        });

        second.Status.ShouldBe(ReservationStatus.Active);
    }
}
