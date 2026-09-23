using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Catalog.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Catalog.Categories;

[Authorize]
public class CategoryAppService : ApplicationService, ICategoryAppService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly CategoryManager _categoryManager;

    public CategoryAppService(ICategoryRepository categoryRepository, CategoryManager categoryManager)
    {
        _categoryRepository = categoryRepository;
        _categoryManager = categoryManager;
    }

    public virtual async Task<CategoryDto> GetAsync(Guid id)
    {
        var category = await _categoryRepository.GetAsync(id);
        return await MapToDtoWithToolCountAsync(category);
    }

    public virtual async Task<PagedResultDto<CategoryDto>> GetListAsync(GetCategoryListInput input)
    {
        var normalizedFilter = string.IsNullOrWhiteSpace(input.Filter) ? null : CatalogTextNormalizer.Normalize(input.Filter);

        var queryable = await _categoryRepository.GetQueryableAsync();
        if (normalizedFilter is not null)
        {
            queryable = queryable.Where(c => c.NormalizedName.Contains(normalizedFilter));
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var sorted = input.Sorting?.Trim().StartsWith("name desc", StringComparison.OrdinalIgnoreCase) == true
            ? queryable.OrderByDescending(c => c.NormalizedName)
            : queryable.OrderBy(c => c.NormalizedName);

        var categories = await AsyncExecuter.ToListAsync(sorted.Skip(input.SkipCount).Take(input.MaxResultCount));

        var dtos = new List<CategoryDto>();
        foreach (var category in categories)
        {
            dtos.Add(await MapToDtoWithToolCountAsync(category));
        }

        return new PagedResultDto<CategoryDto>(totalCount, dtos);
    }

    public virtual async Task<ListResultDto<CategoryLookupDto>> GetLookupAsync()
    {
        var categories = await _categoryRepository.GetListAsync();
        var dtos = categories.Select(c => ObjectMapper.Map<Category, CategoryLookupDto>(c)).ToList();
        return new ListResultDto<CategoryLookupDto>(dtos);
    }

    [Authorize(CatalogPermissions.Categories.Create)]
    public virtual async Task<CategoryDto> CreateAsync(CreateCategoryDto input)
    {
        var category = await _categoryManager.CreateAsync(input.Name, input.Description);
        await _categoryRepository.InsertAsync(category);
        return await MapToDtoWithToolCountAsync(category);
    }

    [Authorize(CatalogPermissions.Categories.Edit)]
    public virtual async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryDto input)
    {
        var category = await _categoryRepository.GetAsync(id);
        category.ConcurrencyStamp = input.ConcurrencyStamp;

        await _categoryManager.ChangeNameAsync(category, input.Name);
        category.SetDescription(input.Description);

        // autoSave: the returned DTO's ConcurrencyStamp must reflect the value
        // ABP regenerates on persist, not the (soon-to-be-stale) input stamp —
        // otherwise the caller's very next edit fails a concurrency check that
        // was never actually a conflict.
        await _categoryRepository.UpdateAsync(category, autoSave: true);
        return await MapToDtoWithToolCountAsync(category);
    }

    [Authorize(CatalogPermissions.Categories.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var category = await _categoryRepository.GetAsync(id);
        await _categoryManager.DeleteAsync(category);
    }

    private async Task<CategoryDto> MapToDtoWithToolCountAsync(Category category)
    {
        var dto = ObjectMapper.Map<Category, CategoryDto>(category);
        dto.ToolCount = await _categoryRepository.CountToolsAsync(category.Id);
        return dto;
    }
}
