using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.EntityFrameworkCore;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Catalog.Tools;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Catalog.Repositories;

public class EfCoreToolRepository : EfCoreRepository<CatalogDbContext, Tool, Guid>, IToolRepository
{
    public EfCoreToolRepository(IDbContextProvider<CatalogDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<bool> AnyInstanceAssignedAsync(Guid toolId, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.Set<ToolInstance>().AnyAsync(i => i.ToolId == toolId, GetCancellationToken(cancellationToken));
    }

    public async Task<(Tool Tool, List<ToolInstance> Instances)?> GetWithInstancesAsync(
        Guid id,
        bool includeRetiredInstances = false,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var ct = GetCancellationToken(cancellationToken);

        var tool = await dbContext.Set<Tool>().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tool is null)
        {
            return null;
        }

        var instancesQuery = dbContext.Set<ToolInstance>()
            .Include(i => i.Photos)
            .Include(i => i.StateHistory)
            .Where(i => i.ToolId == id);

        if (!includeRetiredInstances)
        {
            instancesQuery = instancesQuery.Where(i => i.CirculationState != ToolInstanceCirculationState.Retired);
        }

        var instances = await instancesQuery.ToListAsync(ct);

        return (tool, instances);
    }

    public async Task<int> GetCountAsync(
        string? normalizedFilter = null,
        Guid? categoryId = null,
        bool onlyAvailable = false,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(normalizedFilter, categoryId, onlyAvailable);
        return await query.CountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<Tool>> GetPagedListAsync(
        string? normalizedFilter,
        Guid? categoryId,
        bool onlyAvailable,
        bool includeRetiredInstances,
        string sorting,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(normalizedFilter, categoryId, onlyAvailable);

        query = ApplySorting(query, sorting);

        return await query.Skip(skip).Take(take).ToListAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<Tool>> BuildFilteredQueryAsync(string? normalizedFilter, Guid? categoryId, bool onlyAvailable)
    {
        var dbContext = await GetDbContextAsync();
        var query = dbContext.Set<Tool>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedFilter))
        {
            query = query.Where(t => t.NormalizedName.Contains(normalizedFilter));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == categoryId.Value);
        }

        if (onlyAvailable)
        {
            query = query.Where(t => dbContext.Set<ToolInstance>().Any(i =>
                i.ToolId == t.Id &&
                i.CirculationState == ToolInstanceCirculationState.InCirculation &&
                i.Condition != ToolCondition.Damaged));
        }

        return query;
    }

    private static IQueryable<Tool> ApplySorting(IQueryable<Tool> query, string sorting)
    {
        var normalized = sorting?.Trim() ?? string.Empty;

        return normalized.ToLowerInvariant() switch
        {
            var s when s.StartsWith("categoryname desc") => query.OrderByDescending(t => t.CategoryId),
            var s when s.StartsWith("categoryname") => query.OrderBy(t => t.CategoryId),
            var s when s.StartsWith("creationtime desc") => query.OrderByDescending(t => t.CreationTime),
            var s when s.StartsWith("creationtime") => query.OrderBy(t => t.CreationTime),
            var s when s.StartsWith("name desc") => query.OrderByDescending(t => t.NormalizedName),
            _ => query.OrderBy(t => t.NormalizedName)
        };
    }
}
