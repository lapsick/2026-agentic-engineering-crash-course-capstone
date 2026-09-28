using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 FR-014, MAINT-01: two Librarians reporting the same instance at the
/// same moment — exactly one report is accepted, the other is refused as
/// "already open", and exactly one open request exists.
/// </summary>
public class ReportVersusReportConcurrencyTests : LendingAuthorizationTestBase
{
    private const int Iterations = 3;

    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;

    public ReportVersusReportConcurrencyTests()
    {
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _maintenanceRequestRepository = GetRequiredService<IMaintenanceRequestRepository>();
    }

    [Fact]
    public async Task Two_simultaneous_reports_yield_exactly_one_open_request()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var (_, _, instance) = await SeedCatalogDataAsync();
            var refusals = new ConcurrentQueue<string>();

            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(n => Task.Run(async () =>
            {
                try
                {
                    using (AsLibrarian())
                    {
                        await _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
                        {
                            ToolInstanceId = instance.Id,
                            ObservedCondition = ToolCondition.Good,
                            Reason = $"Report {n}"
                        });
                    }

                    return true;
                }
                catch (BusinessException exception)
                {
                    refusals.Enqueue(exception.Code ?? exception.GetType().Name);
                    return false;
                }
            })));

            results.Count(accepted => accepted).ShouldBe(1, $"iteration {i}");
            refusals.Count.ShouldBe(1, $"iteration {i}");
            refusals.Single().ShouldBe(LendingDomainErrorCodes.MaintenanceRequestAlreadyOpen, $"iteration {i}");

            var open = await WithUnitOfWorkAsync(() => _maintenanceRequestRepository.GetListAsync(r =>
                r.ToolInstanceId == instance.Id && r.Status == MaintenanceRequestStatus.Open));
            open.Count.ShouldBe(1, $"iteration {i}");
        }
    }
}
