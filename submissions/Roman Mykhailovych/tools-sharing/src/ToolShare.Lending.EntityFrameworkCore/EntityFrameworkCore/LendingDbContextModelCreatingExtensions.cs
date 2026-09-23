using Microsoft.EntityFrameworkCore;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Reservations;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ToolShare.Lending.EntityFrameworkCore;

public static class LendingDbContextModelCreatingExtensions
{
    public static void ConfigureLending(this ModelBuilder builder)
    {
        builder.Entity<Reservation>(b =>
        {
            b.ToTable(LendingDbProperties.DbTablePrefix + "Reservations", LendingDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.CancellationReason).HasMaxLength(LendingDomainSharedConsts.CancellationReasonMaxLength);

            // No FK to Catalog's or Membership's schema (Constitution III).
            b.HasIndex(x => x.MemberId);

            // RES-03's authority under concurrency is a PostgreSQL exclusion
            // constraint (research R3) added by raw SQL in the migration —
            // EF Core's fluent API has no first-class EXCLUDE USING gist
            // builder. This plain index supports the application-level
            // overlap pre-check and general instance-scoped lookups.
            b.HasIndex(x => new { x.ToolInstanceId, x.Status });
        });

        builder.Entity<WaitlistEntry>(b =>
        {
            b.ToTable(LendingDbProperties.DbTablePrefix + "WaitlistEntries", LendingDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => new { x.ToolInstanceId, x.JoinedAt });

            // WL-02: at most one unresolved (Waiting=0, Offered=1) entry per
            // member/instance pair.
            b.HasIndex(x => new { x.MemberId, x.ToolInstanceId })
                .IsUnique()
                .HasFilter("\"OfferState\" IN (0, 1)");
        });

        builder.Entity<Loan>(b =>
        {
            b.ToTable(LendingDbProperties.DbTablePrefix + "Loans", LendingDbProperties.DbSchema);
            b.ConfigureByConvention();

            // LOAN-06's pre-check: the open loan for an instance, if any.
            b.HasIndex(x => x.ToolInstanceId).HasFilter("\"ReturnedAt\" IS NULL");

            // RES-04/RES-05's inputs: a member's open loans.
            b.HasIndex(x => x.MemberId).HasFilter("\"ReturnedAt\" IS NULL");
        });

        builder.Entity<MaintenanceRequest>(b =>
        {
            b.ToTable(LendingDbProperties.DbTablePrefix + "MaintenanceRequests", LendingDbProperties.DbSchema);
            b.ConfigureByConvention();

            // MAINT-01: at most one open (Status = 0) request per instance.
            b.HasIndex(x => x.ToolInstanceId)
                .IsUnique()
                .HasFilter("\"Status\" = 0");
        });
    }
}
