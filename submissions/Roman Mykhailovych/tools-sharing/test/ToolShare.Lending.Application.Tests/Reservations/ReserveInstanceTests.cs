using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>FR-001, RES-01, RES-02.</summary>
public class ReserveInstanceTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;

    public ReserveInstanceTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
    }

    [Fact]
    public async Task Reserving_an_available_instance_within_the_maximum_loan_term_succeeds()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();

        var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5)
        });

        reservation.Status.ShouldBe(ReservationStatus.Active);
        reservation.ToolInstanceId.ShouldBe(instance.Id);
    }

    [Fact]
    public async Task Reserving_beyond_the_maximum_loan_term_is_rejected()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();

        var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30) // default MaxLoanTermDays is 14
        }));

        exception.Code.ShouldBe("Lending:LoanTermExceeded");
    }

    [Fact]
    public async Task Reserving_an_unavailable_instance_is_rejected()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        await ToolInstanceAppService.RetireAsync(instance.Id, new Catalog.ToolInstances.RetireToolInstanceDto
        {
            Reason = "no longer usable",
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5)
        }));

        exception.Code.ShouldBe("Lending:InstanceUnavailable");
    }
}
