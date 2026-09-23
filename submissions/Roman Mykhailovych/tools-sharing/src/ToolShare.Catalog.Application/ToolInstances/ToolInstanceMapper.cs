using System.Linq;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace ToolShare.Catalog.ToolInstances;

[Mapper]
public partial class ToolInstanceMapper : MapperBase<ToolInstance, ToolInstanceDto>
{
    public override partial ToolInstanceDto Map(ToolInstance source);

    public override partial void Map(ToolInstance source, ToolInstanceDto destination);

    public override void AfterMap(ToolInstance source, ToolInstanceDto destination)
    {
        destination.PrimaryPhotoId = source.Photos.FirstOrDefault(p => p.IsPrimary)?.Id;
    }
}

[Mapper]
public partial class ToolInstanceToDetailMapper : MapperBase<ToolInstance, ToolInstanceDetailDto>
{
    public override partial ToolInstanceDetailDto Map(ToolInstance source);

    public override partial void Map(ToolInstance source, ToolInstanceDetailDto destination);

    public override void AfterMap(ToolInstance source, ToolInstanceDetailDto destination)
    {
        destination.PrimaryPhotoId = source.Photos.FirstOrDefault(p => p.IsPrimary)?.Id;
    }
}

[Mapper]
public partial class ToolInstancePhotoMapper : MapperBase<ToolInstancePhoto, ToolInstancePhotoDto>
{
    public override partial ToolInstancePhotoDto Map(ToolInstancePhoto source);

    public override partial void Map(ToolInstancePhoto source, ToolInstancePhotoDto destination);
}

[Mapper]
public partial class ToolInstanceStateChangeMapper : MapperBase<ToolInstanceStateChange, ToolInstanceStateChangeDto>
{
    public override partial ToolInstanceStateChangeDto Map(ToolInstanceStateChange source);

    public override partial void Map(ToolInstanceStateChange source, ToolInstanceStateChangeDto destination);
}
