using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.Membership.Authorization;
using ToolShare.Membership.CommunityRules;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace ToolShare.Lending.Loans;

/// <summary>
/// FR-022: generates a return-reminder notification ahead of the planned
/// return date, per Membership's configured lead time — read live, so it
/// impersonates Membership's well-known <see cref="SystemPrincipal"/> for the
/// duration of that read, mirroring <c>WaitlistOfferExpiryWorker</c>.
/// </summary>
public class ReturnReminderWorker : AsyncPeriodicBackgroundWorkerBase, ITransientDependency
{
    public ReturnReminderWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
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
        var currentPrincipalAccessor = serviceProvider.GetRequiredService<ICurrentPrincipalAccessor>();

        using (currentPrincipalAccessor.Change(SystemPrincipal.Build()))
        {
            var loanRepository = serviceProvider.GetRequiredService<ILoanRepository>();
            var communityRulesLookupAppService = serviceProvider.GetRequiredService<ICommunityRulesLookupAppService>();
            var clock = serviceProvider.GetRequiredService<IClock>();

            var rules = await communityRulesLookupAppService.GetAsync();
            var now = clock.Now;

            var approaching = await loanRepository.GetApproachingReminderAsync(now, rules.ReminderLeadTimeDays);
            foreach (var loan in approaching)
            {
                loan.MarkReminderSent(now);
                await loanRepository.UpdateAsync(loan, autoSave: true);
            }
        }
    }
}
