using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// RES-04: a member with an open, overdue loan cannot reserve. Checkout
/// (US2) does not exist yet, so this test manipulates <see cref="Loan"/>
/// directly via its own module's repository to simulate the condition —
/// legitimate here since this file lives in Lending's own test suite, not a
/// PublicContract-style stand-in for another module.
/// </summary>
public class OverdueBlockTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanRepository _loanRepository;

    public OverdueBlockTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanRepository = GetRequiredService<ILoanRepository>();
    }

    [Fact]
    public async Task A_member_with_an_open_overdue_loan_cannot_reserve()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var standing = await GetRequiredService<Membership.Members.IMemberStandingAppService>()
            .GetByIdentityUserIdAsync(LendingTestPrincipals.DefaultTestRunnerId);

        var otherInstanceId = Guid.NewGuid();
        var reservation = new Reservation(
            Guid.NewGuid(), standing.MemberId!.Value, otherInstanceId,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10),
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5),
            maxLoanTermDays: 14, createdAt: DateTime.UtcNow.AddDays(-10));

        var loan = new Loan(Guid.NewGuid(), reservation, DateTime.UtcNow.AddDays(-10), Catalog.ToolCondition.Good);
        loan.MarkOverdue(DateTime.UtcNow);
        await _loanRepository.InsertAsync(loan, autoSave: true);

        var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = instance.Id,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3)
        }));

        exception.Code.ShouldBe("Lending:MemberHasOverdueLoan");
    }
}
