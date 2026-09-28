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

    /// <summary>
    /// 008-out-of-band-maintenance: a problem found on an instance that is in
    /// circulation and not on loan. Moves it InCirculation → UnderMaintenance
    /// and, if <paramref name="observedCondition"/> is worse, records it.
    /// Appends exactly one history row carrying <paramref name="reason"/> and
    /// raises <see cref="ToolInstanceStateChangedEto"/>. Closed by the existing
    /// <see cref="MarkMaintenanceClosedAsync"/>. Additive to this Tier 1
    /// contract — see specs/008-out-of-band-maintenance/contracts/catalog-extension.md.
    /// </summary>
    Task MarkSentToMaintenanceAsync(Guid toolInstanceId, ToolCondition observedCondition, string reason);
}
