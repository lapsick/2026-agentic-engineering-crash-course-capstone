using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 US2 (FR-010, WL-07, WL-08): an offer outstanding when the instance goes
/// under maintenance is withdrawn and its member re-queued at their original
/// place, so the expiry worker cannot drain the queue while nobody can reserve;
/// closing the request offers that same member first, exactly as today.
/// </summary>
public class OutOfBandWaitlistTests : LendingAuthorizationTestBase
{
    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IReservationAppService _reservationAppService;
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;
    private readonly WaitlistOfferExpiryWorker _worker;

    public OutOfBandWaitlistTests()
    {
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _waitlistEntryRepository = GetRequiredService<IWaitlistEntryRepository>();
        _worker = GetRequiredService<WaitlistOfferExpiryWorker>();
    }

    [Fact]
    public async Task An_outstanding_offer_is_withdrawn_and_its_member_keeps_their_place_through_to_the_close()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        // Held by the default (Administrator) principal; A (Librarian) then B (member) queue behind it.
        var blocking = await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start,
            EndDate = start.AddDays(3)
        });

        WaitlistEntryDto a;
        using (AsLibrarian())
        {
            a = await _reservationAppService.JoinWaitlistAsync(new JoinWaitlistDto { ToolInstanceId = instance.Id });
        }

        WaitlistEntryDto b;
        using (AsMemberWithNoGrants())
        {
            b = await _reservationAppService.JoinWaitlistAsync(new JoinWaitlistDto { ToolInstanceId = instance.Id });
        }

        await _reservationAppService.CancelAsync(blocking.Id);
        (await _waitlistEntryRepository.GetAsync(a.Id)).OfferState.ShouldBe(WaitlistOfferState.Offered);

        MaintenanceRequestDto report;
        using (AsLibrarian())
        {
            report = await _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = instance.Id,
                ObservedCondition = ToolCondition.Good,
                Reason = "Loose guard"
            });
        }

        var withdrawn = await _waitlistEntryRepository.GetAsync(a.Id);
        withdrawn.OfferState.ShouldBe(WaitlistOfferState.Withdrawn);
        withdrawn.ResolvedAt.ShouldNotBeNull();

        var entries = await _waitlistEntryRepository.GetListAsync(e => e.ToolInstanceId == instance.Id);
        var requeued = entries.Single(e => e.MemberId == a.MemberId && e.Id != a.Id);
        requeued.OfferState.ShouldBe(WaitlistOfferState.Waiting);
        requeued.JoinedAt.ShouldBe(withdrawn.JoinedAt);
        entries.Single(e => e.Id == b.Id).OfferState.ShouldBe(WaitlistOfferState.Waiting);

        // Nothing outstanding for the worker to roll down the queue.
        await _worker.ExecuteOnceAsync();
        (await _waitlistEntryRepository.GetAsync(requeued.Id)).OfferState.ShouldBe(WaitlistOfferState.Waiting);
        (await _waitlistEntryRepository.GetAsync(b.Id)).OfferState.ShouldBe(WaitlistOfferState.Waiting);

        // A cannot claim the instance while it is under maintenance.
        using (AsLibrarian())
        {
            var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = start.AddDays(2)
            }));
            exception.Code.ShouldBe(LendingDomainErrorCodes.InstanceUnavailable);
        }

        using (AsLibrarian())
        {
            await _maintenanceRequestAppService.CloseAsync(report.Id, new CloseMaintenanceRequestDto { Cost = 0m });
        }

        (await _waitlistEntryRepository.GetAsync(requeued.Id)).OfferState.ShouldBe(WaitlistOfferState.Offered);
        (await _waitlistEntryRepository.GetAsync(b.Id)).OfferState.ShouldBe(WaitlistOfferState.Waiting);
    }

    [Fact]
    public async Task A_report_with_no_outstanding_offer_leaves_the_waitlist_untouched()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = start,
            EndDate = start.AddDays(3)
        });

        WaitlistEntryDto waiting;
        using (AsMemberWithNoGrants())
        {
            waiting = await _reservationAppService.JoinWaitlistAsync(new JoinWaitlistDto { ToolInstanceId = instance.Id });
        }

        using (AsLibrarian())
        {
            await _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = instance.Id,
                ObservedCondition = ToolCondition.Good,
                Reason = "Loose guard"
            });
        }

        var entries = await _waitlistEntryRepository.GetListAsync(e => e.ToolInstanceId == instance.Id);
        entries.Count.ShouldBe(1);
        entries.Single().Id.ShouldBe(waiting.Id);
        entries.Single().OfferState.ShouldBe(WaitlistOfferState.Waiting);
    }
}
