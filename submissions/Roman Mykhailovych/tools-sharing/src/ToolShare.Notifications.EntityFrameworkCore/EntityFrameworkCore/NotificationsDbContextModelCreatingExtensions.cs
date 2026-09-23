using Microsoft.EntityFrameworkCore;
using ToolShare.Notifications.Notifications;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ToolShare.Notifications.EntityFrameworkCore;

public static class NotificationsDbContextModelCreatingExtensions
{
    public static void ConfigureNotifications(this ModelBuilder builder)
    {
        builder.Entity<Notification>(b =>
        {
            b.ToTable(NotificationsDbProperties.DbTablePrefix + "Notifications", NotificationsDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.DisplayText).HasMaxLength(NotificationsDomainSharedConsts.DisplayTextMaxLength);

            // FR-012's own-inbox listing, most recent first.
            b.HasIndex(x => new { x.MemberId, x.CreatedAt });

            // NOTIF-01's dedup key — split into two filtered unique indexes
            // rather than one covering all four columns, because a plain
            // multi-column unique index treats NULL as distinct from NULL
            // (standard SQL semantics), so a single index would never catch a
            // duplicate for either source (OriginatingChangedAt is always
            // NULL for the two Lending-sourced kinds, and OriginatingLoanId is
            // always NULL for the three Membership-sourced kinds) — the same
            // "filtered unique index" idiom WaitlistEntries/MaintenanceRequests
            // already use in Lending's schema.
            b.HasIndex(x => new { x.MemberId, x.Kind, x.OriginatingLoanId })
                .IsUnique()
                .HasFilter("\"OriginatingLoanId\" IS NOT NULL");

            b.HasIndex(x => new { x.MemberId, x.Kind, x.OriginatingChangedAt })
                .IsUnique()
                .HasFilter("\"OriginatingChangedAt\" IS NOT NULL");

            b.HasMany(x => x.DeliveryRecords)
                .WithOne()
                .HasForeignKey(x => x.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);

            // No FK to Lending's or Membership's schema (Constitution III) —
            // OriginatingLoanId/OriginatingToolInstanceId/MemberId are plain columns.
        });

        builder.Entity<NotificationDeliveryRecord>(b =>
        {
            b.ToTable(NotificationsDbProperties.DbTablePrefix + "NotificationDeliveryRecords", NotificationsDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.FailureDetail).HasMaxLength(NotificationsDomainSharedConsts.FailureDetailMaxLength);

            b.HasIndex(x => x.NotificationId);
        });
    }
}
