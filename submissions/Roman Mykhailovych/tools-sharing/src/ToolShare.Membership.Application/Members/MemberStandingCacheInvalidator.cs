using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace ToolShare.Membership.Members;

/// <summary>
/// Evicts the cached standing entry inside the same unit of work that recorded
/// the change (<see cref="Member"/> raises <see cref="MemberStandingChangedEto"/>
/// from the same private helper that appends the history row — MR-07/HR-04), so
/// a deactivation is refused on the caller's very next action (FR-006, FR-006a) —
/// no stale-cache window.
/// </summary>
public class MemberStandingCacheInvalidator : ILocalEventHandler<MemberStandingChangedEto>, ITransientDependency
{
    private readonly IDistributedCache<MemberStandingCacheItem> _cache;

    public MemberStandingCacheInvalidator(IDistributedCache<MemberStandingCacheItem> cache)
    {
        _cache = cache;
    }

    public virtual async Task HandleEventAsync(MemberStandingChangedEto eventData)
    {
        await _cache.RemoveAsync(MemberStandingProvider.GetCacheKey(eventData.IdentityUserId));
    }
}
