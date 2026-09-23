using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Catalog.Tools;

public class ToolDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = default!;
    public string ConcurrencyStamp { get; set; } = default!;
}
