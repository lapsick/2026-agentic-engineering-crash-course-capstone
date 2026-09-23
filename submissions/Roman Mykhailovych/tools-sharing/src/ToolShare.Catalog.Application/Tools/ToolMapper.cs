using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace ToolShare.Catalog.Tools;

/// <summary>CategoryName is not on the entity — the app service sets it after mapping.</summary>
[Mapper]
public partial class ToolMapper : MapperBase<Tool, ToolDto>
{
    public override partial ToolDto Map(Tool source);

    public override partial void Map(Tool source, ToolDto destination);
}

/// <summary>CategoryName and Instances are not on the entity — the app service sets them after mapping.</summary>
[Mapper]
public partial class ToolToDetailMapper : MapperBase<Tool, ToolDetailDto>
{
    public override partial ToolDetailDto Map(Tool source);

    public override partial void Map(Tool source, ToolDetailDto destination);
}
