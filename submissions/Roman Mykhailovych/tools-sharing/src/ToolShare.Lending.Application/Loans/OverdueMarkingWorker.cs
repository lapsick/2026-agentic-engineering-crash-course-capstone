using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace ToolShare.Lending.Loans;

/// <summary>
/// FR-023/FR-025: marks a loan overdue once its planned return date has
/// passed unreturned, and raises the overdue notification. Touches only
/// Lending's own repository — no Membership/Catalog call, so no impersonation
/// is needed (unlike <c>WaitlistOfferExpiryWorker</c>/<c>ReturnReminderWorker</c>).
/// </summary>
public class OverdueMarkingWorker : AsyncPeriodicBackgroundWorkerBase, ITransientDependency
{
    public OverdueMarkingWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 3_600_000; // 1 hour
    }

    protected override Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        return ExecuteAsync(workerContext.ServiceProvider);
    }

    /// <summary>Callable directly by tests — creates its own scope, exactly like a real timer tick would.</summary>
    public async Task ExecuteOnceAsync()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        await ExecuteAsync(scope.ServiceProvider);
    }

    private static async Task ExecuteAsync(IServiceProvider serviceProvider)
    {
        var loanRepository = serviceProvider.GetRequiredService<ILoanRepository>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var now = clock.Now;

        var newlyOverdue = await loanRepository.GetNewlyOverdueAsync(now);
        foreach (var loan in newlyOverdue)
        {
            loan.MarkOverdue(now);
            loan.MarkOverdueNoticeSent(now);
            await loanRepository.UpdateAsync(loan, autoSave: true);
        }
    }
}
