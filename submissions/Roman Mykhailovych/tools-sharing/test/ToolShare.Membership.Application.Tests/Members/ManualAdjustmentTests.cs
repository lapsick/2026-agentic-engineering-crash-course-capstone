using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// FR-021: manual rating adjustments are Administrator-only, require a reason,
/// leave every previous history entry untouched, and are attributed to the
/// acting Administrator.
/// </summary>
public class ManualAdjustmentTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task Only_an_administrator_may_adjust_a_members_rating()
    {
        var member = await EnrolAsAdminAsync("Adjust Rating Auth Target");

        using (AsMemberWithNoGrants())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => MemberAppService.AdjustRatingAsync(member.Id, new AdjustMemberRatingDto
            {
                Points = -10,
                Reason = "Should be denied",
                ConcurrencyStamp = member.ConcurrencyStamp
            }));
        }
    }

    [Fact]
    public async Task A_reason_is_mandatory()
    {
        var member = await EnrolAsAdminAsync("Adjust Rating Reason Target");
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            await Should.ThrowAsync<AbpValidationException>(() => MemberAppService.AdjustRatingAsync(member.Id, new AdjustMemberRatingDto
            {
                Points = -10,
                Reason = string.Empty,
                ConcurrencyStamp = member.ConcurrencyStamp
            }));
        }
    }

    [Fact]
    public async Task Previous_history_entries_are_left_untouched_and_the_adjustment_is_attributed_to_the_acting_administrator()
    {
        var member = await EnrolAsAdminAsync("Adjust Rating Attribution Target", CommunityRole.Librarian);
        var adminPrincipal = await BuildAdminPrincipalAsync();
        var adminIdentityId = Guid.Parse(adminPrincipal.FindFirst(AbpClaimTypes.UserId)!.Value);

        using (Impersonate(adminPrincipal))
        {
            var before = await MemberAppService.GetStandingHistoryAsync(member.Id);
            before.Count.ShouldBe(2); // Enrolled + the RoleChanged from EnrolAsAdminAsync(role: Librarian)

            var adjusted = await MemberAppService.AdjustRatingAsync(member.Id, new AdjustMemberRatingDto
            {
                Points = -15,
                Reason = "Correcting a mistaken penalty",
                ConcurrencyStamp = member.ConcurrencyStamp
            });

            adjusted.CurrentRating.ShouldBe(85);

            var after = await MemberAppService.GetStandingHistoryAsync(member.Id);
            after.Count.ShouldBe(before.Count + 1);

            // Every entry that existed before the adjustment is byte-for-byte
            // untouched (FR-020/FR-021) — asserted by re-reading and diffing,
            // not merely by count.
            for (var i = 0; i < before.Count; i++)
            {
                after[i].Id.ShouldBe(before[i].Id);
                after[i].Kind.ShouldBe(before[i].Kind);
                after[i].ChangedAt.ShouldBe(before[i].ChangedAt);
                after[i].ChangedByUserId.ShouldBe(before[i].ChangedByUserId);
                after[i].Reason.ShouldBe(before[i].Reason);
                after[i].PreviousStatus.ShouldBe(before[i].PreviousStatus);
                after[i].NewStatus.ShouldBe(before[i].NewStatus);
                after[i].PreviousRole.ShouldBe(before[i].PreviousRole);
                after[i].NewRole.ShouldBe(before[i].NewRole);
                after[i].RawPoints.ShouldBe(before[i].RawPoints);
                after[i].EffectivePoints.ShouldBe(before[i].EffectivePoints);
                after[i].ResultingRating.ShouldBe(before[i].ResultingRating);
            }

            var newEntry = after[^1];
            newEntry.Kind.ShouldBe(MemberStandingChangeKind.RatingOutcome);
            newEntry.OutcomeType.ShouldBe(ReliabilityOutcomeType.ManualAdjustment);
            newEntry.RawPoints.ShouldBe(-15);
            newEntry.EffectivePoints.ShouldBe(-15);
            newEntry.ResultingRating.ShouldBe(85);
            newEntry.Reason.ShouldBe("Correcting a mistaken penalty");
            newEntry.ChangedByUserId.ShouldBe(adminIdentityId);
            newEntry.ChangedByDisplayName.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
