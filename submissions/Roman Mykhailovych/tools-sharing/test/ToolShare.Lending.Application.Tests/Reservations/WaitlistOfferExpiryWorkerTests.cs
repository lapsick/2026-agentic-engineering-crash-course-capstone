using System;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>WL-05/research R4: drives the worker's execution directly rather than waiting for the real timer.</summary>
public class WaitlistOfferExpiryWorkerTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;
    private readonly WaitlistOfferExpiryWorker _worker;

    public WaitlistOfferExpiryWorkerTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _waitlistEntryRepository = GetRequiredService<IWaitlistEntryRepository>();
        _worker = GetRequiredService<WaitlistOfferExpiryWorker>();
    }

    [Fact]
    public async Task Expires_a_past_window_offer_and_rolls_to_the_next_waiting_member()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        // Held by the default (Administrator) principal.
        var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start,
            EndDate = start.AddDays(4)
        });

        WaitlistEntryDto firstEntry;
        using (AsLibrarian())
        {
            firstEntry = await _reservationAppService.JoinWaitlistAsync(new JoinWaitlistDto { ToolInstanceId = instance.Id });
        }

        WaitlistEntryDto secondEntry;
        using (AsMemberWithNoGrants())
        {
            secondEntry = await _reservationAppService.JoinWaitlistAsync(new JoinWaitlistDto { ToolInstanceId = instance.Id });
        }

        await _reservationAppService.CancelAsync(reservation.Id);

        var firstOffered = await _waitlistEntryRepository.GetAsync(firstEntry.Id);
        firstOffered.OfferState.ShouldBe(WaitlistOfferState.Offered);

        // Force the first offer into the past so the worker treats it as expired.
        SetOfferExpiresAt(firstOffered, DateTime.UtcNow.AddDays(-1));
        await _waitlistEntryRepository.UpdateAsync(firstOffered, autoSave: true);

        await _worker.ExecuteOnceAsync();

        var firstResolved = await _waitlistEntryRepository.GetAsync(firstEntry.Id);
        firstResolved.OfferState.ShouldBe(WaitlistOfferState.Expired);

        var secondResolved = await _waitlistEntryRepository.GetAsync(secondEntry.Id);
        secondResolved.OfferState.ShouldBe(WaitlistOfferState.Offered);
    }

    [Fact]
    public async Task Leaves_a_not_yet_expired_offer_untouched()
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
        await _reservationAppService.CancelAsync(reservation.Id);

        var offered = await _waitlistEntryRepository.GetAsync(entry.Id);
        offered.OfferState.ShouldBe(WaitlistOfferState.Offered);

        await _worker.ExecuteOnceAsync();

        var stillOffered = await _waitlistEntryRepository.GetAsync(entry.Id);
        stillOffered.OfferState.ShouldBe(WaitlistOfferState.Offered);
    }

    /// <summary>Test-only backdoor to simulate elapsed time without a real clock dependency — reflection over the private setter, exactly the kind of direct entity manipulation this module's own test suite already uses (OverdueBlockTests).</summary>
    private static void SetOfferExpiresAt(WaitlistEntry entry, DateTime value)
    {
        typeof(WaitlistEntry).GetProperty(nameof(WaitlistEntry.OfferExpiresAt))!.SetValue(entry, value);
    }
}
