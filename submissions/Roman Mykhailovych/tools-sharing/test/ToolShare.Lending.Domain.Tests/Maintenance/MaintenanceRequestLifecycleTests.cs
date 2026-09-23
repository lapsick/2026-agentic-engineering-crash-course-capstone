using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>MAINT-02 — pure entity guards, no database.</summary>
public class MaintenanceRequestLifecycleTests
{
    private static readonly DateTime OpenedAt = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Close_with_a_positive_cost_succeeds()
    {
        var request = new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OpenedAt);

        request.Close(OpenedAt.AddDays(2), 45.50m);

        request.Status.ShouldBe(MaintenanceRequestStatus.Closed);
        request.Cost.ShouldBe(45.50m);
        request.ClosedAt.ShouldBe(OpenedAt.AddDays(2));
    }

    [Fact]
    public void Close_with_exactly_zero_cost_is_valid()
    {
        var request = new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OpenedAt);

        request.Close(OpenedAt.AddDays(1), 0m);

        request.Cost.ShouldBe(0m);
    }

    [Fact]
    public void Close_without_a_cost_throws()
    {
        var request = new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OpenedAt);

        Should.Throw<BusinessException>(() => request.Close(OpenedAt.AddDays(1), null))
            .Code.ShouldBe(LendingDomainErrorCodes.MaintenanceCostRequired);
    }

    [Fact]
    public void Close_with_a_negative_cost_throws()
    {
        var request = new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OpenedAt);

        Should.Throw<BusinessException>(() => request.Close(OpenedAt.AddDays(1), -1m))
            .Code.ShouldBe(LendingDomainErrorCodes.MaintenanceCostRequired);
    }

    [Fact]
    public void Close_is_permitted_only_once()
    {
        var request = new MaintenanceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OpenedAt);
        request.Close(OpenedAt.AddDays(1), 10m);

        Should.Throw<BusinessException>(() => request.Close(OpenedAt.AddDays(2), 20m));
    }
}
