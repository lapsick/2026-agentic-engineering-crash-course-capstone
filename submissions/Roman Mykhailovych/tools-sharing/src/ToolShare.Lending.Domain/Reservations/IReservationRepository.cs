using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Lending.Reservations;

public interface IReservationRepository : IRepository<Reservation, Guid>
{
    /// <summary>RES-03's friendly pre-check — the exclusion constraint (research R3) is the authority under concurrency.</summary>
    Task<bool> HasOverlapAsync(Guid toolInstanceId, DateOnly startDate, DateOnly endDate, Guid? excludeReservationId = null, CancellationToken cancellationToken = default);

    /// <summary>Backs RES-05's concurrent-loan-limit check.</summary>
    Task<int> GetActiveCountForMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>RES-08/MAINT-03's target set: every Active reservation for the instance whose range has not yet started.</summary>
    Task<List<Reservation>> GetActiveForInstanceAsync(Guid toolInstanceId, CancellationToken cancellationToken = default);
}
