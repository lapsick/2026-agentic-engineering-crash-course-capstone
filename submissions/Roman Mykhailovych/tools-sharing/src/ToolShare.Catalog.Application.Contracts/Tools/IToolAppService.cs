using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Catalog.Tools;

public interface IToolAppService : IApplicationService
{
    Task<ToolDetailDto> GetAsync(Guid id, bool includeRetiredInstances = false);

    Task<PagedResultDto<ToolListItemDto>> GetListAsync(GetToolListInput input);

    Task<ToolDto> CreateAsync(CreateToolDto input);

    Task<ToolDto> UpdateAsync(Guid id, UpdateToolDto input);

    Task DeleteAsync(Guid id);
}
