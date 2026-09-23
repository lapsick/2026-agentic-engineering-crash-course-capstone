using Microsoft.EntityFrameworkCore;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ToolShare.Membership.EntityFrameworkCore;

public static class MembershipDbContextModelCreatingExtensions
{
    public static void ConfigureMembership(this ModelBuilder builder)
    {
        builder.Entity<Member>(b =>
        {
            b.ToTable(MembershipDbProperties.DbTablePrefix + "Members", MembershipDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.DisplayName).IsRequired().HasMaxLength(MembershipDomainSharedConsts.DisplayNameMaxLength);
            b.Property(x => x.Email).IsRequired().HasMaxLength(MembershipDomainSharedConsts.EmailMaxLength);
            b.Property(x => x.StatusChangeReason).HasMaxLength(MembershipDomainSharedConsts.StatusChangeReasonMaxLength);

            // No FK to public."AbpUsers" — Constitution III, the identity user
            // lives in another schema.
            b.HasIndex(x => x.IdentityUserId).IsUnique();
            b.HasIndex(x => new { x.Status, x.Role });
            b.HasIndex(x => x.Email);

            b.Ignore(x => x.IsActive);

            b.HasMany(x => x.StandingHistory)
                .WithOne()
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Metadata.FindNavigation(nameof(Member.StandingHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<MemberStandingChange>(b =>
        {
            b.ToTable(MembershipDbProperties.DbTablePrefix + "MemberStandingChanges", MembershipDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Reason).HasMaxLength(MembershipDomainSharedConsts.StatusChangeReasonMaxLength);

            // No FK to identity for the actor column — Constitution III.
            b.HasIndex(x => new { x.MemberId, x.ChangedAt });

            // HR-05/SC-009: one loan can legitimately produce both an
            // OverdueReturn and a DamagedReturn outcome, so the composite
            // (not OccurrenceId alone) is the idempotency authority.
            b.HasIndex(x => new { x.OccurrenceId, x.OutcomeType })
                .IsUnique()
                .HasFilter("\"OccurrenceId\" IS NOT NULL");
        });

        builder.Entity<CommunityRules.CommunityRules>(b =>
        {
            b.ToTable(MembershipDbProperties.DbTablePrefix + "CommunityRules", MembershipDbProperties.DbSchema);
            b.ConfigureByConvention();
        });
    }
}
