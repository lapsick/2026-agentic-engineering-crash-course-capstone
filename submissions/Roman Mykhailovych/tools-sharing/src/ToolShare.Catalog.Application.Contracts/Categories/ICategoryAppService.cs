using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Catalog.Categories;

public interface ICategoryAppService : IApplicationService
{
    Task<CategoryDto> GetAsync(Guid id);

    Task<PagedResultDto<CategoryDto>> GetListAsync(GetCategoryListInput input);

    Task<ListResultDto<CategoryLookupDto>> GetLookupAsync();

    Task<CategoryDto> CreateAsync(CreateCategoryDto input);

    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryDto input);

    Task DeleteAsync(Guid id);
}
