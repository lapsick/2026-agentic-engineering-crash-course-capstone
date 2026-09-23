using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Permissions;
using ToolShare.Catalog.Tools;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;

namespace ToolShare.Catalog.ToolInstances;

[Authorize]
public class ToolInstanceAppService : ApplicationService, IToolInstanceAppService
{
    private readonly IToolInstanceRepository _toolInstanceRepository;
    private readonly IToolRepository _toolRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ToolInstanceManager _toolInstanceManager;
    private readonly IBlobContainer<ToolInstancePhotoContainer> _photoContainer;
    private readonly IOptions<CatalogPhotoOptions> _photoOptions;

    public ToolInstanceAppService(
        IToolInstanceRepository toolInstanceRepository,
        IToolRepository toolRepository,
        ICategoryRepository categoryRepository,
        ToolInstanceManager toolInstanceManager,
        IBlobContainer<ToolInstancePhotoContainer> photoContainer,
        IOptions<CatalogPhotoOptions> photoOptions)
    {
        _toolInstanceRepository = toolInstanceRepository;
        _toolRepository = toolRepository;
        _categoryRepository = categoryRepository;
        _toolInstanceManager = toolInstanceManager;
        _photoContainer = photoContainer;
        _photoOptions = photoOptions;
    }

    public virtual async Task<ToolInstanceDetailDto> GetAsync(Guid id)
    {
        var instance = await _toolInstanceRepository.GetWithDetailsAsync(id)
            ?? throw new EntityNotFoundException(typeof(ToolInstance), id);

        var tool = await _toolRepository.GetAsync(instance.ToolId);
        var category = await _categoryRepository.GetAsync(tool.CategoryId);

        var dto = ObjectMapper.Map<ToolInstance, ToolInstanceDetailDto>(instance);
        dto.ToolName = tool.Name;
        dto.CategoryId = category.Id;
        dto.CategoryName = category.Name;
        dto.Photos = instance.Photos
            .OrderBy(p => p.DisplayOrder)
            .Select(p => ObjectMapper.Map<ToolInstancePhoto, ToolInstancePhotoDto>(p))
            .ToList();
        dto.History = instance.StateHistory
            .OrderBy(h => h.ChangedAt)
            .Select(h => ObjectMapper.Map<ToolInstanceStateChange, ToolInstanceStateChangeDto>(h))
            .ToList();

        return dto;
    }

    public virtual async Task<ListResultDto<ToolInstanceDto>> GetListByToolAsync(Guid toolId, bool includeRetired = false)
    {
        var instances = await _toolInstanceRepository.GetListByToolIdAsync(toolId, includeRetired);
        var dtos = instances.Select(i => ObjectMapper.Map<ToolInstance, ToolInstanceDto>(i)).ToList();
        return new ListResultDto<ToolInstanceDto>(dtos);
    }

    [Authorize(CatalogPermissions.ToolInstances.Create)]
    public virtual async Task<ToolInstanceDto> CreateAsync(CreateToolInstanceDto input)
    {
        if (await _toolRepository.FindAsync(input.ToolId) is null)
        {
            throw new BusinessException("Catalog:ToolNotFound");
        }

        var instance = await _toolInstanceManager.CreateAsync(input.ToolId, input.SerialNumber, input.Condition, input.Notes);
        await _toolInstanceRepository.InsertAsync(instance);

        return ObjectMapper.Map<ToolInstance, ToolInstanceDto>(instance);
    }

    [Authorize(CatalogPermissions.ToolInstances.Edit)]
    public virtual async Task<ToolInstanceDto> UpdateAsync(Guid id, UpdateToolInstanceDto input)
    {
        var instance = await _toolInstanceRepository.GetAsync(id);
        instance.ConcurrencyStamp = input.ConcurrencyStamp;

        await _toolInstanceManager.ChangeSerialNumberAsync(instance, input.SerialNumber);
        instance.SetNotes(input.Notes);

        // autoSave: see CategoryAppService.UpdateAsync for why this must not be deferred —
        // the mapped DTO below must carry the post-persist ConcurrencyStamp, not the input one.
        await _toolInstanceRepository.UpdateAsync(instance, autoSave: true);

        return ObjectMapper.Map<ToolInstance, ToolInstanceDto>(instance);
    }

