using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.Membership.Authorization;
using ToolShare.Membership.CommunityRules;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// Rolls a lapsed waitlist offer forward to the next waiting member (research
/// R4). Runs with no signed-in user, so it impersonates Membership's
/// well-known <see cref="SystemPrincipal"/> for the duration of its read of
/// the community rules — the enrolment gate's one explicit, narrow exemption.
/// </summary>
public class WaitlistOfferExpiryWorker : AsyncPeriodicBackgroundWorkerBase, ITransientDependency
{
    public WaitlistOfferExpiryWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 60_000; // 1 minute
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

    private static async Task ExecuteAsync(System.IServiceProvider serviceProvider)
    {
        var currentPrincipalAccessor = serviceProvider.GetRequiredService<ICurrentPrincipalAccessor>();

        using (currentPrincipalAccessor.Change(SystemPrincipal.Build()))
        {
            var waitlistEntryRepository = serviceProvider.GetRequiredService<IWaitlistEntryRepository>();
            var waitlistManager = serviceProvider.GetRequiredService<WaitlistManager>();
            var communityRulesLookupAppService = serviceProvider.GetRequiredService<ICommunityRulesLookupAppService>();
            var clock = serviceProvider.GetRequiredService<IClock>();

            var rules = await communityRulesLookupAppService.GetAsync();
            var now = clock.Now;

            var expiredOffers = await waitlistEntryRepository.GetExpiredOffersAsync(now);
            foreach (var entry in expiredOffers)
            {
                entry.Expire(now);
                await waitlistEntryRepository.UpdateAsync(entry, autoSave: true);
                await waitlistManager.OfferNextAsync(entry.ToolInstanceId, now, rules.WaitlistOfferWindowHours);
            }
        }
    }
}
