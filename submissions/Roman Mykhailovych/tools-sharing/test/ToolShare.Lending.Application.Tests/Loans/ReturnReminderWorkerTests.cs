using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.CommunityRules;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>FR-022: generates a return reminder within rules.ReminderLeadTimeDays, idempotent via Loan.ReminderSentAt.</summary>
public class ReturnReminderWorkerTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly ILoanRepository _loanRepository;
    private readonly ICommunityRulesLookupAppService _communityRulesLookupAppService;
    private readonly ReturnReminderWorker _worker;

    public ReturnReminderWorkerTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _loanRepository = GetRequiredService<ILoanRepository>();
        _communityRulesLookupAppService = GetRequiredService<ICommunityRulesLookupAppService>();
        _worker = GetRequiredService<ReturnReminderWorker>();
    }

    [Fact]
    public async Task A_loan_returning_within_the_lead_time_gets_reminded_exactly_once()
    {
        var rules = await _communityRulesLookupAppService.GetAsync();
        var (_, _, instance) = await SeedCatalogDataAsync();

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var end = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(rules.ReminderLeadTimeDays - 1);

        LoanDto loan;
        using (AsLibrarian())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = end
            });

            loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
        }

        await _worker.ExecuteOnceAsync();

        var reminded = await _loanRepository.GetAsync(loan.Id);
        reminded.ReminderSentAt.ShouldNotBeNull();
        var firstReminderTime = reminded.ReminderSentAt!.Value;

        await _worker.ExecuteOnceAsync();

        var stillReminded = await _loanRepository.GetAsync(loan.Id);
        stillReminded.ReminderSentAt.ShouldBe(firstReminderTime);
    }

    [Fact]
    public async Task A_loan_returning_well_beyond_the_lead_time_is_left_untouched()
    {
        var rules = await _communityRulesLookupAppService.GetAsync();
        var (_, _, instance) = await SeedCatalogDataAsync();

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var end = start.AddDays(rules.MaxLoanTermDays);

        LoanDto loan;
        using (AsLibrarian())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = end
            });

            loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
        }

        await _worker.ExecuteOnceAsync();

        var untouched = await _loanRepository.GetAsync(loan.Id);
        untouched.ReminderSentAt.ShouldBeNull();
    }
}
