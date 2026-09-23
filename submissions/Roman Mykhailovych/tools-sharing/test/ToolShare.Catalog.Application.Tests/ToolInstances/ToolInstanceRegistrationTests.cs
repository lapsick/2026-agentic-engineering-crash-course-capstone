using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Tools;
using Volo.Abp;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

public class ToolInstanceRegistrationTests : CatalogApplicationTestBase
{
    private readonly IToolInstanceAppService _toolInstanceAppService;
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;

    public ToolInstanceRegistrationTests()
    {
        _toolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
    }

    private async Task<ToolDto> CreateToolAsync(string name = "Rotary Hammer")
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        return await _toolAppService.CreateAsync(new CreateToolDto { Name = name, CategoryId = category.Id });
    }

    [Fact]
    public async Task Registering_an_instance_succeeds_and_starts_in_circulation()
    {
        var tool = await CreateToolAsync();

        var instance = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = "RH-001",
            Condition = ToolCondition.Good
        });

        instance.SerialNumber.ShouldBe("RH-001");
        instance.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        instance.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Registering_a_duplicate_serial_number_is_rejected()
    {
        var tool = await CreateToolAsync();
        await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto { ToolId = tool.Id, SerialNumber = "RH-002", Condition = ToolCondition.New });

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto { ToolId = tool.Id, SerialNumber = "RH-002", Condition = ToolCondition.Good }));

        exception.Code.ShouldBe("Catalog:SerialNumberAlreadyExists");
    }

    [Fact]
    public async Task Duplicate_serial_number_rejection_is_case_insensitive()
    {
        var tool = await CreateToolAsync();
        await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto { ToolId = tool.Id, SerialNumber = "RH-003", Condition = ToolCondition.New });

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto { ToolId = tool.Id, SerialNumber = "rh-003", Condition = ToolCondition.Good }));

        exception.Code.ShouldBe("Catalog:SerialNumberAlreadyExists");
    }

    [Fact]
    public async Task Duplicate_serial_number_rejection_is_catalog_wide_across_different_tools()
    {
        var toolA = await CreateToolAsync("Rotary Hammer");
        var toolB = await CreateToolAsync("Impact Driver");

        await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto { ToolId = toolA.Id, SerialNumber = "SN-100", Condition = ToolCondition.New });

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto { ToolId = toolB.Id, SerialNumber = "SN-100", Condition = ToolCondition.New }));

        exception.Code.ShouldBe("Catalog:SerialNumberAlreadyExists");
    }

    [Fact]
    public async Task Registering_an_instance_for_an_unknown_tool_is_rejected()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
            {
                ToolId = Guid.NewGuid(),
                SerialNumber = "SN-999",
                Condition = ToolCondition.New
            }));

        exception.Code.ShouldBe("Catalog:ToolNotFound");
    }
}
