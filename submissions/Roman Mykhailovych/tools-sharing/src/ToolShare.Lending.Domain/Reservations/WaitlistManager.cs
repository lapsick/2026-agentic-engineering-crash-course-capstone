using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// The rules that need repository access: WL-02's duplicate-entry pre-check
/// (join) and driving WL-03's offer to the earliest waiting entry whenever an
/// instance frees (research R4).
/// </summary>
public class WaitlistManager : DomainService
{
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;

    public WaitlistManager(IWaitlistEntryRepository waitlistEntryRepository)
    {
        _waitlistEntryRepository = waitlistEntryRepository;
    }

    /// <summary>Enforces WL-02.</summary>
    public async Task<WaitlistEntry> JoinAsync(Guid memberId, Guid toolInstanceId, DateTime at)
    {
        var existing = await _waitlistEntryRepository.FindActiveForMemberAndInstanceAsync(memberId, toolInstanceId);
        if (existing is not null)
        {
            throw new BusinessException(LendingDomainErrorCodes.AlreadyOnWaitlist);
        }

        return new WaitlistEntry(GuidGenerator.Create(), memberId, toolInstanceId, at);
    }

    /// <summary>
    /// Offers the earliest still-Waiting entry for the instance, if any — a
    /// no-op when the queue is empty. Called whenever an instance frees up
    /// (reservation cancelled, loan returned cleanly, maintenance request
    /// closed) and whenever <c>WaitlistOfferExpiryWorker</c> rolls an expired
    /// offer forward (research R4).
    /// </summary>
    public async Task OfferNextAsync(Guid toolInstanceId, DateTime at, int windowHours)
    {
        var next = await _waitlistEntryRepository.GetEarliestWaitingAsync(toolInstanceId);
        if (next is null)
        {
            return;
        }

        next.Offer(at, windowHours);
        await _waitlistEntryRepository.UpdateAsync(next);
    }

    /// <summary>
    /// Enforces WL-07/WL-08 (008 research R6): an instance just went under
    /// maintenance while one entry held an offer nobody can act on. Withdraw
    /// it and re-queue the same member at their original place, so the expiry
    /// worker has nothing to roll down the queue and closing the request
    /// offers this member first — via the unchanged <see cref="OfferNextAsync"/>.
    /// A no-op when no offer is outstanding.
    /// </summary>
    public async Task WithdrawOutstandingOfferAsync(Guid toolInstanceId, DateTime at)
    {
        var offered = await _waitlistEntryRepository.FindOfferedForInstanceAsync(toolInstanceId);
        if (offered is null)
        {
            return;
        }

        offered.Withdraw(at);

        // Saved before the insert: WL-02's filtered unique index allows only one
        // Waiting/Offered entry per member and instance at a time.
        await _waitlistEntryRepository.UpdateAsync(offered, autoSave: true);

        var requeued = new WaitlistEntry(GuidGenerator.Create(), offered.MemberId, offered.ToolInstanceId, offered.JoinedAt);
        await _waitlistEntryRepository.InsertAsync(requeued, autoSave: true);
    }
}
