using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Permissions;
using ToolShare.Membership.Members;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Lending.Reports;

/// <summary>
/// The three librarian reports (US7). Read-only throughout (FR-011) — every
/// method composes a query and returns DTOs; none writes.
///
/// Queries go through the default <see cref="IRepository{TEntity,TKey}"/>
/// rather than Lending's custom repositories (research R3): each aggregation
/// has exactly one caller, needs no domain-service reuse, and leaving 004's
/// tested repository surface untouched is worth more than the shared method.
/// </summary>
[Authorize(LendingPermissions.Reports.Default)]
public class ReportAppService : ApplicationService, IReportAppService
{
    private readonly IRepository<Loan, Guid> _loanRepository;
    private readonly IRepository<MaintenanceRequest, Guid> _maintenanceRequestRepository;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;
    private readonly IMemberStandingAppService _memberStandingAppService;

    public ReportAppService(
        IRepository<Loan, Guid> loanRepository,
        IRepository<MaintenanceRequest, Guid> maintenanceRequestRepository,
        IToolInstanceLookupAppService toolInstanceLookupAppService,
        IMemberStandingAppService memberStandingAppService)
    {
        _loanRepository = loanRepository;
        _maintenanceRequestRepository = maintenanceRequestRepository;
        _toolInstanceLookupAppService = toolInstanceLookupAppService;
        _memberStandingAppService = memberStandingAppService;
    }

    /// <summary>
    /// FR-004–FR-006. Overdue membership is computed live from
    /// <see cref="ApplicationService.Clock"/>, never from the stored
    /// <c>Loan.IsOverdue</c> flag, which <c>OverdueMarkingWorker</c> refreshes
    /// only hourly (research R2, guarantee B2). The SQL predicate restates
    /// <c>Loan.IsOverdueAsOf</c> because EF Core cannot translate an instance
    /// method; <c>ReportDriftTests</c> is what keeps the two from diverging.
    /// </summary>
    public virtual async Task<ListResultDto<OverdueLoanReportItemDto>> GetOverdueLoansAsync()
    {
        var today = DateOnly.FromDateTime(Clock.Now);

        var queryable = await _loanRepository.GetQueryableAsync();
        var overdueLoans = await AsyncExecuter.ToListAsync(
            queryable
                .Where(l => l.ReturnedAt == null && l.PlannedReturnDate < today)
                // Most overdue first (FR-006) — the earliest planned return
                // date is the largest days-overdue value, and it is the form
                // the database can sort on.
                .OrderBy(l => l.PlannedReturnDate)
                .ThenBy(l => l.CheckedOutAt));

        if (overdueLoans.Count == 0)
        {
            return new ListResultDto<OverdueLoanReportItemDto>(new List<OverdueLoanReportItemDto>());
        }

        // One batch call into each module, never one per row (B10).
        var instances = await _toolInstanceLookupAppService.GetByIdsAsync(
            overdueLoans.Select(l => l.ToolInstanceId).Distinct().ToList());
        var instancesById = instances.ToDictionary(i => i.Id);

        var standings = await _memberStandingAppService.GetByIdsAsync(
            overdueLoans.Select(l => l.MemberId).Distinct().ToList());
        var standingsByMemberId = standings
            .Where(s => s.MemberId.HasValue)
            .ToDictionary(s => s.MemberId!.Value);

        var items = overdueLoans
            .Select(l =>
            {
                // Both lookups omit unknown ids rather than throwing, so an
                // unresolvable reference leaves a null name and keeps the row,
                // with the raw id still available to the UI (B9).
                instancesById.TryGetValue(l.ToolInstanceId, out var instance);
                standingsByMemberId.TryGetValue(l.MemberId, out var standing);

                return new OverdueLoanReportItemDto
                {
                    LoanId = l.Id,
                    MemberId = l.MemberId,
                    MemberDisplayName = standing?.DisplayName,
                    ToolInstanceId = l.ToolInstanceId,
                    ToolName = instance?.ToolName,
                    SerialNumber = instance?.SerialNumber,
                    CheckedOutAt = l.CheckedOutAt,
                    PlannedReturnDate = l.PlannedReturnDate,
                    DaysOverdue = l.DaysOverdueAsOf(today)
                };
            })
            .ToList();

        return new ListResultDto<OverdueLoanReportItemDto>(items);
    }

