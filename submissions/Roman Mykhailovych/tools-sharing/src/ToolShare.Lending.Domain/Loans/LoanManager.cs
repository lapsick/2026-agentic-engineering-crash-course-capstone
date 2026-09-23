using System;
using System.Threading.Tasks;
using ToolShare.Catalog;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Reservations;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ToolShare.Lending.Loans;

/// <summary>
/// The rules that need repository access: LOAN-06's availability re-check at
/// checkout (the bool itself is supplied by the caller, sourced live from
/// Catalog) and orchestrating LOAN-04's maintenance-request opening —
/// including MAINT-01's pre-check — in the same operation as a worsened
/// return.
/// </summary>
public class LoanManager : DomainService
{
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;

    public LoanManager(IMaintenanceRequestRepository maintenanceRequestRepository)
    {
        _maintenanceRequestRepository = maintenanceRequestRepository;
    }

    /// <summary>Enforces LOAN-06 before delegating to <see cref="Loan"/>'s own LOAN-01/LOAN-02 guard.</summary>
    public Task<Loan> CheckOutAsync(Reservation reservation, bool isInstanceAvailable, DateTime checkedOutAt, ToolCondition conditionAtCheckout)
    {
        if (!isInstanceAvailable)
        {
            throw new BusinessException(LendingDomainErrorCodes.InstanceUnavailable);
        }

        return Task.FromResult(new Loan(GuidGenerator.Create(), reservation, checkedOutAt, conditionAtCheckout));
    }

    /// <summary>
    /// Enforces LOAN-03 (via <see cref="Loan.Return"/>) and, on a worsened
    /// return (LOAN-04), MAINT-01's pre-check before opening exactly one
    /// <see cref="MaintenanceRequest"/> — the filtered unique index remains
    /// the authority under concurrency. Returns <c>null</c> when the return
    /// was not worsened.
    /// </summary>
    public async Task<MaintenanceRequest?> ReturnAsync(Loan loan, DateTime returnedAt, ToolCondition returnedCondition)
    {
        loan.Return(returnedAt, returnedCondition);

        if (!loan.IsWorsened())
        {
            return null;
        }

        var existingOpen = await _maintenanceRequestRepository.GetOpenForInstanceAsync(loan.ToolInstanceId);
        if (existingOpen is not null)
        {
            throw new BusinessException(LendingDomainErrorCodes.MaintenanceRequestAlreadyOpen);
        }

        return new MaintenanceRequest(GuidGenerator.Create(), loan.ToolInstanceId, loan.Id, returnedAt);
    }
}
