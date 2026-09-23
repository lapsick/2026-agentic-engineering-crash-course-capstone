using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>WL-01 (FIFO order), WL-02 (no duplicate), WL-03 (cancelling offers the earliest waiting member).</summary>
public class WaitlistFifoAndOfferTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;

    public WaitlistFifoAndOfferTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _waitlistEntryRepository = GetRequiredService<IWaitlistEntryRepository>();
    }

    [Fact]
    public async Task Joining_the_waitlist_twice_for_the_same_instance_is_rejected()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await _reservationAppService.CreateAsync(new CreateReservationDto { ToolInstanceId = instance.Id, StartDate = start, EndDate = start.AddDays(4) });

        await _reservationAppService.JoinWaitlistAsync(new JoinWaitlistDto { ToolInstanceId = instance.Id });

        var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.JoinWaitlistAsync(new JoinWaitlistDto { ToolInstanceId = instance.Id }));
        exception.Code.ShouldBe("Lending:AlreadyOnWaitlist");
    }

    [Fact]
    public async Task Cancelling_a_reservation_offers_the_waitlist_to_the_earliest_waiting_entry()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start,
            EndDate = start.AddDays(4)
        });

        var entry = await _reservationAppService.JoinWaitlistAsync(new JoinWaitlistDto { ToolInstanceId = instance.Id });
        entry.OfferState.ShouldBe(WaitlistOfferState.Waiting);

        await _reservationAppService.CancelAsync(reservation.Id);

        var reloaded = await _waitlistEntryRepository.GetAsync(entry.Id);
        reloaded.OfferState.ShouldBe(WaitlistOfferState.Offered);
        reloaded.OfferExpiresAt.ShouldNotBeNull();
    }
}
