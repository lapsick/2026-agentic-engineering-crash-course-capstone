using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// FR-024/FR-025: <see cref="IMemberStandingAppService"/> never throws for
/// absence and always distinguishes "not enrolled" from "enrolled but
/// deactivated" via the two-flag design (contracts/membership-public-contracts.md).
/// </summary>
public class MemberStandingContractTests : MembershipAuthorizationTestBase
{
    private readonly IMemberStandingAppService _standingAppService;

    public MemberStandingContractTests()
    {
        _standingAppService = GetRequiredService<IMemberStandingAppService>();
    }

    [Fact]
    public async Task Unknown_identity_yields_not_enrolled_without_throwing()
    {
        var standing = await _standingAppService.GetByIdentityUserIdAsync(Guid.NewGuid());

        standing.IsEnrolled.ShouldBeFalse();
        standing.IsActive.ShouldBeFalse();
        standing.MemberId.ShouldBeNull();
        standing.CurrentRating.ShouldBe(0);
        standing.EffectiveConcurrentLoanLimit.ShouldBe(0);
    }

    [Fact]
    public async Task Unknown_member_id_yields_not_enrolled_without_throwing()
    {
        var standing = await _standingAppService.GetAsync(Guid.NewGuid());

        standing.IsEnrolled.ShouldBeFalse();
        standing.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivated_member_is_returned_with_IsActive_false()
    {
        var member = await EnrolAsAdminAsync("Standing Deactivated Target");
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            await MemberAppService.DeactivateAsync(member.Id, new DeactivateMemberDto
            {
                Reason = "Testing standing lookup",
                ConcurrencyStamp = member.ConcurrencyStamp
            });
        }

        var standing = await _standingAppService.GetAsync(member.Id);

        standing.IsEnrolled.ShouldBeTrue();
        standing.IsActive.ShouldBeFalse();
        standing.MemberId.ShouldBe(member.Id);
    }

    [Fact]
    public async Task GetByIdentityUserIdAsync_returns_the_active_members_standing()
    {
        var member = await EnrolAsAdminAsync("Standing Active Target");

        var standing = await _standingAppService.GetByIdentityUserIdAsync(member.IdentityUserId);

        standing.IsEnrolled.ShouldBeTrue();
        standing.IsActive.ShouldBeTrue();
        standing.MemberId.ShouldBe(member.Id);
        standing.IdentityUserId.ShouldBe(member.IdentityUserId);
        standing.CurrentRating.ShouldBe(100);
        standing.EffectiveConcurrentLoanLimit.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetByIdsAsync_omits_unknown_ids_without_throwing()
    {
        var memberA = await EnrolAsAdminAsync("Standing Batch A");
        var memberB = await EnrolAsAdminAsync("Standing Batch B");
        var unknownId = Guid.NewGuid();

        var results = await _standingAppService.GetByIdsAsync(new[] { memberA.Id, unknownId, memberB.Id });

        results.Count.ShouldBe(2);
        results.Select(r => r.MemberId).ShouldBe(new Guid?[] { memberA.Id, memberB.Id }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetByIdsAsync_with_empty_input_returns_an_empty_list()
    {
        var results = await _standingAppService.GetByIdsAsync(Array.Empty<Guid>());

        results.ShouldBeEmpty();
    }
}
