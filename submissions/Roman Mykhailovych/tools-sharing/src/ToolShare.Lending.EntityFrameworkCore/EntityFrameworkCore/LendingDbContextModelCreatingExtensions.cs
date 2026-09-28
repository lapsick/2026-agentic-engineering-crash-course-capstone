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
            // 008 MAINT-04: exactly one origin shape per row — a return-triggered
            // request has a loan and no report fields; an out-of-band request
            // has all report fields and no loan. The migration adds "Origin"
            // with DEFAULT 0, so every pre-008 row reads as ReturnTriggered.
            b.ToTable(LendingDbProperties.DbTablePrefix + "MaintenanceRequests", LendingDbProperties.DbSchema, t =>
                t.HasCheckConstraint(
                    "CK_MaintenanceRequests_OriginShape",
                    "(\"Origin\" = 0 AND \"TriggeringLoanId\" IS NOT NULL AND \"ReportedByMemberId\" IS NULL AND \"ReportReason\" IS NULL AND \"ObservedCondition\" IS NULL) " +
                    "OR (\"Origin\" = 1 AND \"TriggeringLoanId\" IS NULL AND \"ReportedByMemberId\" IS NOT NULL AND \"ReportReason\" IS NOT NULL AND \"ObservedCondition\" IS NOT NULL)"));
            b.ConfigureByConvention();

            b.Property(x => x.ReportReason).HasMaxLength(LendingDomainSharedConsts.MaintenanceReportReasonMaxLength);

            // MAINT-01: at most one open (Status = 0) request per instance, of either origin.
            b.HasIndex(x => x.ToolInstanceId)
                .IsUnique()
                .HasFilter("\"Status\" = 0");
        });
    }
}
