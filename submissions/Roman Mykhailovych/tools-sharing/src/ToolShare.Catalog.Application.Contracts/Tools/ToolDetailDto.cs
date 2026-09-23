using System.Collections.Generic;
using ToolShare.Catalog.ToolInstances;

namespace ToolShare.Catalog.Tools;

public class ToolDetailDto : ToolDto
{
    public List<ToolInstanceDto> Instances { get; set; } = new();
}
