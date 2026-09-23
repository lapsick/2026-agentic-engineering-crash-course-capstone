using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Tools;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Acts as a stand-in downstream module (SC-007, FR-017): resolves only
/// <see cref="IToolInstanceLookupAppService"/> and its Tier 1 DTOs. Setup goes
/// through the module's own (Tier 2, but still Application.Contracts-only)
/// app services — see contracts/README.md's "How SC-007 is verified" and
/// T109's <c>ContractIsolationTests</c> for the isolation this file must obey.
/// </summary>
public class ToolInstanceLookupContractTests : CatalogApplicationTestBase
{
    private readonly IToolInstanceLookupAppService _lookupAppService;
    private readonly IToolInstanceAppService _toolInstanceAppService;
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;

    public ToolInstanceLookupContractTests()
    {
        _lookupAppService = GetRequiredService<IToolInstanceLookupAppService>();
        _toolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
    }

    private async Task<(CategoryDto, ToolDto, ToolInstanceDto)> CreateInstanceAsync(ToolCondition condition = ToolCondition.Good)
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer " + Guid.NewGuid(), CategoryId = category.Id });
        var instance = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = condition
        });
        return (category, tool, instance);
    }

    [Fact]
    public async Task FindAsync_returns_the_denormalized_lookup_for_a_known_instance()
    {
        var (category, tool, instance) = await CreateInstanceAsync(ToolCondition.Good);

        var found = await _lookupAppService.FindAsync(instance.Id);

        found.ShouldNotBeNull();
        found.Id.ShouldBe(instance.Id);
        found.ToolId.ShouldBe(tool.Id);
        found.ToolName.ShouldBe(tool.Name);
        found.CategoryId.ShouldBe(category.Id);
        found.CategoryName.ShouldBe(category.Name);
        found.SerialNumber.ShouldBe(instance.SerialNumber);
        found.Condition.ShouldBe(ToolCondition.Good);
        found.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        found.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task FindAsync_returns_a_retired_instance_rather_than_hiding_it()
    {
        var (_, _, instance) = await CreateInstanceAsync();

        await _toolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto
        {
            Reason = "Motor burnt out",
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        var found = await _lookupAppService.FindAsync(instance.Id);

        found.ShouldNotBeNull();
        found.CirculationState.ShouldBe(ToolInstanceCirculationState.Retired);
        found.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task FindAsync_returns_null_for_an_unknown_id()
    {
        var found = await _lookupAppService.FindAsync(Guid.NewGuid());

        found.ShouldBeNull();
    }

    [Fact]
    public async Task IsAvailableAsync_returns_false_for_an_unknown_id_rather_than_throwing()
    {
        var isAvailable = await _lookupAppService.IsAvailableAsync(Guid.NewGuid());

        isAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task GetByIdsAsync_omits_unknown_ids_without_throwing()
    {
        var (_, _, instanceA) = await CreateInstanceAsync();
        var (_, _, instanceB) = await CreateInstanceAsync();
        var unknownId = Guid.NewGuid();

        var results = await _lookupAppService.GetByIdsAsync(new[] { instanceA.Id, unknownId, instanceB.Id });

        results.Count.ShouldBe(2);
        results.Select(r => r.Id).ShouldBe(new[] { instanceA.Id, instanceB.Id }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetByIdsAsync_with_empty_input_returns_an_empty_list()
    {
        var results = await _lookupAppService.GetByIdsAsync(Array.Empty<Guid>());

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task IsAvailable_formula_matches_InCirculation_and_not_Damaged()
    {
        var (_, _, inCirculationGood) = await CreateInstanceAsync(ToolCondition.Good);
        var (_, _, inCirculationDamaged) = await CreateInstanceAsync(ToolCondition.Damaged);
        var (_, _, retired) = await CreateInstanceAsync(ToolCondition.New);

        await _toolInstanceAppService.RetireAsync(retired.Id, new RetireToolInstanceDto
        {
            Reason = "No longer needed",
            ConcurrencyStamp = retired.ConcurrencyStamp
        });

        (await _lookupAppService.IsAvailableAsync(inCirculationGood.Id)).ShouldBeTrue();
        (await _lookupAppService.IsAvailableAsync(inCirculationDamaged.Id)).ShouldBeFalse();
        (await _lookupAppService.IsAvailableAsync(retired.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task GetListAsync_default_excludes_retired_and_respects_OnlyAvailable()
    {
        var (_, tool, available) = await CreateInstanceAsync(ToolCondition.Good);
        var damaged = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.Damaged
        });
        var retired = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.New
        });
        await _toolInstanceAppService.RetireAsync(retired.Id, new RetireToolInstanceDto
        {
            Reason = "No longer needed",
            ConcurrencyStamp = retired.ConcurrencyStamp
        });

        var defaultList = await _lookupAppService.GetListAsync(new ToolInstanceLookupFilterDto { ToolId = tool.Id });
        defaultList.Items.Select(i => i.Id).ShouldBe(new[] { available.Id, damaged.Id }, ignoreOrder: true);

        var onlyAvailable = await _lookupAppService.GetListAsync(new ToolInstanceLookupFilterDto { ToolId = tool.Id, OnlyAvailable = true });
        onlyAvailable.Items.Select(i => i.Id).ShouldBe(new List<Guid> { available.Id });

        var includeRetired = await _lookupAppService.GetListAsync(new ToolInstanceLookupFilterDto { ToolId = tool.Id, IncludeRetired = true });
        includeRetired.Items.Select(i => i.Id).ShouldBe(new[] { available.Id, damaged.Id, retired.Id }, ignoreOrder: true);
    }
}
