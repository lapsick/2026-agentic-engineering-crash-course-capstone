using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Catalog.Categories;

public class CategoryLookupDto : EntityDto<Guid>
{
    public string Name { get; set; } = default!;
}
