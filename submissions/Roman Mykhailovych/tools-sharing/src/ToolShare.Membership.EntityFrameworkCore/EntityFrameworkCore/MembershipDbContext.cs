using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Membership.EntityFrameworkCore;

[ConnectionStringName(MembershipDbProperties.ConnectionStringName)]
public class MembershipDbContext : AbpDbContext<MembershipDbContext>, IMembershipDbContext
{
    public DbSet<Member> Members { get; set; } = null!;
    public DbSet<MemberStandingChange> MemberStandingChanges { get; set; } = null!;
    public DbSet<CommunityRules.CommunityRules> CommunityRules { get; set; } = null!;

    public MembershipDbContext(DbContextOptions<MembershipDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(MembershipDbProperties.DbSchema);

        builder.ConfigureMembership();
    }

    /// <summary>
    /// Defense-in-depth for HR-01/FR-020 (Constitution IV). <see cref="MemberStandingChange"/>
    /// is append-only by construction — private setters, no update/delete
    /// method on the entity, and no repository or application-service path
    /// that ever calls one (see the entity and <see cref="Members.MemberManager"/>)
    /// — but none of that stops a caller who reaches for this <c>DbContext</c>
    /// directly and bypasses the property setters via
    /// <c>EntityEntry.Property(...).CurrentValue</c> or <c>DbSet.Remove(...)</c>.
    /// This override is the actual enforcement point that makes the guarantee
    /// hold even against that bypass.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureStandingHistoryIsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureStandingHistoryIsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureStandingHistoryIsAppendOnly()
    {
        var violation = ChangeTracker.Entries<MemberStandingChange>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (violation is not null)
        {
            throw new InvalidOperationException(
                $"MemberStandingChange rows are append-only (Constitution IV, HR-01/FR-020) — {violation.State} is not permitted.");
        }
    }
}
