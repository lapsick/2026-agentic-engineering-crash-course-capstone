using Microsoft.EntityFrameworkCore;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Reservations;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Lending.EntityFrameworkCore;

[ConnectionStringName(LendingDbProperties.ConnectionStringName)]
public class LendingDbContext : AbpDbContext<LendingDbContext>, ILendingDbContext
{
    public DbSet<Reservation> Reservations { get; set; } = null!;
    public DbSet<WaitlistEntry> WaitlistEntries { get; set; } = null!;
    public DbSet<Loan> Loans { get; set; } = null!;
    public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; } = null!;

    public LendingDbContext(DbContextOptions<LendingDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(LendingDbProperties.DbSchema);

        builder.ConfigureLending();
    }
}
