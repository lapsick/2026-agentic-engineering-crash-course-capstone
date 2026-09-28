using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Uow;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 research R4 (FR-014): <see cref="IInstanceLock"/> is a transaction-scoped
/// advisory lock — callers locking the same instance serialize until the
/// holder's transaction ends, different instances don't contend, and a call
/// outside a transaction fails loudly instead of silently not locking.
/// </summary>
public class InstanceLockTests : LendingApplicationTestBase
{
    private static readonly TimeSpan HoldFor = TimeSpan.FromMilliseconds(600);

    private readonly IInstanceLock _instanceLock;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public InstanceLockTests()
    {
        _instanceLock = GetRequiredService<IInstanceLock>();
        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
    }

    [Fact]
    public async Task Two_transactions_locking_the_same_instance_run_one_after_the_other()
    {
        var instanceId = Guid.NewGuid();
        var clock = Stopwatch.StartNew();

        var first = Task.Run(() => HoldLockAsync(instanceId, clock));
        await Task.Delay(100);
        var second = Task.Run(() => HoldLockAsync(instanceId, clock));

        var firstWindow = await first;
        var secondWindow = await second;

        secondWindow.AcquiredAt.ShouldBeGreaterThanOrEqualTo(firstWindow.ReleasedAt - TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task Locking_different_instances_does_not_block()
    {
        var clock = Stopwatch.StartNew();

        var first = Task.Run(() => HoldLockAsync(Guid.NewGuid(), clock));
        await Task.Delay(100);
        var second = Task.Run(() => HoldLockAsync(Guid.NewGuid(), clock));

        var firstWindow = await first;
        var secondWindow = await second;

        secondWindow.AcquiredAt.ShouldBeLessThan(firstWindow.ReleasedAt);
    }

    [Fact]
    public async Task Locking_outside_a_transactional_unit_of_work_throws()
    {
        using var uow = _unitOfWorkManager.Begin(new AbpUnitOfWorkOptions(isTransactional: false), requiresNew: true);

        await Should.ThrowAsync<AbpException>(() => _instanceLock.LockInstanceAsync(Guid.NewGuid()));
    }

    private async Task<(TimeSpan AcquiredAt, TimeSpan ReleasedAt)> HoldLockAsync(Guid instanceId, Stopwatch clock)
    {
        using var uow = _unitOfWorkManager.Begin(new AbpUnitOfWorkOptions(isTransactional: true), requiresNew: true);

        await _instanceLock.LockInstanceAsync(instanceId);
        var acquiredAt = clock.Elapsed;

        await Task.Delay(HoldFor);

        var releasedAt = clock.Elapsed;
        await uow.CompleteAsync();

        return (acquiredAt, releasedAt);
    }
}
