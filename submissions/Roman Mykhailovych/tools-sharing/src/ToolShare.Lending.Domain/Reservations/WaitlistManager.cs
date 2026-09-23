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
}
