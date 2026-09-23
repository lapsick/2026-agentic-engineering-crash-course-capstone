using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Catalog.ToolInstances;

public class ToolInstancePhotoDto : EntityDto<Guid>
{
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}
