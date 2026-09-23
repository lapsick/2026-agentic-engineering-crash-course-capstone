using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.ToolInstances;
using Xunit;

namespace ToolShare.Catalog.Tools;

public class ToolListEmptyAndRetiredTests : CatalogApplicationTestBase
{
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;
    private readonly IToolInstanceAppService _toolInstanceAppService;

    public ToolListEmptyAndRetiredTests()
    {
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
        _toolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
    }

    private async Task<CategoryDto> CreateCategoryAsync()
    {
        return await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
    }

    [Fact]
    public async Task A_no_match_query_returns_zero_total_count_instead_of_throwing()
    {
        var result = await _toolAppService.GetListAsync(new GetToolListInput { Filter = "no such tool anywhere" });

        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Retired_instances_are_excluded_from_the_instance_count_by_default()
    {
        var category = await CreateCategoryAsync();
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Drill With History", CategoryId = category.Id });

        var keep = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto { ToolId = tool.Id, SerialNumber = "SN-KEEP", Condition = ToolCondition.Good });
        var retire = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto { ToolId = tool.Id, SerialNumber = "SN-RETIRE", Condition = ToolCondition.Good });

        await _toolInstanceAppService.RetireAsync(retire.Id, new RetireToolInstanceDto { Reason = "Broken", ConcurrencyStamp = retire.ConcurrencyStamp });

        var defaultResult = await _toolAppService.GetListAsync(new GetToolListInput());
        defaultResult.Items.Single().InstanceCount.ShouldBe(1);

        var includingRetiredResult = await _toolAppService.GetListAsync(new GetToolListInput { IncludeRetiredInstances = true });
        includingRetiredResult.Items.Single().InstanceCount.ShouldBe(2);
    }
}
