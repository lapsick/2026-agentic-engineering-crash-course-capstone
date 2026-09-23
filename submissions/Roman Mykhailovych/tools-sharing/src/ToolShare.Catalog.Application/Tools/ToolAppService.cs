using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Permissions;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Catalog.Tools;

[Authorize]
public class ToolAppService : ApplicationService, IToolAppService
{
    private readonly IToolRepository _toolRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IRepository<ToolInstance, Guid> _toolInstanceRepository;

    public ToolAppService(
        IToolRepository toolRepository,
        ICategoryRepository categoryRepository,
        IRepository<ToolInstance, Guid> toolInstanceRepository)
    {
        _toolRepository = toolRepository;
        _categoryRepository = categoryRepository;
        _toolInstanceRepository = toolInstanceRepository;
    }

    public virtual async Task<ToolDetailDto> GetAsync(Guid id, bool includeRetiredInstances = false)
    {
        var result = await _toolRepository.GetWithInstancesAsync(id, includeRetiredInstances)
            ?? throw new EntityNotFoundException(typeof(Tool), id);

        var category = await _categoryRepository.GetAsync(result.Tool.CategoryId);

        var dto = ObjectMapper.Map<Tool, ToolDetailDto>(result.Tool);
        dto.CategoryName = category.Name;
        dto.Instances = result.Instances
            .Select(i => ObjectMapper.Map<ToolInstance, ToolInstanceDto>(i))
            .ToList();

        return dto;
    }

    public virtual async Task<PagedResultDto<ToolListItemDto>> GetListAsync(GetToolListInput input)
    {
        var normalizedFilter = string.IsNullOrWhiteSpace(input.Filter) ? null : CatalogTextNormalizer.Normalize(input.Filter);

        var totalCount = await _toolRepository.GetCountAsync(normalizedFilter, input.CategoryId, input.OnlyAvailable);
        var tools = await _toolRepository.GetPagedListAsync(
            normalizedFilter,
            input.CategoryId,
            input.OnlyAvailable,
            input.IncludeRetiredInstances,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        var categories = (await _categoryRepository.GetListAsync())
            .ToDictionary(c => c.Id, c => c.Name);

        var toolIds = tools.Select(t => t.Id).ToList();
        var instanceCounts = await GetInstanceCountsAsync(toolIds, input.IncludeRetiredInstances);

        var dtos = tools.Select(tool =>
        {
            var (total, available) = instanceCounts.TryGetValue(tool.Id, out var counts) ? counts : (0, 0);
            return new ToolListItemDto
            {
                Id = tool.Id,
                Name = tool.Name,
                CategoryId = tool.CategoryId,
                CategoryName = categories.TryGetValue(tool.CategoryId, out var name) ? name : string.Empty,
                InstanceCount = total,
                AvailableInstanceCount = available
            };
        }).ToList();

        return new PagedResultDto<ToolListItemDto>(totalCount, dtos);
    }

    private async Task<Dictionary<Guid, (int Total, int Available)>> GetInstanceCountsAsync(List<Guid> toolIds, bool includeRetiredInstances)
    {
        if (toolIds.Count == 0)
        {
            return new Dictionary<Guid, (int, int)>();
        }

        var queryable = await _toolInstanceRepository.GetQueryableAsync();
        var grouped = await AsyncExecuter.ToListAsync(
            queryable
                .Where(i => toolIds.Contains(i.ToolId))
                .GroupBy(i => i.ToolId)
                .Select(g => new
                {
                    ToolId = g.Key,
                    Total = includeRetiredInstances
                        ? g.Count()
                        : g.Count(i => i.CirculationState != ToolInstanceCirculationState.Retired),
                    Available = g.Count(i =>
                        i.CirculationState == ToolInstanceCirculationState.InCirculation &&
                        i.Condition != ToolCondition.Damaged)
                }));

        return grouped.ToDictionary(x => x.ToolId, x => (x.Total, x.Available));
    }

    [Authorize(CatalogPermissions.Tools.Create)]
    public virtual async Task<ToolDto> CreateAsync(CreateToolDto input)
    {
        var category = await _categoryRepository.FindAsync(input.CategoryId)
            ?? throw new BusinessException("Catalog:CategoryNotFound");

        var tool = new Tool(GuidGenerator.Create(), category.Id, input.Name, input.Description);
        await _toolRepository.InsertAsync(tool);

        return await MapToDtoAsync(tool, category);
    }

    [Authorize(CatalogPermissions.Tools.Edit)]
    public virtual async Task<ToolDto> UpdateAsync(Guid id, UpdateToolDto input)
    {
        var tool = await _toolRepository.GetAsync(id);
        tool.ConcurrencyStamp = input.ConcurrencyStamp;

        var category = await _categoryRepository.FindAsync(input.CategoryId)
            ?? throw new BusinessException("Catalog:CategoryNotFound");

        tool.SetName(input.Name);
        tool.SetDescription(input.Description);
        tool.ChangeCategory(category.Id);

        // autoSave: see CategoryAppService.UpdateAsync for why this must not be deferred.
        await _toolRepository.UpdateAsync(tool, autoSave: true);

        return await MapToDtoAsync(tool, category);
    }

    [Authorize(CatalogPermissions.Tools.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var tool = await _toolRepository.GetAsync(id);

        if (await _toolRepository.AnyInstanceAssignedAsync(tool.Id))
        {
            throw new BusinessException("Catalog:ToolHasInstances");
        }

        await _toolRepository.DeleteAsync(tool);
    }

    private async Task<ToolDto> MapToDtoAsync(Tool tool, Category? category = null)
    {
        category ??= await _categoryRepository.GetAsync(tool.CategoryId);
        var dto = ObjectMapper.Map<Tool, ToolDto>(tool);
        dto.CategoryName = category.Name;
        return dto;
    }
}
