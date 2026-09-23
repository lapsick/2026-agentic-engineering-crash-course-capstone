using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace ToolShare.Catalog.ToolInstances;

public interface IToolInstanceAppService : IApplicationService
{
    Task<ToolInstanceDetailDto> GetAsync(Guid id);

    Task<ListResultDto<ToolInstanceDto>> GetListByToolAsync(Guid toolId, bool includeRetired = false);

    Task<ToolInstanceDto> CreateAsync(CreateToolInstanceDto input);

    Task<ToolInstanceDto> UpdateAsync(Guid id, UpdateToolInstanceDto input);

    Task<ToolInstanceDto> ChangeConditionAsync(Guid id, ChangeToolInstanceConditionDto input);

    Task<ToolInstanceDto> RetireAsync(Guid id, RetireToolInstanceDto input);

    Task<ListResultDto<ToolInstanceStateChangeDto>> GetHistoryAsync(Guid id);

    Task<ToolInstancePhotoDto> AddPhotoAsync(Guid id, AddToolInstancePhotoDto input);

    Task DeletePhotoAsync(Guid id, Guid photoId);

    Task SetPrimaryPhotoAsync(Guid id, Guid photoId);

    Task<IRemoteStreamContent> GetPhotoAsync(Guid id, Guid photoId);
}
