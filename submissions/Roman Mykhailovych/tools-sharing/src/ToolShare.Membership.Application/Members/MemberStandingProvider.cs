using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace ToolShare.Membership.Members;

/// <summary>
/// The O(1) standing read the enrolment gate (research R3) checks on every
/// application-service call — backed by <c>IDistributedCache&lt;MemberStandingCacheItem&gt;</c>
/// so a hot path never hits the database. Kept fresh by
/// <see cref="MemberStandingCacheInvalidator"/>.
/// </summary>
public class MemberStandingProvider : IMemberStandingProvider, ITransientDependency
{
    private static readonly DistributedCacheEntryOptions CacheEntryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    private readonly IDistributedCache<MemberStandingCacheItem> _cache;
    private readonly IMemberRepository _memberRepository;

    public MemberStandingProvider(IDistributedCache<MemberStandingCacheItem> cache, IMemberRepository memberRepository)
    {
        _cache = cache;
        _memberRepository = memberRepository;
    }

    public virtual async Task<MemberStandingSnapshot?> GetByIdentityUserIdAsync(Guid identityUserId)
    {
        var cacheItem = await _cache.GetOrAddAsync(
            GetCacheKey(identityUserId),
            async () =>
            {
                var member = await _memberRepository.FindByIdentityUserIdAsync(identityUserId);
                return member is null
                    ? new MemberStandingCacheItem { IsEnrolled = false }
                    : new MemberStandingCacheItem
                    {
                        IsEnrolled = true,
                        MemberId = member.Id,
                        IsActive = member.IsActive,
                        DisplayName = member.DisplayName,
                        Status = member.Status,
                        Role = member.Role,
                        CurrentRating = member.CurrentRating
                    };
            },
            () => CacheEntryOptions);

        return cacheItem is { IsEnrolled: true }
            ? new MemberStandingSnapshot
            {
                MemberId = cacheItem.MemberId!.Value,
                IsActive = cacheItem.IsActive,
                DisplayName = cacheItem.DisplayName,
                Status = cacheItem.Status,
                Role = cacheItem.Role,
                CurrentRating = cacheItem.CurrentRating
            }
            : null;
    }

    /// <summary>Shared with <see cref="MemberStandingCacheInvalidator"/> so eviction always targets the exact key a read would have used.</summary>
    public static string GetCacheKey(Guid identityUserId) => identityUserId.ToString("D");
}
