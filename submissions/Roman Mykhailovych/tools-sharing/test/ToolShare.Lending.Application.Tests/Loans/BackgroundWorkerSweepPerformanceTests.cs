using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>
/// Verifies plan.md's performance goal (T118): each background worker sweep
/// (waitlist expiry, reminders, overdue marking) completes a full pass in
/// under 5 seconds at the documented scale (low tens of concurrent open
/// loans/reservations). Seeds directly via each aggregate's own repository —
/// mirroring <c>OneOpenRequestPerInstanceTests</c> — rather than through the
/// app services: a real deployment at this scale has that many DISTINCT
/// members, but this suite has only a handful of synthetic test principals,
/// and routing every seed row through one shared member would immediately
/// trip RES-05's concurrent-loan limit long before reaching the scale being
/// measured. What's being timed is the workers' own query/update cost against
/// that many rows, not Membership's/Catalog's per-request overhead.
/// </summary>
public class BackgroundWorkerSweepPerformanceTests : LendingAuthorizationTestBase
{
    private const int ItemsPerWorker = 20;

    private readonly IReservationRepository _reservationRepository;
    private readonly ILoanRepository _loanRepository;
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;
    private readonly WaitlistOfferExpiryWorker _waitlistWorker;
    private readonly ReturnReminderWorker _reminderWorker;
    private readonly OverdueMarkingWorker _overdueWorker;

    public BackgroundWorkerSweepPerformanceTests()
    {
        _reservationRepository = GetRequiredService<IReservationRepository>();
        _loanRepository = GetRequiredService<ILoanRepository>();
        _waitlistEntryRepository = GetRequiredService<IWaitlistEntryRepository>();
        _waitlistWorker = GetRequiredService<WaitlistOfferExpiryWorker>();
        _reminderWorker = GetRequiredService<ReturnReminderWorker>();
        _overdueWorker = GetRequiredService<OverdueMarkingWorker>();
    }

    [Fact]
    public async Task A_full_sweep_of_all_three_workers_completes_in_under_five_seconds()
    {
        await SeedApproachingReminderLoansAsync();
        await SeedOverdueLoansAsync();
        await SeedExpiredWaitlistOffersAsync();

        var stopwatch = Stopwatch.StartNew();

        await _waitlistWorker.ExecuteOnceAsync();
        await _reminderWorker.ExecuteOnceAsync();
        await _overdueWorker.ExecuteOnceAsync();

        stopwatch.Stop();

        stopwatch.Elapsed.TotalSeconds.ShouldBeLessThan(5, $"full sweep took {stopwatch.Elapsed.TotalSeconds:F2}s");
    }

    private async Task SeedApproachingReminderLoansAsync()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var end = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 0; i < ItemsPerWorker; i++)
        {
            var (_, _, instance) = await SeedCatalogDataAsync();
            var reservation = new Reservation(Guid.NewGuid(), Guid.NewGuid(), instance.Id, start, end, maxLoanTermDays: 14, createdAt: DateTime.UtcNow);
            var loan = new Loan(Guid.NewGuid(), reservation, DateTime.UtcNow.AddDays(-1), ToolCondition.Good);

            await _reservationRepository.InsertAsync(reservation, autoSave: true);
            await _loanRepository.InsertAsync(loan, autoSave: true);
        }
    }

    private async Task SeedOverdueLoansAsync()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        var end = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

        for (var i = 0; i < ItemsPerWorker; i++)
        {
            var (_, _, instance) = await SeedCatalogDataAsync();
            var reservation = new Reservation(Guid.NewGuid(), Guid.NewGuid(), instance.Id, start, end, maxLoanTermDays: 14, createdAt: DateTime.UtcNow.AddDays(-10));
            var loan = new Loan(Guid.NewGuid(), reservation, DateTime.UtcNow.AddDays(-10), ToolCondition.Good);

            await _reservationRepository.InsertAsync(reservation, autoSave: true);
            await _loanRepository.InsertAsync(loan, autoSave: true);
        }
    }

    private async Task SeedExpiredWaitlistOffersAsync()
    {
        for (var i = 0; i < ItemsPerWorker; i++)
        {
            var (_, _, instance) = await SeedCatalogDataAsync();

            var entry = new WaitlistEntry(Guid.NewGuid(), Guid.NewGuid(), instance.Id, DateTime.UtcNow.AddDays(-2));
            entry.Offer(DateTime.UtcNow.AddDays(-2), windowHours: 24);
            SetOfferExpiresAt(entry, DateTime.UtcNow.AddDays(-1));

            await _waitlistEntryRepository.InsertAsync(entry, autoSave: true);
        }
    }

    /// <summary>Same reflection-based backdoor as <c>WaitlistOfferExpiryWorkerTests</c> — simulates elapsed time without a real clock dependency.</summary>
    private static void SetOfferExpiresAt(WaitlistEntry entry, DateTime value)
    {
        typeof(WaitlistEntry).GetProperty(nameof(WaitlistEntry.OfferExpiresAt))!.SetValue(entry, value);
    }
}
