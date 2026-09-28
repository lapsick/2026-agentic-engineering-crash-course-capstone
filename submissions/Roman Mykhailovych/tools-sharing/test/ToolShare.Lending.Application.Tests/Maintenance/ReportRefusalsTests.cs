using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using Volo.Abp;
using Volo.Abp.Validation;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 US3 (FR-002–FR-005, SC-004, MAINT-05–MAINT-07): each refused report
/// carries a cause-specific code and leaves no trace — no maintenance request,
/// no Catalog condition/circulation/history change, no reservation
/// cancellation, no waitlist change.
/// </summary>
public class ReportRefusalsTests : LendingAuthorizationTestBase
{
    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;

    public ReportRefusalsTests()
    {
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _maintenanceRequestRepository = GetRequiredService<IMaintenanceRequestRepository>();
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _waitlistEntryRepository = GetRequiredService<IWaitlistEntryRepository>();
        _toolInstanceLookupAppService = GetRequiredService<IToolInstanceLookupAppService>();
    }

    [Fact]
    public async Task An_instance_on_loan_is_refused_and_its_damage_is_left_to_the_return()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        await CheckOutAsync(instance.Id);

        await AssertRefusedWithoutTraceAsync(instance.Id, ToolCondition.Worn, "Cracked",
            LendingDomainErrorCodes.InstanceOnLoanRecordAtReturn);
    }

    [Fact]
    public async Task A_retired_instance_is_refused()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        using (Impersonate(await BuildAdminPrincipalAsync()))
        {
            var fresh = await ToolInstanceAppService.GetAsync(instance.Id);
            await ToolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto
            {
                Reason = "Beyond repair",
                ConcurrencyStamp = fresh.ConcurrencyStamp
            });
        }

        await AssertRefusedWithoutTraceAsync(instance.Id, ToolCondition.Worn, "Cracked",
            LendingDomainErrorCodes.InstanceRetired);
    }

    [Fact]
    public async Task An_instance_with_an_open_return_triggered_request_is_refused()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var loan = await CheckOutAsync(instance.Id);
        using (AsLibrarian())
        {
            await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Worn });
        }

        await AssertRefusedWithoutTraceAsync(instance.Id, ToolCondition.Worn, "Also cracked",
            LendingDomainErrorCodes.MaintenanceRequestAlreadyOpen);
    }

    [Fact]
    public async Task An_instance_with_an_open_out_of_band_request_is_refused()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        using (AsLibrarian())
        {
            await _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = instance.Id,
                ObservedCondition = ToolCondition.Good,
                Reason = "Loose guard"
            });
        }

        await AssertRefusedWithoutTraceAsync(instance.Id, ToolCondition.Worn, "Also cracked",
            LendingDomainErrorCodes.MaintenanceRequestAlreadyOpen);
    }

    [Fact]
    public async Task A_better_observed_condition_is_refused()
    {
        var (_, _, instance) = await SeedCatalogDataAsync(); // Good

        await AssertRefusedWithoutTraceAsync(instance.Id, ToolCondition.New, "Cracked",
            LendingDomainErrorCodes.ObservedConditionBetterThanCurrent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_missing_reason_is_refused(string reason)
    {
        var (_, _, instance) = await SeedCatalogDataAsync();

        await AssertRefusedWithoutTraceAsync<AbpValidationException>(instance.Id, ToolCondition.Worn, reason);
    }

    [Fact]
    public async Task A_reason_longer_than_500_characters_is_refused()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();

        await AssertRefusedWithoutTraceAsync<AbpValidationException>(instance.Id, ToolCondition.Worn,
            new string('x', LendingDomainSharedConsts.MaintenanceReportReasonMaxLength + 1));
    }

    [Fact]
    public async Task An_unknown_instance_is_refused()
    {
        using (AsLibrarian())
        {
            var exception = await Should.ThrowAsync<BusinessException>(() => _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = Guid.NewGuid(),
                ObservedCondition = ToolCondition.Good,
                Reason = "Cracked"
            }));
            exception.Code.ShouldBe(LendingDomainErrorCodes.InstanceUnavailable);
        }
    }

    private async Task<LoanDto> CheckOutAsync(Guid instanceId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (AsMemberWithNoGrants())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instanceId,
                StartDate = today,
                EndDate = today.AddDays(2)
            });

            using (AsLibrarian())
            {
                return await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
            }
        }
    }

    private async Task AssertRefusedWithoutTraceAsync(Guid instanceId, ToolCondition observed, string reason, string expectedCode)
    {
        var exception = await AssertRefusedWithoutTraceAsync<BusinessException>(instanceId, observed, reason);
        exception.Code.ShouldBe(expectedCode);
    }

    private async Task<TException> AssertRefusedWithoutTraceAsync<TException>(Guid instanceId, ToolCondition observed, string reason)
        where TException : Exception
    {
        var before = await SnapshotAsync(instanceId);

        TException exception;
        using (AsLibrarian())
        {
            exception = await Should.ThrowAsync<TException>(() => _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = instanceId,
                ObservedCondition = observed,
                Reason = reason
            }));
        }

        var after = await SnapshotAsync(instanceId);
        after.ShouldBe(before);

        return exception;
    }

    private async Task<string> SnapshotAsync(Guid instanceId)
    {
        var requestCount = await WithUnitOfWorkAsync(() => _maintenanceRequestRepository.GetCountAsync());
        var lookup = await _toolInstanceLookupAppService.FindAsync(instanceId);

        int historyCount;
        using (Impersonate(await BuildAdminPrincipalAsync()))
        {
            historyCount = (await ToolInstanceAppService.GetAsync(instanceId)).History.Count;
        }

        var reservations = (await _reservationAppService.GetListForInstanceAsync(instanceId)).Items
            .OrderBy(r => r.Id).Select(r => $"{r.Id}:{r.Status}");
        var waitlist = (await _waitlistEntryRepository.GetListAsync(e => e.ToolInstanceId == instanceId))
            .OrderBy(e => e.Id).Select(e => $"{e.Id}:{e.OfferState}");

        return $"requests={requestCount}; state={lookup!.CirculationState}; condition={lookup.Condition}; history={historyCount}; " +
               $"reservations=[{string.Join(",", reservations)}]; waitlist=[{string.Join(",", waitlist)}]";
    }
}
