using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Reports;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Lending.Authorization;

/// <summary>
/// FR-012 / SC-005: the reports are restricted to Librarian and Administrator.
/// The guard being exercised is the class-level
/// <c>[Authorize(LendingPermissions.Reports.Default)]</c> on
/// <c>ReportAppService</c>, which is why refusing one method refuses all three —
/// each is asserted anyway, so a later per-method override cannot silently open
/// a hole. Mirrors the existing <see cref="MaintenanceAuthorizationTests"/>.
/// </summary>
public class ReportAuthorizationTests : LendingAuthorizationTestBase
{
    private readonly IReportAppService _reportAppService;

    public ReportAuthorizationTests()
    {
        _reportAppService = GetRequiredService<IReportAppService>();
    }

    [Fact]
    public async Task A_member_with_no_Lending_grants_cannot_open_the_overdue_report()
    {
        using (AsMemberWithNoGrants())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _reportAppService.GetOverdueLoansAsync());
        }
    }

    [Fact]
    public async Task A_member_with_no_Lending_grants_cannot_open_the_popularity_report()
    {
        using (AsMemberWithNoGrants())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(
                () => _reportAppService.GetToolPopularityAsync(new ToolPopularityReportInput()));
        }
    }

    [Fact]
    public async Task A_member_with_no_Lending_grants_cannot_open_the_maintenance_cost_report()
    {
        using (AsMemberWithNoGrants())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(
                () => _reportAppService.GetMaintenanceCostAsync(new MaintenanceCostReportInput
                {
                    From = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30),
                    To = DateOnly.FromDateTime(DateTime.UtcNow)
                }));
        }
    }

    [Fact]
    public async Task A_Librarian_is_granted_the_reports_permission_by_the_role_seeder()
    {
        // The other half of FR-012: the seeder edit actually took effect, so
        // "refused for a Member" is not simply "refused for everyone".
        using (AsLibrarian())
        {
            await _reportAppService.GetOverdueLoansAsync();
        }
    }
}
