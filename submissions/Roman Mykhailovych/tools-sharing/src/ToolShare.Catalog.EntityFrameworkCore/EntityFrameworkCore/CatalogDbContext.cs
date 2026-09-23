using Microsoft.EntityFrameworkCore;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Tools;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Catalog.EntityFrameworkCore;

[ConnectionStringName("Default")]
public class CatalogDbContext : AbpDbContext<CatalogDbContext>, ICatalogDbContext
{
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Tool> Tools { get; set; } = null!;
    public DbSet<ToolInstance> ToolInstances { get; set; } = null!;
    public DbSet<ToolInstancePhoto> ToolInstancePhotos { get; set; } = null!;
    public DbSet<ToolInstanceStateChange> ToolInstanceStateChanges { get; set; } = null!;

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(CatalogDbProperties.DbSchema);

        builder.ConfigureCatalog();
    }
}
