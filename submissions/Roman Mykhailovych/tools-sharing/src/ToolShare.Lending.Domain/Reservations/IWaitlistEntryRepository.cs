using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Lending.Reservations;

public interface IWaitlistEntryRepository : IRepository<WaitlistEntry, Guid>
{
    /// <summary>WL-03's target: the earliest still-Waiting entry for the instance, or null if the queue is empty.</summary>
    Task<WaitlistEntry?> GetEarliestWaitingAsync(Guid toolInstanceId, CancellationToken cancellationToken = default);

    /// <summary>WL-02's duplicate-entry pre-check: an unresolved (Waiting or Offered) entry for this member/instance pair, if any.</summary>
    Task<WaitlistEntry?> FindActiveForMemberAndInstanceAsync(Guid memberId, Guid toolInstanceId, CancellationToken cancellationToken = default);

    /// <summary>WL-05's worker query: every Offered entry whose window has passed as of <paramref name="asOf"/>.</summary>
    Task<List<WaitlistEntry>> GetExpiredOffersAsync(DateTime asOf, CancellationToken cancellationToken = default);
}
