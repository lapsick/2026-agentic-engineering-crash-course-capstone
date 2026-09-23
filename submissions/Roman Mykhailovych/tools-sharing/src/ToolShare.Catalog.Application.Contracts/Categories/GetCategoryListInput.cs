using Volo.Abp.Application.Dtos;

namespace ToolShare.Catalog.Categories;

public class GetCategoryListInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
