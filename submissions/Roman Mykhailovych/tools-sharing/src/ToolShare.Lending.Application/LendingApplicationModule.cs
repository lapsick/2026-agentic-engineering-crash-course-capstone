using System.Threading.Tasks;
using ToolShare.Catalog;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using ToolShare.Membership;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;

namespace ToolShare.Lending;

[DependsOn(
    typeof(LendingDomainModule),
    typeof(LendingApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(CatalogApplicationContractsModule),
    typeof(MembershipApplicationContractsModule)
    )]
public class LendingApplicationModule : AbpModule
{
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<WaitlistOfferExpiryWorker>();
        await context.AddBackgroundWorkerAsync<ReturnReminderWorker>();
        await context.AddBackgroundWorkerAsync<OverdueMarkingWorker>();
    }
}
