using System;
using System.Threading;
using System.Threading.Tasks;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 research R4: the per-instance serialization point between an
/// out-of-band maintenance report and reservation creation. No declarative
/// constraint can say "no active reservation while Catalog says
/// UnderMaintenance" — that spans two schemas (Constitution III) — so both
/// operations take this lock <b>before</b> reading Catalog availability.
/// The lock is transaction-scoped: it is held until the ambient unit of
/// work's transaction commits or rolls back, and requires one.
/// </summary>
public interface IInstanceLock
{
    Task LockInstanceAsync(Guid toolInstanceId, CancellationToken cancellationToken = default);
}
