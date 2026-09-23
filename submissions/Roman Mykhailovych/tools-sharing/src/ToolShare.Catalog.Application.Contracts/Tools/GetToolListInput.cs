using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Catalog.Tools;

public class GetToolListInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }

    public Guid? CategoryId { get; set; }

    public bool OnlyAvailable { get; set; }

    public bool IncludeRetiredInstances { get; set; }
}
