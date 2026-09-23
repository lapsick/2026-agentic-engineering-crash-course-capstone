using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// The inbound counterpart to <see cref="IToolInstanceLookupAppService"/> (002):
/// a downstream module (Lending, 004) reports lending-driven facts about an
/// instance it does not own. Mirrors the shape of Membership's
/// <c>IReliabilityReportingAppService</c> (003). Consumers depend on this
/// interface only — never on Catalog's Domain or EntityFrameworkCore layer.
/// See specs/004-lending/contracts/catalog-extension.md.
/// </summary>
public interface IToolInstanceCirculationReportingAppService : IApplicationService
{
    Task MarkOnLoanAsync(Guid toolInstanceId);

    Task MarkReturnedAsync(Guid toolInstanceId, ToolCondition returnedCondition);

    Task MarkReturnedForMaintenanceAsync(Guid toolInstanceId, ToolCondition returnedCondition);

    Task MarkMaintenanceClosedAsync(Guid toolInstanceId);
}
