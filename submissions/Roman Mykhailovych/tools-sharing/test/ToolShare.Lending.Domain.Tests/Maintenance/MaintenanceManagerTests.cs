using System;
using Shouldly;
using ToolShare.Catalog;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 MAINT-06/MAINT-07 (FR-001, FR-003, FR-004): the pure rule deciding
/// whether an instance may be reported out-of-band, given the Catalog facts
/// the caller supplies as plain values — no database, no other module.
/// </summary>
public class MaintenanceManagerTests
{
    [Theory]
    [InlineData(ToolCondition.Good, ToolCondition.Good)]
    [InlineData(ToolCondition.Good, ToolCondition.Worn)]
    [InlineData(ToolCondition.Good, ToolCondition.Damaged)]
    [InlineData(ToolCondition.New, ToolCondition.New)]
    [InlineData(ToolCondition.Damaged, ToolCondition.Damaged)]
    public void An_in_circulation_instance_with_no_open_request_may_be_reported_with_an_observed_condition_no_better_than_current(
        ToolCondition current,
        ToolCondition observed)
    {
        Should.NotThrow(() => MaintenanceManager.EnsureCanReportOutOfBand(
            ToolInstanceCirculationState.InCirculation,
            current,
            observed,
            hasOpenRequest: false));
    }

    // ---- US3 refusal matrix (research R7), checked in order ----

    [Theory]
    [InlineData(ToolInstanceCirculationState.Retired, false, LendingDomainErrorCodes.InstanceRetired)]
    [InlineData(ToolInstanceCirculationState.OnLoan, false, LendingDomainErrorCodes.InstanceOnLoanRecordAtReturn)]
    [InlineData(ToolInstanceCirculationState.UnderMaintenance, false, LendingDomainErrorCodes.MaintenanceRequestAlreadyOpen)]
    [InlineData(ToolInstanceCirculationState.InCirculation, true, LendingDomainErrorCodes.MaintenanceRequestAlreadyOpen)]
    public void An_instance_that_is_not_reportable_is_refused_with_a_cause_specific_code(
        ToolInstanceCirculationState state,
        bool hasOpenRequest,
        string expectedCode)
    {
        Should.Throw<BusinessException>(() => MaintenanceManager.EnsureCanReportOutOfBand(state, ToolCondition.Good, ToolCondition.Good, hasOpenRequest))
            .Code.ShouldBe(expectedCode);
    }

    [Fact]
    public void A_better_observed_condition_is_refused_and_names_the_current_condition()
    {
        var exception = Should.Throw<BusinessException>(() => MaintenanceManager.EnsureCanReportOutOfBand(
            ToolInstanceCirculationState.InCirculation, ToolCondition.Worn, ToolCondition.Good, hasOpenRequest: false));

        exception.Code.ShouldBe(LendingDomainErrorCodes.ObservedConditionBetterThanCurrent);
        exception.Data["current"].ShouldBe(ToolCondition.Worn);
    }

    [Fact]
    public void The_first_failing_rule_wins_retired_before_a_better_condition()
    {
        Should.Throw<BusinessException>(() => MaintenanceManager.EnsureCanReportOutOfBand(
                ToolInstanceCirculationState.Retired, ToolCondition.Worn, ToolCondition.New, hasOpenRequest: true))
            .Code.ShouldBe(LendingDomainErrorCodes.InstanceRetired);
    }

    [Fact]
    public void The_first_failing_rule_wins_on_loan_before_an_open_request()
    {
        Should.Throw<BusinessException>(() => MaintenanceManager.EnsureCanReportOutOfBand(
                ToolInstanceCirculationState.OnLoan, ToolCondition.Good, ToolCondition.Good, hasOpenRequest: true))
            .Code.ShouldBe(LendingDomainErrorCodes.InstanceOnLoanRecordAtReturn);
    }
}
