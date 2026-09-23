using System;
using System.Collections.Generic;

namespace ToolShare.Catalog.ToolInstances;

public class ToolInstanceDetailDto : ToolInstanceDto
{
    public string ToolName { get; set; } = default!;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = default!;
    public List<ToolInstancePhotoDto> Photos { get; set; } = new();
    public List<ToolInstanceStateChangeDto> History { get; set; } = new();
}
