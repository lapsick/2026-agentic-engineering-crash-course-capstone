using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Catalog.Tools;

public class ToolListItemDto : EntityDto<Guid>
{
    public string Name { get; set; } = default!;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = default!;
    public int InstanceCount { get; set; }
    public int AvailableInstanceCount { get; set; }
}
