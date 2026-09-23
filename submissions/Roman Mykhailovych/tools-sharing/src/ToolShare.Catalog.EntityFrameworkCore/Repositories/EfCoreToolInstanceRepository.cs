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

public class EfCoreToolInstanceRepository : EfCoreRepository<CatalogDbContext, ToolInstance, Guid>, IToolInstanceRepository
{
    public EfCoreToolInstanceRepository(IDbContextProvider<CatalogDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<ToolInstance?> FindByNormalizedSerialNumberAsync(string normalizedSerialNumber, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable
            .FirstOrDefaultAsync(i => i.NormalizedSerialNumber == normalizedSerialNumber, GetCancellationToken(cancellationToken));
    }

    public async Task<bool> AnyBySerialNumberAsync(string normalizedSerialNumber, Guid? excludedId = null, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        var query = queryable.Where(i => i.NormalizedSerialNumber == normalizedSerialNumber);
        if (excludedId.HasValue)
        {
            query = query.Where(i => i.Id != excludedId.Value);
        }

        return await query.AnyAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<ToolInstance>> GetListByToolIdAsync(Guid toolId, bool includeRetired = false, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var query = dbContext.Set<ToolInstance>()
            .Include(i => i.Photos)
            .Where(i => i.ToolId == toolId);

        if (!includeRetired)
        {
            query = query.Where(i => i.CirculationState != ToolInstanceCirculationState.Retired);
        }

        return await query.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<ToolInstance?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await dbContext.Set<ToolInstance>()
            .Include(i => i.Photos)
            .Include(i => i.StateHistory)
            .FirstOrDefaultAsync(i => i.Id == id, GetCancellationToken(cancellationToken));
    }

    public async Task<ToolInstanceLookupRow?> FindLookupAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await BuildLookupQueryAsync();
        return await query.FirstOrDefaultAsync(r => r.Id == id, GetCancellationToken(cancellationToken));
    }

    public async Task<List<ToolInstanceLookupRow>> GetLookupByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids as ICollection<Guid> ?? ids.ToList();
        if (idList.Count == 0)
        {
            return new List<ToolInstanceLookupRow>();
        }

        var query = await BuildLookupQueryAsync();
        return await query.Where(r => idList.Contains(r.Id)).ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<int> GetLookupCountAsync(
        Guid? toolId,
        Guid? categoryId,
        string? normalizedSerialNumberFilter,
        ToolCondition? condition,
        bool onlyAvailable,
        bool includeRetired,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredLookupQueryAsync(toolId, categoryId, normalizedSerialNumberFilter, condition, onlyAvailable, includeRetired);
        return await query.CountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<ToolInstanceLookupRow>> GetLookupListAsync(
        Guid? toolId,
        Guid? categoryId,
        string? normalizedSerialNumberFilter,
        ToolCondition? condition,
        bool onlyAvailable,
        bool includeRetired,
        string sorting,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredLookupQueryAsync(toolId, categoryId, normalizedSerialNumberFilter, condition, onlyAvailable, includeRetired);
        query = ApplyLookupSorting(query, sorting);

        return await query.Skip(skip).Take(take).ToListAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<ToolInstanceLookupRow>> BuildLookupQueryAsync()
    {
        var dbContext = await GetDbContextAsync();

        return
            from i in dbContext.Set<ToolInstance>()
            join t in dbContext.Set<Tool>() on i.ToolId equals t.Id
            join c in dbContext.Set<Category>() on t.CategoryId equals c.Id
            select new ToolInstanceLookupRow
            {
                Id = i.Id,
                ToolId = t.Id,
                ToolName = t.Name,
                CategoryId = c.Id,
                CategoryName = c.Name,
                SerialNumber = i.SerialNumber,
                Condition = i.Condition,
                CirculationState = i.CirculationState,
                IsAvailable = i.CirculationState == ToolInstanceCirculationState.InCirculation && i.Condition != ToolCondition.Damaged
            };
    }

    private async Task<IQueryable<ToolInstanceLookupRow>> BuildFilteredLookupQueryAsync(
        Guid? toolId,
        Guid? categoryId,
        string? normalizedSerialNumberFilter,
        ToolCondition? condition,
        bool onlyAvailable,
        bool includeRetired)
    {
        var query = await BuildLookupQueryAsync();

        if (toolId.HasValue)
        {
            query = query.Where(r => r.ToolId == toolId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(r => r.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSerialNumberFilter))
        {
            // Contains match against the normalized serial number, so the caller
            // must have already run it through CatalogTextNormalizer — this
            // repository stores/queries only normalized values (see
            // ToolInstance.NormalizedSerialNumber). Joined via a second query
            // against ToolInstance because ToolInstanceLookupRow doesn't carry it.
            var dbContext = await GetDbContextAsync();
            var matchingIds = dbContext.Set<ToolInstance>()
                .Where(i => i.NormalizedSerialNumber.Contains(normalizedSerialNumberFilter))
                .Select(i => i.Id);
            query = query.Where(r => matchingIds.Contains(r.Id));
        }

        if (condition.HasValue)
        {
            query = query.Where(r => r.Condition == condition.Value);
        }

        if (onlyAvailable)
        {
            query = query.Where(r => r.IsAvailable);
        }

        if (!includeRetired)
        {
            query = query.Where(r => r.CirculationState != ToolInstanceCirculationState.Retired);
        }

        return query;
    }

    private static IQueryable<ToolInstanceLookupRow> ApplyLookupSorting(IQueryable<ToolInstanceLookupRow> query, string sorting)
    {
        var normalized = sorting.Trim();

        return normalized.ToLowerInvariant() switch
        {
            var s when s.StartsWith("condition desc") => query.OrderByDescending(r => r.Condition),
            var s when s.StartsWith("condition") => query.OrderBy(r => r.Condition),
            var s when s.StartsWith("circulationstate desc") => query.OrderByDescending(r => r.CirculationState),
            var s when s.StartsWith("circulationstate") => query.OrderBy(r => r.CirculationState),
            var s when s.StartsWith("creationtime desc") => query.OrderByDescending(r => r.Id),
            var s when s.StartsWith("creationtime") => query.OrderBy(r => r.Id),
            var s when s.StartsWith("serialnumber desc") => query.OrderByDescending(r => r.SerialNumber),
            _ => query.OrderBy(r => r.SerialNumber)
        };
    }
}
    