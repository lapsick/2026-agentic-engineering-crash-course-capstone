using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ToolShare.Catalog;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Catalog.Tools;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.Members;

namespace ToolShare.Lending.Reports;

/// <summary>
/// Seeding helpers shared by 006-librarian-reports' integration suites, in the
/// spirit of the existing <see cref="LendingAuthorizationTestBase"/>.
///
/// History is built by inserting <see cref="Reservation"/>/<see cref="Loan"/>
/// aggregates directly rather than by driving <c>ReservationAppService</c> +
/// <c>LoanAppService</c>, for two reasons the app-service route cannot give
/// these tests: RES-04 refuses a member a second reservation while they hold an
/// overdue loan (so a multi-overdue ordering fixture is unbuildable through it),
/// and the reports need loans pointing at deliberately unresolvable member and
/// instance ids to prove guarantee B9. The entities are constructed through
/// their real public constructors, so the rows are the same rows checkout
/// produces — only the path to them is shorter.
/// </summary>
public abstract class ReportTestBase : LendingAuthorizationTestBase
{
    protected readonly IReportAppService ReportAppService;
    protected readonly ILoanRepository LoanRepository;
    protected readonly IReservationRepository ReservationRepository;
    protected readonly IMaintenanceRequestRepository MaintenanceRequestRepository;

    private readonly IMemberStandingAppService _memberStandingAppService;

    protected ReportTestBase()
    {
        ReportAppService = GetRequiredService<IReportAppService>();
        LoanRepository = GetRequiredService<ILoanRepository>();
        ReservationRepository = GetRequiredService<IReservationRepository>();
        MaintenanceRequestRepository = GetRequiredService<IMaintenanceRequestRepository>();
        _memberStandingAppService = GetRequiredService<IMemberStandingAppService>();
    }

    protected static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>The member id behind one of <see cref="LendingTestPrincipals"/>' seeded identities.</summary>
    protected async Task<Guid> ResolveMemberIdAsync(Guid identityUserId)
    {
        var standing = await _memberStandingAppService.GetByIdentityUserIdAsync(identityUserId);
        return standing.MemberId!.Value;
    }

    protected Task<Guid> LibrarianMemberIdAsync() => ResolveMemberIdAsync(LendingTestPrincipals.LibrarianUserId);

    /// <summary>
    /// One tool with <paramref name="instanceCount"/> instances, so the
    /// popularity report's instance→tool fold (research R4) has something to
    /// fold. Created as admin, like <see cref="SeedCatalogDataAsync"/>.
    /// </summary>
    protected async Task<(ToolDto Tool, List<ToolInstanceDto> Instances)> SeedToolWithInstancesAsync(string toolName, int instanceCount)
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var category = await CategoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
            var tool = await ToolAppService.CreateAsync(new CreateToolDto { Name = toolName, CategoryId = category.Id });

            var instances = new List<ToolInstanceDto>();
            for (var i = 0; i < instanceCount; i++)
            {
                instances.Add(await ToolInstanceAppService.CreateAsync(new CreateToolInstanceDto
                {
                    ToolId = tool.Id,
                    SerialNumber = Guid.NewGuid().ToString("N")[..8],
                    Condition = ToolCondition.Good
                }));
            }

            return (tool, instances);
        }
    }

    /// <summary>Retires an instance through Catalog's own app service, as admin — the real retirement path, not a fabricated state.</summary>
    protected async Task RetireInstanceAsync(Guid toolInstanceId)
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var fresh = await ToolInstanceAppService.GetAsync(toolInstanceId);
            await ToolInstanceAppService.RetireAsync(toolInstanceId, new RetireToolInstanceDto
            {
                Reason = "Retired during a report test",
                ConcurrencyStamp = fresh.ConcurrencyStamp
            });
        }
    }

    /// <summary>An open (unreturned) loan whose planned return date is <paramref name="daysOverdue"/> days in the past — so it is overdue right now, without any worker having run.</summary>
    protected Task<Loan> InsertOverdueLoanAsync(Guid memberId, Guid toolInstanceId, int daysOverdue)
    {
        var plannedReturn = Today.AddDays(-daysOverdue);
        return InsertLoanAsync(memberId, toolInstanceId, plannedReturn.AddDays(-3), plannedReturn);
    }

    /// <summary>A loan checked out on <paramref name="checkedOutOn"/> and returned the same period — for popularity history that is not overdue.</summary>
    protected async Task<Loan> InsertCompletedLoanAsync(Guid memberId, Guid toolInstanceId, DateOnly checkedOutOn)
    {
        var loan = await InsertLoanAsync(memberId, toolInstanceId, checkedOutOn, checkedOutOn.AddDays(2));

        loan.Return(checkedOutOn.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), ToolCondition.Good);
        await LoanRepository.UpdateAsync(loan, autoSave: true);

        return loan;
    }

    /// <summary>The general form: a loan realized from a reservation over the given range, still open unless the caller returns it.</summary>
    protected async Task<Loan> InsertLoanAsync(Guid memberId, Guid toolInstanceId, DateOnly startDate, DateOnly plannedReturnDate)
    {
        var checkedOutAt = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var reservation = new Reservation(
            Guid.NewGuid(),
            memberId,
            toolInstanceId,
            startDate,
            plannedReturnDate,
            maxLoanTermDays: 3650,
            createdAt: checkedOutAt);

        // Realizes the reservation as CheckedOut, so the row never participates
        // in the Active-only overlap exclusion constraint.
        var loan = new Loan(Guid.NewGuid(), reservation, checkedOutAt, ToolCondition.Good);

        await ReservationRepository.InsertAsync(reservation, autoSave: true);
        await LoanRepository.InsertAsync(loan, autoSave: true);

        return loan;
    }

    /// <summary>A maintenance request closed on <paramref name="closedOn"/> with <paramref name="cost"/>.</summary>
    protected async Task<MaintenanceRequest> InsertClosedMaintenanceRequestAsync(Guid toolInstanceId, Guid triggeringLoanId, DateOnly openedOn, DateOnly closedOn, decimal cost)
    {
        var request = new MaintenanceRequest(
            Guid.NewGuid(),
            toolInstanceId,
            triggeringLoanId,
            openedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        request.Close(closedOn.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc), cost);

        await MaintenanceRequestRepository.InsertAsync(request, autoSave: true);

        return request;
    }

    /// <summary>An open maintenance request — no cost recorded, so it must contribute nothing to any period (FR-008).</summary>
    protected async Task<MaintenanceRequest> InsertOpenMaintenanceRequestAsync(Guid toolInstanceId, Guid triggeringLoanId, DateOnly openedOn)
    {
        var request = new MaintenanceRequest(
            Guid.NewGuid(),
            toolInstanceId,
            triggeringLoanId,
            openedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        await MaintenanceRequestRepository.InsertAsync(request, autoSave: true);

        return request;
    }
}
