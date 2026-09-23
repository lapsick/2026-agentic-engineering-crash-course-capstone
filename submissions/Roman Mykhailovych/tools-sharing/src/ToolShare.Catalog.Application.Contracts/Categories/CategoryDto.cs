using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Catalog.Categories;

public class CategoryDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public int ToolCount { get; set; }
    public string ConcurrencyStamp { get; set; } = default!;
}
