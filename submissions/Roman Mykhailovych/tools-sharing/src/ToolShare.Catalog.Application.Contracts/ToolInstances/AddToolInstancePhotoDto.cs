using Volo.Abp.Content;

namespace ToolShare.Catalog.ToolInstances;

public class AddToolInstancePhotoDto
{
    public IRemoteStreamContent File { get; set; } = default!;
}
