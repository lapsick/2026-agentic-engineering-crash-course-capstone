using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Lending.Loans;

public interface ILoanRepository : IRepository<Loan, Guid>
{
    /// <summary>LOAN-06's pre-check: the open (not-yet-returned) loan for the instance, if any.</summary>
    Task<Loan?> GetOpenForInstanceAsync(Guid toolInstanceId, CancellationToken cancellationToken = default);

    /// <summary>RES-04/RES-05's inputs: every open loan for the member.</summary>
    Task<List<Loan>> GetOpenForMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    /// <summary><c>ReturnReminderWorker</c>'s query (research R5): open loans whose planned return date is within <paramref name="leadDays"/> and not yet reminded.</summary>
    Task<List<Loan>> GetApproachingReminderAsync(DateTime asOf, int leadDays, CancellationToken cancellationToken = default);

    /// <summary><c>OverdueMarkingWorker</c>'s query: open loans whose planned return date has passed and are not yet marked overdue.</summary>
    Task<List<Loan>> GetNewlyOverdueAsync(DateTime asOf, CancellationToken cancellationToken = default);

    /// <summary>The roster view's (FR-024) paged, filtered, sorted query backing <c>ILoanAppService.GetListAsync</c>.</summary>
    Task<List<Loan>> GetPagedListAsync(Guid? memberId, bool onlyOverdue, string sorting, int skipCount, int maxResultCount, CancellationToken cancellationToken = default);

    /// <summary>Matching count for <see cref="GetPagedListAsync"/>.</summary>
    Task<int> GetCountAsync(Guid? memberId, bool onlyOverdue, CancellationToken cancellationToken = default);
}
