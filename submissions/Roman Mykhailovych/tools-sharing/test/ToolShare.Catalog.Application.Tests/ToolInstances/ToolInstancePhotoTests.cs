using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Tools;
using Volo.Abp;
using Volo.Abp.Content;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

public class ToolInstancePhotoTests : CatalogApplicationTestBase
{
    private readonly IToolInstanceAppService _toolInstanceAppService;
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;

    public ToolInstancePhotoTests()
    {
        _toolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
    }

    private async Task<ToolInstanceDto> CreateInstanceAsync()
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer", CategoryId = category.Id });
        return await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.Good
        });
    }

    private static AddToolInstancePhotoDto CreatePhotoDto(string fileName, string contentType, int sizeBytes)
    {
        var stream = new MemoryStream(new byte[sizeBytes]);
        return new AddToolInstancePhotoDto
        {
            File = new RemoteStreamContent(stream, fileName, contentType)
        };
    }

    [Fact]
    public async Task Adding_a_photo_succeeds_and_the_first_becomes_primary()
    {
        var instance = await CreateInstanceAsync();

        var photo = await _toolInstanceAppService.AddPhotoAsync(instance.Id, CreatePhotoDto("front.jpg", "image/jpeg", 1024));

        photo.IsPrimary.ShouldBeTrue();

        var detail = await _toolInstanceAppService.GetAsync(instance.Id);
        detail.Photos.Count.ShouldBe(1);
        detail.PrimaryPhotoId.ShouldBe(photo.Id);
    }

    [Fact]
    public async Task Adding_an_unsupported_format_is_rejected()
    {
        var instance = await CreateInstanceAsync();

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _toolInstanceAppService.AddPhotoAsync(instance.Id, CreatePhotoDto("doc.txt", "text/plain", 1024)));

        exception.Code.ShouldBe("Catalog:UnsupportedPhotoFormat");
    }

    [Fact]
    public async Task Adding_an_oversized_photo_is_rejected()
    {
        var instance = await CreateInstanceAsync();

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _toolInstanceAppService.AddPhotoAsync(instance.Id, CreatePhotoDto("huge.jpg", "image/jpeg", 6 * 1024 * 1024)));

        exception.Code.ShouldBe("Catalog:PhotoTooLarge");
    }

    [Fact]
    public async Task Adding_more_than_the_maximum_photos_is_rejected()
    {
        var instance = await CreateInstanceAsync();

        for (var i = 0; i < 5; i++)
        {
            await _toolInstanceAppService.AddPhotoAsync(instance.Id, CreatePhotoDto($"photo-{i}.jpg", "image/jpeg", 1024));
        }

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _toolInstanceAppService.AddPhotoAsync(instance.Id, CreatePhotoDto("overflow.jpg", "image/jpeg", 1024)));

        exception.Code.ShouldBe("Catalog:TooManyPhotos");
    }

    [Fact]
    public async Task Setting_a_different_photo_as_primary_moves_the_flag()
    {
        var instance = await CreateInstanceAsync();
        var first = await _toolInstanceAppService.AddPhotoAsync(instance.Id, CreatePhotoDto("front.jpg", "image/jpeg", 1024));
        var second = await _toolInstanceAppService.AddPhotoAsync(instance.Id, CreatePhotoDto("back.jpg", "image/jpeg", 1024));

        await _toolInstanceAppService.SetPrimaryPhotoAsync(instance.Id, second.Id);

        var detail = await _toolInstanceAppService.GetAsync(instance.Id);
        detail.PrimaryPhotoId.ShouldBe(second.Id);
        first.ShouldNotBeNull();
    }

    [Fact]
    public async Task Deleting_a_photo_removes_it()
    {
        var instance = await CreateInstanceAsync();
        var photo = await _toolInstanceAppService.AddPhotoAsync(instance.Id, CreatePhotoDto("front.jpg", "image/jpeg", 1024));

        await _toolInstanceAppService.DeletePhotoAsync(instance.Id, photo.Id);

        var detail = await _toolInstanceAppService.GetAsync(instance.Id);
        detail.Photos.ShouldBeEmpty();
    }
}
