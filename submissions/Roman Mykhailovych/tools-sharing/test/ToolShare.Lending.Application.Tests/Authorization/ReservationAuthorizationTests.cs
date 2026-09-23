using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Reservations;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Lending.Authorization;

/// <summary>FR-029: creating/cancelling one's own reservation requires only the enrolment gate — no `Lending.*` permission.</summary>
public class ReservationAuthorizationTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;

    public ReservationAuthorizationTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
    }

    [Fact]
    public async Task A_member_with_no_Lending_grants_may_still_reserve()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        using (AsMemberWithNoGrants())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = start.AddDays(2)
            });

            reservation.Status.ShouldBe(ReservationStatus.Active);
        }
    }

    [Fact]
    public async Task An_authenticated_non_member_is_refused()
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
    public async Task An_anonymous_principal_is_refused()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        using (AsAnonymous())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = start.AddDays(2)
            }));
        }
    }
}
