using Microsoft.EntityFrameworkCore;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.EntityFrameworkCore;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Catalog.Tools;
using ToolShare.Lending.EntityFrameworkCore;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.EntityFrameworkCore;
using ToolShare.Membership.Members;
using ToolShare.Notifications.EntityFrameworkCore;
using ToolShare.Notifications.Notifications;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace ToolShare.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ReplaceDbContext(typeof(ICatalogDbContext))]
[ReplaceDbContext(typeof(IMembershipDbContext))]
[ReplaceDbContext(typeof(ILendingDbContext))]
[ReplaceDbContext(typeof(INotificationsDbContext))]
[ConnectionStringName("Default")]
public class ToolShareDbContext :
    AbpDbContext<ToolShareDbContext>,
    IIdentityDbContext,
    ITenantManagementDbContext,
    ICatalogDbContext,
    IMembershipDbContext,
    ILendingDbContext,
    INotificationsDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */

    #region Entities from the modules

    /* Notice: We implemented IIdentityDbContext, ITenantManagementDbContext,
     * ICatalogDbContext and IMembershipDbContext and replaced them for this
     * DbContext. This allows you to perform JOIN queries for the entities of
     * these modules over the repositories easily, and — per
     * https://abp.io/docs/latest/framework/data/entity-framework-core/migrations
     * — makes this the single project whose Migrations folder is used for
     * `dotnet ef migrations add`, instead of every module maintaining its own
     * migration history.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    //Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }
    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }
    // Catalog
    public DbSet<Category> Categories { get; set; }
    public DbSet<Tool> Tools { get; set; }
    public DbSet<ToolInstance> ToolInstances { get; set; }
    public DbSet<ToolInstancePhoto> ToolInstancePhotos { get; set; }
    public DbSet<ToolInstanceStateChange> ToolInstanceStateChanges { get; set; }
    // Membership
    public DbSet<Member> Members { get; set; }
    public DbSet<MemberStandingChange> MemberStandingChanges { get; set; }
    public DbSet<Membership.CommunityRules.CommunityRules> CommunityRules { get; set; }
    // Lending
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<WaitlistEntry> WaitlistEntries { get; set; }
    public DbSet<Loan> Loans { get; set; }
    public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
    // Notifications
    public DbSet<Notification> Notifications { get; set; }

    #endregion

    public ToolShareDbContext(DbContextOptions<ToolShareDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureFeatureManagement();
        builder.ConfigureTenantManagement();
        builder.ConfigureCatalog();
        builder.ConfigureMembership();
        builder.ConfigureLending();
        builder.ConfigureNotifications();

        /* Configure your own tables/entities inside here */

        //builder.Entity<YourEntity>(b =>
        //{
        //    b.ToTable(ToolShareConsts.DbTablePrefix + "YourEntities", ToolShareConsts.DbSchema);
        //    b.ConfigureByConvention(); //auto configure for the base class props
        //    //...
        //});
    }
}
