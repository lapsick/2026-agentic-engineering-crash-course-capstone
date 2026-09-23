using System;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.Lending.EntityFrameworkCore;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Repositories;
using ToolShare.Lending.Reservations;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;

namespace ToolShare.Lending;

[DependsOn(
    typeof(LendingDomainModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule)
    )]
public class LendingEntityFrameworkCoreModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpClockOptions>(options =>
        {
            options.Kind = DateTimeKind.Utc;
        });

        context.Services.AddAbpDbContext<LendingDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
            options.AddRepository<Reservation, EfCoreReservationRepository>();
            options.AddRepository<WaitlistEntry, EfCoreWaitlistEntryRepository>();
            options.AddRepository<Loan, EfCoreLoanRepository>();
            options.AddRepository<MaintenanceRequest, EfCoreMaintenanceRequestRepository>();
        });
    }
}
