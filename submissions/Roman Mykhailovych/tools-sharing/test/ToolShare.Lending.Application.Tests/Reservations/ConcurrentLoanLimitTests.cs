using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Catalog.Tools;
using ToolShare.Membership;
using ToolShare.Membership.Members;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>RES-05: the effective concurrent-loan limit is read live from Membership's standing, including the reduced limit under a low rating.</summary>
public class ConcurrentLoanLimitTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly IMemberStandingAppService _memberStandingAppService;
    private readonly IReliabilityReportingAppService _reliabilityReportingAppService;

    public ConcurrentLoanLimitTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _memberStandingAppService = GetRequiredService<IMemberStandingAppService>();
        _reliabilityReportingAppService = GetRequiredService<IReliabilityReportingAppService>();
    }

    private async Task<ToolInstanceDto> CreateInstanceAsync(Guid categoryId, Guid toolId)
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            return await ToolInstanceAppService.CreateAsync(new CreateToolInstanceDto
            {
                ToolId = toolId,
                SerialNumber = Guid.NewGuid().ToString("N")[..8],
                Condition = ToolCondition.Good
            });
        }
    }

    [Fact]
    public async Task Reserving_beyond_the_normal_concurrent_loan_limit_is_rejected()
    {
        var (category, tool, firstInstance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        // Default ConcurrentLoanLimit is 3 — three succeed, the fourth is rejected.
        await _reservationAppService.CreateAsync(new CreateReservationDto { ToolInstanceId = firstInstance.Id, StartDate = start, EndDate = start.AddDays(2) });

        for (var i = 0; i < 2; i++)
        {
            var instance = await CreateInstanceAsync(category.Id, tool.Id);
            await _reservationAppService.CreateAsync(new CreateReservationDto { ToolInstanceId = instance.Id, StartDate = start, EndDate = start.AddDays(2) });
        }

        var fourthInstance = await CreateInstanceAsync(category.Id, tool.Id);
        var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = fourthInstance.Id,
            StartDate = start,
            EndDate = start.AddDays(2)
        }));

        exception.Code.ShouldBe("Lending:ConcurrentLoanLimitReached");
    }

    [Fact]
    public async Task A_low_rating_reduces_the_concurrent_loan_limit()
    {
        var (category, tool, firstInstance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var standing = await _memberStandingAppService.GetByIdentityUserIdAsync(LendingTestPrincipals.DefaultTestRunnerId);
        standing.IsEnrolled.ShouldBeTrue();

        // Drop the rating below the default LowRatingThreshold (50) via the
        // public reliability-reporting contract — 6 x -10 (default
        // OverduePenaltyPoints) takes 100 down to 40.
        for (var i = 0; i < 6; i++)
        {
            await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
            {
                MemberId = standing.MemberId!.Value,
                OutcomeType = ReliabilityOutcomeType.OverdueReturn,
                OccurrenceId = Guid.NewGuid()
            });
        }

        // First reservation succeeds (reduced limit is 1).
        await _reservationAppService.CreateAsync(new CreateReservationDto { ToolInstanceId = firstInstance.Id, StartDate = start, EndDate = start.AddDays(2) });

        var secondInstance = await CreateInstanceAsync(category.Id, tool.Id);
        var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = secondInstance.Id,
            StartDate = start,
            EndDate = start.AddDays(2)
        }));

        exception.Code.ShouldBe("Lending:ConcurrentLoanLimitReached");
    }
}
