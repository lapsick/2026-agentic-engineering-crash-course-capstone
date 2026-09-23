using Microsoft.EntityFrameworkCore;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Reservations;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Lending.EntityFrameworkCore;

[ConnectionStringName(LendingDbProperties.ConnectionStringName)]
public interface ILendingDbContext : IEfCoreDbContext
{
    DbSet<Reservation> Reservations { get; }

    DbSet<WaitlistEntry> WaitlistEntries { get; }

    DbSet<Loan> Loans { get; }

    DbSet<MaintenanceRequest> MaintenanceRequests { get; }
}
