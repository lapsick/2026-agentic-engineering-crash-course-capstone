using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Reservations;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Lending.Authorization;

/// <summary>FR-019: an authenticated identity with no backing Membership enrolment is refused, for both reservation and checkout.</summary>
public class NonMemberRefusedTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;

    public NonMemberRefusedTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
    }

    [Fact]
    public async Task An_authenticated_non_member_cannot_reserve()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        using (AsAuthenticatedNonMember())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = start.AddDays(2)
            }));
        }
    }

    [Fact]
    public async Task An_authenticated_non_member_cannot_check_out()
    {
        using (AsAuthenticatedNonMember())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = Guid.NewGuid() }));
        }
    }

    /// <summary>008 FR-020: the enrolment gate refuses an out-of-band maintenance report too.</summary>
    [Fact]
    public async Task An_authenticated_non_member_cannot_report_maintenance()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();

        using (AsAuthenticatedNonMember())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = instance.Id,
                ObservedCondition = Catalog.ToolCondition.Good,
                Reason = "Cracked"
            }));
        }
    }
}
