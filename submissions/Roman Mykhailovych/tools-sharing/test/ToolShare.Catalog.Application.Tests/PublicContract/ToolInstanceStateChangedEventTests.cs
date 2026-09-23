using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Tools;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// SC-007/FR-018: registers a probe <see cref="ILocalEventHandler{TEvent}"/> for
/// the Tier 1 <see cref="ToolInstanceStateChangedEto"/> and asserts registration
/// and retirement each raise exactly the event contracts/catalog-events.md
/// promises. Only references <c>ToolShare.Catalog.ToolInstances</c> (this
/// event, plus the Tier 2 app-service contracts needed to perform the catalog
/// action) — see <see cref="ContractIsolationTests"/>.
/// </summary>
public class ToolInstanceStateChangedEventTests : CatalogApplicationTestBase
{
    private readonly IToolInstanceAppService _toolInstanceAppService;
    private readonly IToolAppService _toolAppService;
    private readonly ICategoryAppService _categoryAppService;
    private readonly ToolInstanceStateChangedEventCapture _capture;

    public ToolInstanceStateChangedEventTests()
    {
        _toolInstanceAppService = GetRequiredService<IToolInstanceAppService>();
        _toolAppService = GetRequiredService<IToolAppService>();
        _categoryAppService = GetRequiredService<ICategoryAppService>();
        _capture = GetRequiredService<ToolInstanceStateChangedEventCapture>();
    }

    [Fact]
    public async Task Registering_and_then_retiring_an_instance_raises_the_expected_events_in_order()
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Rotary Hammer", CategoryId = category.Id });

        var instance = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.Good
        });

        _capture.Events.Count.ShouldBe(1);
        var registeredEvent = _capture.Events[0];
        registeredEvent.ToolInstanceId.ShouldBe(instance.Id);
        registeredEvent.ToolId.ShouldBe(tool.Id);
        registeredEvent.PreviousCondition.ShouldBeNull();
        registeredEvent.NewCondition.ShouldBe(ToolCondition.Good);
        registeredEvent.PreviousCirculationState.ShouldBeNull();
        registeredEvent.NewCirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        registeredEvent.Reason.ShouldBeNull();

        await _toolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto
        {
            Reason = "Motor burnt out",
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        _capture.Events.Count.ShouldBe(2);
        var retiredEvent = _capture.Events[1];
        retiredEvent.ToolInstanceId.ShouldBe(instance.Id);
        retiredEvent.PreviousCirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        retiredEvent.NewCirculationState.ShouldBe(ToolInstanceCirculationState.Retired);
        retiredEvent.Reason.ShouldBe("Motor burnt out");
    }

    [Fact]
    public async Task Changing_condition_raises_an_event_with_the_previous_and_new_condition()
    {
        var category = await _categoryAppService.CreateAsync(new CreateCategoryDto { Name = Guid.NewGuid().ToString() });
        var tool = await _toolAppService.CreateAsync(new CreateToolDto { Name = "Impact Driver", CategoryId = category.Id });
        var instance = await _toolInstanceAppService.CreateAsync(new CreateToolInstanceDto
        {
            ToolId = tool.Id,
            SerialNumber = Guid.NewGuid().ToString("N")[..8],
            Condition = ToolCondition.Good
        });

        await _toolInstanceAppService.ChangeConditionAsync(instance.Id, new ChangeToolInstanceConditionDto
        {
            Condition = ToolCondition.Worn,
            Reason = "Visible wear",
            ConcurrencyStamp = instance.ConcurrencyStamp
        });

        _capture.Events.Count.ShouldBe(2); // registration + this change
        var changedEvent = _capture.Events[1];
        changedEvent.PreviousCondition.ShouldBe(ToolCondition.Good);
        changedEvent.NewCondition.ShouldBe(ToolCondition.Worn);
        changedEvent.PreviousCirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        changedEvent.NewCirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        changedEvent.Reason.ShouldBe("Visible wear");
    }
}

/// <summary>Per-test-instance capture buffer (a fresh DI container is built per test method — see CatalogApplicationTestModule's notes).</summary>
public class ToolInstanceStateChangedEventCapture : ISingletonDependency
{
    public List<ToolInstanceStateChangedEto> Events { get; } = new();
}

/// <summary>
/// The probe handler itself (FR-018/SC-007): discovered purely by ABP's
/// conventional registration of this test assembly (the same mechanism a real
/// downstream module would use in production) — no explicit wiring anywhere.
/// </summary>
public class ToolInstanceStateChangedProbeHandler : ILocalEventHandler<ToolInstanceStateChangedEto>, ITransientDependency
{
    private readonly ToolInstanceStateChangedEventCapture _capture;

    public ToolInstanceStateChangedProbeHandler(ToolInstanceStateChangedEventCapture capture)
    {
        _capture = capture;
    }

    public Task HandleEventAsync(ToolInstanceStateChangedEto eventData)
    {
        _capture.Events.Add(eventData);
        return Task.CompletedTask;
    }
}
