using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Tools;
using Volo.Abp;
using Volo.Abp.Validation;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

public class RetireToolInstanceTests : CatalogApplicationTestBase
{
    private readonly IToolInstanceAppService _toolInstanceAppService;
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;

    public RetireToolInstanceTests()
    {
        _toolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
    }

    private async Task<(ToolDto Tool, ToolInstanceDto Instance)> CreateInstanceAsync()
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer", CategoryId = category.Id });
        var instance = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.Good
        });
        return (tool, instance);
    }

    [Fact]
    public async Task Retiring_with_a_reason_succeeds_and_preserves_the_record()
    {
        var (_, instance) = await CreateInstanceAsync();

        var retired = await _toolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto
        {
            Reason = "Motor burnt out",
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        retired.CirculationState.ShouldBe(ToolInstanceCirculationState.Retired);
        retired.IsAvailable.ShouldBeFalse();

        var stillRetrievable = await _toolInstanceAppService.GetAsync(instance.Id);
        stillRetrievable.SerialNumber.ShouldBe(instance.SerialNumber);
        stillRetrievable.CirculationState.ShouldBe(ToolInstanceCirculationState.Retired);
    }

    [Fact]
    public async Task Retiring_without_a_reason_is_rejected()
    {
        var (_, instance) = await CreateInstanceAsync();

        await Should.ThrowAsync<AbpValidationException>(() =>
            _toolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto
            {
                Reason = "",
                ConcurrencyStamp = instance.ConcurrencyStamp
            }));
    }

    [Fact]
    public async Task Retiring_an_already_retired_instance_is_rejected()
    {
        var (_, instance) = await CreateInstanceAsync();
        var retired = await _toolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto
        {
            Reason = "First retirement",
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _toolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto
            {
                Reason = "Second retirement",
                ConcurrencyStamp = retired.ConcurrencyStamp
            }));

        exception.Code.ShouldBe("Catalog:InstanceAlreadyRetired");
    }

    [Fact]
    public async Task Retired_instance_is_excluded_from_default_listing_but_included_when_requested()
    {
        var (tool, instance) = await CreateInstanceAsync();
        await _toolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto
        {
            Reason = "No longer needed",
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        var defaultList = await _toolInstanceAppService.GetListByToolAsync(tool.Id);
        defaultList.Items.ShouldNotContain(i => i.Id == instance.Id);

        var fullList = await _toolInstanceAppService.GetListByToolAsync(tool.Id, includeRetired: true);
        fullList.Items.ShouldContain(i => i.Id == instance.Id);
    }
}
