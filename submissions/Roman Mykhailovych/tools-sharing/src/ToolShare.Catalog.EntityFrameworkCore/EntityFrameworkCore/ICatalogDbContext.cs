using Microsoft.EntityFrameworkCore;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Catalog.Tools;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Catalog.EntityFrameworkCore;

[ConnectionStringName(CatalogDbProperties.ConnectionStringName)]
public interface ICatalogDbContext : IEfCoreDbContext
{
    DbSet<Category> Categories { get; }

    DbSet<Tool> Tools { get; }

    DbSet<ToolInstance> ToolInstances { get; }

    DbSet<ToolInstancePhoto> ToolInstancePhotos { get; }

    DbSet<ToolInstanceStateChange> ToolInstanceStateChanges { get; }
}
