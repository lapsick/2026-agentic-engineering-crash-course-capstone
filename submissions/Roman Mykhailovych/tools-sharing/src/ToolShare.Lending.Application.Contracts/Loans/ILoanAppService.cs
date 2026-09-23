using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ToolShare.Catalog;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Lending.Loans;

/// <summary>
/// Checkout, return, and the roster view (US2, US4). Checkout/return require
/// <c>Lending.Loans.Checkout</c>/<c>.Return</c> (contracts/lending-permissions.md);
/// the roster view requires <c>Lending.Loans</c>.
/// </summary>
public interface ILoanAppService : IApplicationService
{
    Task<LoanDto> CheckOutAsync(CheckOutReservationDto input);

    Task<LoanDto> ReturnAsync(Guid loanId, RecordReturnDto input);

    Task<PagedResultDto<LoanDto>> GetListAsync(GetLoanListInput input);

    Task<LoanDto> GetAsync(Guid id);
}

public class CheckOutReservationDto
{
    [Required]
    public Guid ReservationId { get; set; }
}

public class RecordReturnDto
{
    [Required]
    public ToolCondition ReturnedCondition { get; set; }
}

public class GetLoanListInput : PagedAndSortedResultRequestDto
{
    public Guid? MemberId { get; set; }

    public bool OnlyOverdue { get; set; }
}

public class LoanDto : EntityDto<Guid>
{
    public Guid ReservationId { get; set; }

    public Guid MemberId { get; set; }

    public Guid ToolInstanceId { get; set; }

    public DateTime CheckedOutAt { get; set; }

    public DateOnly PlannedReturnDate { get; set; }

    public ToolCondition ConditionAtCheckout { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public ToolCondition? ReturnedCondition { get; set; }

    public bool IsOverdue { get; set; }
}