    [Authorize(CatalogPermissions.ToolInstances.ChangeCondition)]
    public virtual async Task<ToolInstanceDto> ChangeConditionAsync(Guid id, ChangeToolInstanceConditionDto input)
    {
        var instance = await _toolInstanceRepository.GetWithDetailsAsync(id)
            ?? throw new EntityNotFoundException(typeof(ToolInstance), id);
        instance.ConcurrencyStamp = input.ConcurrencyStamp;

        instance.ChangeCondition(input.Condition, input.Reason, Clock.Now, CurrentUser.Id);

        await _toolInstanceRepository.UpdateAsync(instance, autoSave: true);

        return ObjectMapper.Map<ToolInstance, ToolInstanceDto>(instance);
    }

    [Authorize(CatalogPermissions.ToolInstances.Retire)]
    public virtual async Task<ToolInstanceDto> RetireAsync(Guid id, RetireToolInstanceDto input)
    {
        var instance = await _toolInstanceRepository.GetWithDetailsAsync(id)
            ?? throw new EntityNotFoundException(typeof(ToolInstance), id);
        instance.ConcurrencyStamp = input.ConcurrencyStamp;

        instance.Retire(input.Reason, Clock.Now, CurrentUser.Id);

        await _toolInstanceRepository.UpdateAsync(instance, autoSave: true);

        return ObjectMapper.Map<ToolInstance, ToolInstanceDto>(instance);
    }

    public virtual async Task<ListResultDto<ToolInstanceStateChangeDto>> GetHistoryAsync(Guid id)
    {
        var instance = await _toolInstanceRepository.GetWithDetailsAsync(id)
            ?? throw new EntityNotFoundException(typeof(ToolInstance), id);

        var ordered = instance.StateHistory.OrderBy(h => h.ChangedAt).ToList();
        var dtos = ordered.Select(h => ObjectMapper.Map<ToolInstanceStateChange, ToolInstanceStateChangeDto>(h)).ToList();

        return new ListResultDto<ToolInstanceStateChangeDto>(dtos);
    }

    [Authorize(CatalogPermissions.ToolInstances.ManagePhotos)]
    public virtual async Task<ToolInstancePhotoDto> AddPhotoAsync(Guid id, AddToolInstancePhotoDto input)
    {
        var instance = await _toolInstanceRepository.GetWithDetailsAsync(id)
            ?? throw new EntityNotFoundException(typeof(ToolInstance), id);

        var options = _photoOptions.Value;
        var contentType = input.File.ContentType ?? string.Empty;
        var fileName = input.File.FileName ?? "photo";
        var sizeBytes = input.File.ContentLength ?? 0;

        var blobName = $"{GuidGenerator.Create():N}{Path.GetExtension(fileName)}";

        var photo = instance.AddPhoto(
            blobName,
            fileName,
            contentType,
            sizeBytes,
            options.AllowedContentTypes,
            options.MaxSizeBytes,
            options.MaxPerInstance);

        await using (var stream = input.File.GetStream())
        {
            await _photoContainer.SaveAsync(blobName, stream);
        }

        await _toolInstanceRepository.UpdateAsync(instance);

        return ObjectMapper.Map<ToolInstancePhoto, ToolInstancePhotoDto>(photo);
    }

    [Authorize(CatalogPermissions.ToolInstances.ManagePhotos)]
    public virtual async Task DeletePhotoAsync(Guid id, Guid photoId)
    {
        var instance = await _toolInstanceRepository.GetWithDetailsAsync(id)
            ?? throw new EntityNotFoundException(typeof(ToolInstance), id);

        var photo = instance.Photos.FirstOrDefault(p => p.Id == photoId);
        if (photo is null)
        {
            return;
        }

        var blobName = photo.BlobName;

        instance.RemovePhoto(photoId);
        await _toolInstanceRepository.UpdateAsync(instance);

        await _photoContainer.DeleteAsync(blobName);
    }

    [Authorize(CatalogPermissions.ToolInstances.ManagePhotos)]
    public virtual async Task SetPrimaryPhotoAsync(Guid id, Guid photoId)
    {
        var instance = await _toolInstanceRepository.GetWithDetailsAsync(id)
            ?? throw new EntityNotFoundException(typeof(ToolInstance), id);

        instance.SetPrimaryPhoto(photoId);
        await _toolInstanceRepository.UpdateAsync(instance);
    }

    public virtual async Task<IRemoteStreamContent> GetPhotoAsync(Guid id, Guid photoId)
    {
        var instance = await _toolInstanceRepository.GetWithDetailsAsync(id)
            ?? throw new EntityNotFoundException(typeof(ToolInstance), id);

        var photo = instance.Photos.FirstOrDefault(p => p.Id == photoId)
            ?? throw new EntityNotFoundException(typeof(ToolInstancePhoto), photoId);

        var stream = await _photoContainer.GetAsync(photo.BlobName);

        return new RemoteStreamContent(stream, photo.FileName, photo.ContentType);
    }
}