    /// <summary>
    /// FR-001–FR-003. Counts per instance in SQL, then folds instances onto
    /// their tool in memory (research R4): <c>Loan</c> stores
    /// <c>ToolInstanceId</c> only — deliberately, since the instance→tool
    /// relationship is Catalog's to own and Principle III forbids Lending
    /// keeping a copy — so the fold cannot happen in the database. Its input is
    /// one row per distinct instance borrowed in range, bounded by fleet size
    /// rather than by history: the unbounded quantity, loans, is collapsed
    /// before it leaves PostgreSQL.
    /// </summary>
    public virtual async Task<ListResultDto<ToolPopularityReportItemDto>> GetToolPopularityAsync(ToolPopularityReportInput input)
    {
        GuardDateRange(input.From, input.To, bothRequired: false);

        var queryable = await _loanRepository.GetQueryableAsync();

        if (input.From is { } from)
        {
            var fromAt = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            queryable = queryable.Where(l => l.CheckedOutAt >= fromAt);
        }

        if (input.To is { } to)
        {
            // Inclusive upper bound against a DateTime column: everything
            // before midnight opening the following day.
            var exclusiveUpperBound = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            queryable = queryable.Where(l => l.CheckedOutAt < exclusiveUpperBound);
        }

        var perInstance = await AsyncExecuter.ToListAsync(
            queryable
                .GroupBy(l => l.ToolInstanceId)
                .Select(g => new { ToolInstanceId = g.Key, LoanCount = g.Count() }));

        if (perInstance.Count == 0)
        {
            return new ListResultDto<ToolPopularityReportItemDto>(new List<ToolPopularityReportItemDto>());
        }

        // Retired instances come back too — Catalog's lookup returns them by
        // contract — so retirement never erases a tool's history (FR-003).
        var instances = await _toolInstanceLookupAppService.GetByIdsAsync(
            perInstance.Select(x => x.ToolInstanceId).ToList());
        var instancesById = instances.ToDictionary(i => i.Id);

        var items = perInstance
            .Select(x => new
            {
                x.LoanCount,
                Instance = instancesById.TryGetValue(x.ToolInstanceId, out var found) ? found : null
            })
            // An instance Catalog cannot resolve has no tool identity to rank
            // under; there is no row to show, unlike the overdue report where
            // the loan itself is the row.
            .Where(x => x.Instance is not null)
            .GroupBy(x => x.Instance!.ToolId)
            .Select(g => new ToolPopularityReportItemDto
            {
                ToolId = g.Key,
                ToolName = g.First().Instance!.ToolName,
                LoanCount = g.Sum(x => x.LoanCount)
            })
            .OrderByDescending(i => i.LoanCount)
            .ThenBy(i => i.ToolName)
            .ToList();

        return new ListResultDto<ToolPopularityReportItemDto>(items);
    }

    /// <summary>
    /// FR-007, FR-008. Sums and counts in SQL over <c>Closed</c> requests
    /// attributed by <c>ClosedAt</c> — the only defensible date, since a
    /// request has no cost before it closes (research R6). <c>Close</c> rejects
    /// a null or negative cost, so a Closed request always carries one and the
    /// only nullable case is the empty set, coalesced to <c>0m</c> (FR-013).
    /// Makes no cross-module call: this report reads only Lending's own data.
    /// </summary>
    public virtual async Task<MaintenanceCostReportDto> GetMaintenanceCostAsync(MaintenanceCostReportInput input)
    {
        GuardDateRange(input.From, input.To, bothRequired: true);

        var from = input.From!.Value;
        var to = input.To!.Value;

        var fromAt = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        // Inclusive upper bound against a DateTime column — `<= to` would
        // silently drop everything closed after midnight on the last day.
        var exclusiveUpperBound = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var queryable = await _maintenanceRequestRepository.GetQueryableAsync();
        var inRange = queryable.Where(r =>
            r.Status == MaintenanceRequestStatus.Closed &&
            r.ClosedAt >= fromAt &&
            r.ClosedAt < exclusiveUpperBound);

        var summary = await AsyncExecuter.FirstOrDefaultAsync(
            inRange
                .GroupBy(_ => 1)
                .Select(g => new { TotalCost = g.Sum(r => r.Cost), ClosedRequestCount = g.Count() }));

        return new MaintenanceCostReportDto
        {
            From = from,
            To = to,
            TotalCost = summary?.TotalCost ?? 0m,
            ClosedRequestCount = summary?.ClosedRequestCount ?? 0
        };
    }

    /// <summary>
    /// FR-009, applied by every range-taking report. Two checks in order:
    /// a bound the report requires was not supplied, then a start after its
    /// end. Both are rejected rather than answered with an empty result, which
    /// would misreport an invalid question as "nothing happened".
    /// </summary>
    private static void GuardDateRange(DateOnly? from, DateOnly? to, bool bothRequired)
    {
        if (bothRequired && (from is null || to is null))
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidReportDateRange);
        }

        if (from is not null && to is not null && from > to)
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidReportDateRange);
        }
    }
}
