using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace ToolShare.Catalog.Categories;

/// <summary>ToolCount is not on the entity — the app service sets it after mapping.</summary>
[Mapper]
public partial class CategoryMapper : MapperBase<Category, CategoryDto>
{
    public override partial CategoryDto Map(Category source);

    public override partial void Map(Category source, CategoryDto destination);
}

[Mapper]
public partial class CategoryToLookupMapper : MapperBase<Category, CategoryLookupDto>
{
    public override partial CategoryLookupDto Map(Category source);

    public override partial void Map(Category source, CategoryLookupDto destination);
}
