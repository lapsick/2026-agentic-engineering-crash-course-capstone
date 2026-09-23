using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.EntityFrameworkCore;
using ToolShare.Catalog.Tools;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Catalog.Repositories;

public class EfCoreCategoryRepository : EfCoreRepository<CatalogDbContext, Category, Guid>, ICategoryRepository
{
    public EfCoreCategoryRepository(IDbContextProvider<CatalogDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<Category?> FindByNormalizedNameAsync(string normalizedName, Guid? excludedId = null, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        var query = queryable.Where(c => c.NormalizedName == normalizedName);
        if (excludedId.HasValue)
        {
            query = query.Where(c => c.Id != excludedId.Value);
        }

        return await query.FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<bool> AnyToolAssignedAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.Set<Tool>().AnyAsync(t => t.CategoryId == categoryId, GetCancellationToken(cancellationToken));
    }

    public async Task<int> CountToolsAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.Set<Tool>().CountAsync(t => t.CategoryId == categoryId, GetCancellationToken(cancellationToken));
    }
}
