using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// MAINT-01 — the filtered unique index is the authority under concurrency;
/// this exercises the application-layer pre-check
/// (<see cref="LoanManager.ReturnAsync"/>) that gives a friendly rejection
/// before that index is ever hit. Reachable only via direct domain
/// manipulation (mirrors <c>OverdueBlockTests</c>): the checkout app service
/// already refuses a second loan for an instance Catalog reports unavailable,
/// so this simulates the genuinely-concurrent race a real deployment could
/// still hit.
/// </summary>
public class OneOpenRequestPerInstanceTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly ILoanRepository _loanRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly LoanManager _loanManager;

    public OneOpenRequestPerInstanceTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _loanRepository = GetRequiredService<ILoanRepository>();
        _reservationRepository = GetRequiredService<IReservationRepository>();
        _loanManager = GetRequiredService<LoanManager>();
    }

    [Fact]
    public async Task A_second_worsened_return_for_an_instance_already_under_maintenance_is_rejected()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        Guid memberId;
        using (AsMemberWithNoGrants())
        {
            var reservation1 = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = start.AddDays(2)
            });
            memberId = reservation1.MemberId;

            using (AsLibrarian())
            {
                var loan1 = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation1.Id });
                await _loanAppService.ReturnAsync(loan1.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Damaged });
            }
        }

        using (AsLibrarian())
        {
            var reservation2 = new Reservation(Guid.NewGuid(), memberId, instance.Id, start.AddDays(3), start.AddDays(5), maxLoanTermDays: 14, createdAt: DateTime.UtcNow);
            await _reservationRepository.InsertAsync(reservation2, autoSave: true);

            var loan2 = await _loanManager.CheckOutAsync(reservation2, isInstanceAvailable: true, DateTime.UtcNow, ToolCondition.Good);
            await _loanRepository.InsertAsync(loan2, autoSave: true);

            var exception = await Should.ThrowAsync<BusinessException>(() => _loanManager.ReturnAsync(loan2, DateTime.UtcNow, ToolCondition.Damaged));
            exception.Code.ShouldBe(LendingDomainErrorCodes.MaintenanceRequestAlreadyOpen);
        }
    }
}
