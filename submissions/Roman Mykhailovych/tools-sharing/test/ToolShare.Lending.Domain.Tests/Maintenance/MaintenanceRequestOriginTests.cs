using System;
using Shouldly;
using ToolShare.Catalog;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 MAINT-04/MAINT-05 (FR-002, FR-015, FR-016): every request has exactly
/// one origin — the pre-008 constructor yields a return-triggered request, the
/// <see cref="MaintenanceRequest.ReportOutOfBand"/> factory an out-of-band
/// one — and both close identically (MAINT-02, FR-012).
/// </summary>
public class MaintenanceRequestOriginTests
{
    private static readonly DateTime OpenedAt = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_existing_constructor_opens_a_return_triggered_request_linked_to_its_loan()
    {
        var loanId = Guid.NewGuid();

        var request = new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(), loanId, OpenedAt);

        request.Origin.ShouldBe(MaintenanceRequestOrigin.ReturnTriggered);
        request.TriggeringLoanId.ShouldBe(loanId);
        request.ReportedByMemberId.ShouldBeNull();
        request.ReportReason.ShouldBeNull();
        request.ObservedCondition.ShouldBeNull();
        request.Status.ShouldBe(MaintenanceRequestStatus.Open);
    }

    [Fact]
    public void ReportOutOfBand_opens_an_out_of_band_request_with_its_report_details()
    {
        var instanceId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var request = MaintenanceRequest.ReportOutOfBand(Guid.NewGuid(), instanceId, reporterId, "  Cracked blade guard  ", ToolCondition.Worn, OpenedAt);

        request.Origin.ShouldBe(MaintenanceRequestOrigin.OutOfBand);
        request.ToolInstanceId.ShouldBe(instanceId);
        request.TriggeringLoanId.ShouldBeNull();
        request.ReportedByMemberId.ShouldBe(reporterId);
        request.ReportReason.ShouldBe("Cracked blade guard");
        request.ObservedCondition.ShouldBe(ToolCondition.Worn);
        request.Status.ShouldBe(MaintenanceRequestStatus.Open);
        request.OpenedAt.ShouldBe(OpenedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReportOutOfBand_requires_a_reason(string? reason)
    {
        Should.Throw<BusinessException>(() =>
                MaintenanceRequest.ReportOutOfBand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reason!, ToolCondition.Good, OpenedAt))
            .Code.ShouldBe(LendingDomainErrorCodes.MaintenanceReasonRequired);
    }

    [Fact]
    public void ReportOutOfBand_accepts_a_reason_of_exactly_the_maximum_length()
    {
        var reason = new string('x', LendingDomainSharedConsts.MaintenanceReportReasonMaxLength);

        var request = MaintenanceRequest.ReportOutOfBand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reason, ToolCondition.Good, OpenedAt);

        request.ReportReason!.Length.ShouldBe(500);
    }

    [Fact]
    public void ReportOutOfBand_rejects_a_reason_longer_than_500_characters()
    {
        var reason = new string('x', LendingDomainSharedConsts.MaintenanceReportReasonMaxLength + 1);

        Should.Throw<ArgumentException>(() =>
            MaintenanceRequest.ReportOutOfBand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reason, ToolCondition.Good, OpenedAt));
    }

    [Fact]
    public void An_out_of_band_request_closes_with_zero_cost_like_any_other()
    {
        var request = MaintenanceRequest.ReportOutOfBand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Loose guard", ToolCondition.Good, OpenedAt);

        request.Close(OpenedAt.AddDays(1), 0m);

        request.Status.ShouldBe(MaintenanceRequestStatus.Closed);
        request.Cost.ShouldBe(0m);
    }

    [Fact]
    public void An_out_of_band_request_cannot_close_without_a_cost()
    {
        var request = MaintenanceRequest.ReportOutOfBand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Loose guard", ToolCondition.Good, OpenedAt);

        Should.Throw<BusinessException>(() => request.Close(OpenedAt.AddDays(1), null))
            .Code.ShouldBe(LendingDomainErrorCodes.MaintenanceCostRequired);
    }
}
